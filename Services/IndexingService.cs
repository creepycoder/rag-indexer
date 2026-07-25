using UEFA.Rag.Indexer.Models;

namespace UEFA.Rag.Indexer.Services;

public class IndexingService
{
    private readonly RepositoryScanner _scanner;
    private readonly CSharpCodeParser _parser;
    private readonly EmbeddingService _embedding;
    private readonly QdrantService _qdrant;
    private RagIgnore? _ignore;


    public IndexingService()
    {
        _scanner = new RepositoryScanner();
        _parser = new CSharpCodeParser();
        _embedding = new EmbeddingService();
        _qdrant = new QdrantService();
    }


    public async Task IndexAsync(string rootFolder)
    {
        Console.WriteLine($"Scanning: {rootFolder}");

        _ignore = new RagIgnore(rootFolder);

        var files = _scanner.Scan(rootFolder)
            .Where(x => !_ignore.IsIgnored(rootFolder, x))
            .Where(IsSupportedFile);
        var count = 0;

        foreach (var file in files)
        {
            try
            {
                Console.WriteLine(
                    $"Processing: {file}");

                var extension =
                    Path.GetExtension(file);


                IEnumerable<CodeChunk> chunks = extension
                    .Equals(".cs",
                        StringComparison.OrdinalIgnoreCase)
                    ? ParseCSharp(file, rootFolder)
                    : ParseText(file, rootFolder);


                foreach (var chunk in chunks)
                {
                    var vector =
                        await _embedding.CreateAsync(
                            chunk.Content);


                    await _qdrant.InsertAsync(
                        chunk,
                        vector);


                    count++;

                    Console.WriteLine(
                        $"  Indexed {chunk.SymbolType}: {chunk.SymbolName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"ERROR {file}: {ex.Message}");
            }
        }


        Console.WriteLine(
            $"Completed. Indexed {count} chunks.");
    }


    private IEnumerable<CodeChunk> ParseCSharp(
        string file,
        string root)
    {
        var project =
            GetProjectName(root, file);


        return _parser.ParseFile(
            file,
            project);
    }


    private IEnumerable<CodeChunk> ParseText(
        string file,
        string root)
    {
        var content =
            File.ReadAllText(file);


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
        var relative =
            Path.GetRelativePath(root, file);

        return relative
            .Split(
                Path.DirectorySeparatorChar)[0];
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