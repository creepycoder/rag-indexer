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

        return Directory
            .EnumerateFiles(rootFolder, "*", SearchOption.AllDirectories)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f)))
            .Where(f => !ignore.IsIgnored(rootFolder, f));
    }
}
