using UEFA.Rag.Indexer.Models;

namespace UEFA.Rag.Indexer.Services;

public class IndexingService
{
    private static readonly LogStream Log = LogStream.Instance;

    private readonly RepositoryScanner _scanner;
    private readonly CSharpCodeParser _parser;
    private readonly IEmbeddingService _embedding;
    private readonly QdrantService _qdrant;
    private readonly IndexStateManager _stateManager;
    private RagIgnore? _ignore;


    public IndexingService(IEmbeddingService embedding)
    {
        _scanner = new RepositoryScanner();
        _parser = new CSharpCodeParser();
        _embedding = embedding;
        _qdrant = new QdrantService();
        _stateManager = new IndexStateManager();
    }


    public async Task IndexAsync(string rootFolder)
    {
        Log.Info("Indexer", $"Scanning: {rootFolder}");

        var collectionWasCreated = await _qdrant.EnsureCollectionExistsAsync();

        _ignore = new RagIgnore(rootFolder);

        // 1. Get all current files
        var allFiles = _scanner.Scan(rootFolder)
            .Where(x => !_ignore.IsIgnored(rootFolder, x))
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

        // 4. Handle deleted files: remove their points from Qdrant
        foreach (var deletedFile in delta.Deleted)
        {
            try
            {
                await _qdrant.DeleteByFilePathAsync(deletedFile);
                state.Files.Remove(deletedFile);
                Log.Info("Indexer", $"Removed from index: {deletedFile}");
            }
            catch (Exception ex)
            {
                Log.Error("Indexer", $"ERROR deleting {deletedFile}", ex.ToString());
            }
        }

        // 5. Handle new/modified files: re-index them
        var count = 0;

        foreach (var file in delta.NewOrModified)
        {
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

                    var vector = await _embedding.CreateAsync(chunk.Content);

                    await _qdrant.InsertAsync(chunk, vector);

                    count++;

                    Log.Info("Indexer", $"  Indexed {chunk.SymbolType}: {chunk.SymbolName}");
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
            }
        }

        // 6. Final state save (redundant but safe)
        _stateManager.Save(rootFolder, state);

        Log.Info("Indexer",
            $"Completed. Indexed {count} chunks ({delta.NewOrModified.Count} files processed, " +
            $"{delta.Deleted.Count} files removed, {allFiles.Count - delta.NewOrModified.Count} files unchanged).");
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


    private static string GetProjectName(
        string root,
        string file)
    {
        var relative = Path.GetRelativePath(root, file);

        return relative.Split(Path.DirectorySeparatorChar)[0];
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