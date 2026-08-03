using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Rag.Indexer.Models;

namespace Rag.Indexer.Services;

public sealed class RepositoryRegistry
{
    public const string RegistryFileName = "ragindex-repositories.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _registryPath;
    private readonly object _gate = new();

    public RepositoryRegistry()
        : this(ResolveRegistryPath())
    {
    }

    public RepositoryRegistry(string registryPath)
    {
        _registryPath = Path.GetFullPath(registryPath);
    }

    public static string ResolveRegistryPath(IConfiguration? configuration = null)
    {
        var configuredPath = configuration?.GetValue<string>("Indexing:RegistryPath");
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return Path.GetFullPath(configuredPath);
        }

        var workspaceRoot = FindWorkspaceRoot();
        return Path.Combine(workspaceRoot, RegistryFileName);
    }

    public IReadOnlyList<RepositoryEntry> GetAll()
    {
        lock (_gate)
        {
            return LoadEntriesNoLock();
        }
    }

    public void Record(string rootFolder, string project, int fileCount)
    {
        lock (_gate)
        {
            var entries = LoadEntriesNoLock();
            var normalizedRoot = NormalizePath(rootFolder);
            var now = DateTime.UtcNow;

            var existing = entries.FirstOrDefault(entry =>
                string.Equals(NormalizePath(entry.Path), normalizedRoot, GetPathComparison()));

            if (existing is null)
            {
                entries.Add(new RepositoryEntry
                {
                    Path = normalizedRoot,
                    Project = string.IsNullOrWhiteSpace(project) ? GetRepositoryName(normalizedRoot) : project,
                    LastIndexed = now,
                    FileCount = fileCount
                });
            }
            else
            {
                existing.Path = normalizedRoot;
                existing.Project = string.IsNullOrWhiteSpace(project) ? existing.Project : project;
                existing.LastIndexed = now;
                existing.FileCount = fileCount;
            }

            SaveEntriesNoLock(entries);
        }
    }

    private List<RepositoryEntry> LoadEntriesNoLock()
    {
        try
        {
            if (!File.Exists(_registryPath))
            {
                return [];
            }

            var json = File.ReadAllText(_registryPath);
            if (string.IsNullOrWhiteSpace(json))
            {
                return [];
            }

            var document = JsonSerializer.Deserialize<RepositoryRegistryDocument>(json, JsonOptions);
            return document?.Repositories ?? [];
        }
        catch
        {
            return [];
        }
    }

    private void SaveEntriesNoLock(List<RepositoryEntry> entries)
    {
        var directory = Path.GetDirectoryName(_registryPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var document = new RepositoryRegistryDocument
        {
            Repositories = entries
                .OrderBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };

        File.WriteAllText(_registryPath, JsonSerializer.Serialize(document, JsonOptions));
    }

    private static string NormalizePath(string path)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    private static StringComparison GetPathComparison()
    {
        return OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    }

    private static string GetRepositoryName(string path)
    {
        var name = Path.GetFileName(path);
        return string.IsNullOrWhiteSpace(name) ? path : name;
    }

    private static string FindWorkspaceRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Rag.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private sealed class RepositoryRegistryDocument
    {
        public List<RepositoryEntry> Repositories { get; set; } = [];
    }
}