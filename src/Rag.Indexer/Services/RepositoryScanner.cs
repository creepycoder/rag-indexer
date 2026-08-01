namespace Rag.Indexer.Services;

public class RepositoryScanner
{
    private static readonly HashSet<string> SupportedExtensions =
    [
        ".cs",
        ".csproj",
        ".sln",
        ".json",
        ".yaml",
        ".yml",
        ".xml",
        ".config",
        ".sql",
        ".md"
    ];

    public IEnumerable<string> Scan(string rootFolder)
    {
        var ignore = new RagIgnore(rootFolder);

        return ignore
            .GetIncludedFiles(rootFolder)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f)));
    }
}
