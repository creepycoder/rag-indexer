using Rag.Indexer.Models;

namespace Rag.Indexer.Services;

public class IndexingService
{
    private static readonly LogStream Log = LogStream.Instance;

    private readonly RepositoryScanner _scanner;
    private readonly CSharpCodeParser _parser;
    private readonly IEmbeddingService _embedding;
    private readonly QdrantService _qdrant;
    private readonly IndexStateManager _stateManager;
    private readonly EmbeddingCacheService _cache;
    private readonly RepositoryRegistry _repositoryRegistry;

    public IndexingService(
        IEmbeddingService embedding,
        EmbeddingCacheService? cache = null,
        RepositoryRegistry? repositoryRegistry = null)
    {
        _scanner = new RepositoryScanner();
        _parser = new CSharpCodeParser();
        _embedding = embedding;
        _qdrant = new QdrantService();
        _stateManager = new IndexStateManager();
        _cache = cache ?? new EmbeddingCacheService();
        _repositoryRegistry = repositoryRegistry ?? new RepositoryRegistry();
    }


    private static readonly IndexProgressTracker Progress = IndexProgressTracker.Instance;

    public async Task IndexAsync(string rootFolder)
    {
        Progress.Begin(rootFolder);
        Log.Info("Indexer", $"Scanning: {rootFolder}");

        try
        {
            if (!await _qdrant.IsAvailableAsync())
            {
                Log.Error("Indexer", "Qdrant is not available. Ensure Qdrant is running and try again.");
                Progress.End("error", "Qdrant is not available.");
                return;
            }

            var collectionWasCreated = await _qdrant.EnsureCollectionExistsAsync();

            Progress.SetPhase("scanning");

            // 1. Get all current files
            var allFiles = _scanner.Scan(rootFolder)
                .Where(IsSupportedFile)
                .Where(x => Path.GetFileName(x) != IndexStateManager.StateFileName)
                .ToList();

            // 2. Load previous index state
            var state = _stateManager.Load(rootFolder);

            // 3. If the collection was just created (e.g. user deleted it), the state file is stale.
            //    Discard it and do a full re-index.
            if (collectionWasCreated)
            {
                Log.Info("Indexer", "Collection was freshly created. State file is stale — performing full re-index.");
                state = new IndexState();
            }

            // 4. Compute delta
            var delta = _stateManager.ComputeDelta(rootFolder, state, allFiles);

            // How much work this run will do = deleted + new/modified files.
            Progress.SetPhase("counting");

            // Pre-flight: parse every file once (no embedding — cheap) so we know
            // the exact total chunk count in advance. Embedding chunks dominates
            // runtime, so a chunk-based percentage accurately reflects how much is left.
            var totalChunks = CountChunks(rootFolder, delta.NewOrModified);
            Progress.StartWork(delta.NewOrModified.Count, delta.Deleted.Count, totalChunks);

            // 4b. Handle deleted files: remove their points from Qdrant
            Progress.SetPhase("deleting");
            foreach (var deletedFile in delta.Deleted)
            {
                Progress.SetCurrentFile(deletedFile);
                try
                {
                    await _qdrant.DeleteByFilePathAsync(deletedFile);
                    state.Files.Remove(deletedFile);
                    Log.Info("Indexer", $"Removed from index: {deletedFile}");
                }
                catch (Exception ex)
                {
                    Log.Error("Indexer", $"ERROR deleting {deletedFile}", ex.ToString());
                    Progress.HasError($"Failed to delete {deletedFile}: {ex.Message}");
                }
                finally
                {
                    Progress.FileDeleted();
                }
            }

            // 5. Handle new/modified files: re-index them
            var count = 0;
            Progress.SetPhase("indexing");

            foreach (var file in delta.NewOrModified)
            {
                Progress.SetCurrentFile(file);
                try
                {
                    Log.Info("Indexer", $"Processing: {file}");

                    // For modified files, remove old points first
                    if (state.Files.ContainsKey(file))
                    {
                        await _qdrant.DeleteByFilePathAsync(file);
                    }

                    var extension = Path.GetExtension(file);

                    IEnumerable<CodeChunk> chunks = extension
                        .Equals(".cs", StringComparison.OrdinalIgnoreCase)
                        ? ParseCSharp(file, rootFolder)
                        : ParseText(file, rootFolder);

                    foreach (var chunk in chunks)
                    {
                        if (string.IsNullOrWhiteSpace(chunk.Content))
                        {
                            Log.Warning("Indexer", $"  Skipped empty chunk: {chunk.SymbolName}");
                            continue;
                        }

                        // Compute deterministic ID for upsert deduplication
                        chunk.ComputeDeterministicId();

                        // Check embedding cache first
                        var contentHash = EmbeddingCacheService.ComputeContentHash(chunk.Content);
                        var cached = _cache.Get(contentHash);

                        float[] vector;
                        if (cached is not null)
                        {
                            vector = cached.Embedding;
                            Log.Info("Indexer", $"  Using cached embedding for {chunk.SymbolType}: {chunk.SymbolName}");
                        }
                        else
                        {
                            vector = await _embedding.CreateAsync(chunk.Content);

                            // Store in cache for future use
                            _cache.Set(contentHash, vector, chunk.FilePath, chunk.SymbolType, chunk.SymbolName);
                            Log.Info("Indexer", $"  Indexed {chunk.SymbolType}: {chunk.SymbolName}");
                        }

                        await _qdrant.InsertAsync(chunk, vector);
                        count++;
                        Progress.AddChunk();
                    }

                    // Update state for this file
                    state.Files[file] = new FileState
                    {
                        ContentHash = IndexStateManager.ComputeFileHash(file),
                        LastIndexed = DateTime.UtcNow
                    };

                    // Save state after each file for interrupt-resilience.
                    // If the process is killed mid-way, the next run will skip
                    // files that were already fully processed.
                    _stateManager.Save(rootFolder, state);
                }
                catch (Exception ex)
                {
                    Log.Error("Indexer", $"ERROR {file}", ex.ToString());
                    Progress.HasError($"Failed to index {file}: {ex.Message}");
                }
                finally
                {
                    Progress.FileIndexed();
                }
            }

            // 6. Final state save (redundant but safe)
            _stateManager.Save(rootFolder, state);
            _repositoryRegistry.Record(rootFolder, GetRepositoryName(rootFolder), allFiles.Count);

            var summary =
                $"Completed. Indexed {count} chunks ({delta.NewOrModified.Count} files processed, " +
                $"{delta.Deleted.Count} files removed, {allFiles.Count - delta.NewOrModified.Count} files unchanged).";
            Log.Info("Indexer", summary);
            Progress.End("completed", summary);
        }
        catch (Exception ex)
        {
            Log.Error("Indexer", "Indexing failed unexpectedly.", ex.ToString());
            Progress.End("failed", "Indexing failed unexpectedly.");
        }
    }


    private IEnumerable<CodeChunk> ParseCSharp(
        string file,
        string root)
    {
        var project = GetProjectName(root, file);

        return _parser.ParseFile(file, project);
    }


    private IEnumerable<CodeChunk> ParseText(
        string file,
        string root)
    {
        var content = File.ReadAllText(file);

        yield return new CodeChunk
        {
            Project = GetProjectName(root, file),
            FilePath = file,
            SymbolType = "document",
            SymbolName = Path.GetFileName(file),
            Content = content
        };
    }


    /// <summary>
    /// Counts how many chunks a set of files will yield by parsing them once,
    /// without embedding. Used to know the total work ahead of time so the
    /// progress bar can report how much remains.
    /// </summary>
    private int CountChunks(
        string rootFolder,
        IEnumerable<string> files)
    {
        var total = 0;

        foreach (var file in files)
        {
            var extension = Path.GetExtension(file);
            var chunks = extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)
                ? ParseCSharp(file, rootFolder)
                : ParseText(file, rootFolder);

            foreach (var _ in chunks)
                total++;
        }

        return total;
    }


    private static string GetProjectName(
        string root,
        string file)
    {
        var relative = Path.GetRelativePath(root, file).AsSpan();
        var sep = Path.DirectorySeparatorChar;

        var idx = relative.IndexOf(sep);
        return idx >= 0 ? relative[..idx].ToString() : relative.ToString();
    }

    private static string GetRepositoryName(string rootFolder)
    {
        var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootFolder));
        var name = Path.GetFileName(normalized);
        return string.IsNullOrWhiteSpace(name) ? normalized : name;
    }

    private static bool IsSupportedFile(string file)
    {
        var ext = Path.GetExtension(file);

        return ext switch
        {
            ".cs" => true,
            ".csproj" => true,
            ".sln" => true,
            ".json" => true,
            ".yaml" => true,
            ".yml" => true,
            ".md" => true,
            ".sql" => true,
            ".xml" => true,
            ".config" => true,
            _ => false
        };
    }
}
