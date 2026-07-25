using Microsoft.Extensions.FileSystemGlobbing;

namespace UEFA.Rag.Indexer.Services;

public sealed class RagIgnore
{
    private readonly Matcher _matcher = new();

    public RagIgnore(string rootFolder)
    {
        var file = Path.Combine(rootFolder, ".ragignore");

        if (!File.Exists(file))
            return;

        foreach (var line in File.ReadLines(file))
        {
            var pattern = line.Trim();

            if (string.IsNullOrWhiteSpace(pattern))
                continue;

            if (pattern.StartsWith('#'))
                continue;

            _matcher.AddExclude(pattern.Replace('\\', '/'));
        }
    }

    public bool IsIgnored(string rootFolder, string file)
    {
        var relative = Path.GetRelativePath(rootFolder, file)
            .Replace('\\', '/');

        return _matcher.Match(relative).HasMatches;
    }
}