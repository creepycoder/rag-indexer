using Microsoft.Extensions.FileSystemGlobbing;

namespace Rag.Indexer.Services;

public sealed class RagIgnore
{
    private readonly Matcher _matcher = new();

    public RagIgnore(string rootFolder)
    {
        var file = Path.Combine(rootFolder, ".ragignore");

        // An exclude-only matcher matches nothing (both Match() and
        // GetResultsInFullPath() return empty results). Add a catch-all include
        // so excludes are applied against the full file set.
        _matcher.AddInclude("**/*");

        if (!File.Exists(file))
            return;

        foreach (var line in File.ReadLines(file))
        {
            var pattern = line.Trim();

            if (string.IsNullOrWhiteSpace(pattern))
                continue;

            if (pattern.StartsWith('#'))
                continue;

            _matcher.AddExclude(Normalize(pattern));
        }
    }

    /// <summary>
    /// Returns the full paths of all files under <paramref name="rootFolder"/>
    /// that are NOT ignored by the .ragignore rules.
    /// </summary>
    public IEnumerable<string> GetIncludedFiles(string rootFolder)
        => _matcher.GetResultsInFullPath(rootFolder);

    /// <summary>
    /// Normalizes a .ragignore pattern to FileSystemGlobbing syntax so it
    /// matches at any depth (mirroring gitignore semantics).
    /// </summary>
    private static string Normalize(string pattern)
    {
        pattern = pattern.Replace('\\', '/');

        if (pattern.StartsWith("**/", StringComparison.Ordinal))
            return pattern;

        return pattern.StartsWith('/')
            ? pattern.TrimStart('/')
            : $"**/{pattern}";
    }
}
