using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UEFA.Rag.Indexer.Models;

namespace UEFA.Rag.Indexer.Services;

public class IndexStateManager
{
    private static readonly LogStream Log = LogStream.Instance;

    public const string StateFileName = ".ragindex-state.json";

    /// <summary>
    /// Loads the index state from the state file in the root folder.
    /// Returns an empty state if the file doesn't exist or is corrupt.
    /// </summary>
    public IndexState Load(string rootFolder)
    {
        var statePath = Path.Combine(rootFolder, StateFileName);

        if (!File.Exists(statePath))
        {
            Log.Info("IndexState", "No existing index state found. Will perform full index.");
            return new IndexState();
        }

        try
        {
            var json = File.ReadAllText(statePath);
            var state = JsonSerializer.Deserialize<IndexState>(json);

            if (state == null)
            {
                Log.Warning("IndexState", "State file is empty or corrupt. Starting fresh.");
                return new IndexState();
            }

            Log.Info("IndexState", $"Loaded index state with {state.Files.Count} tracked files.");
            return state;
        }
        catch (Exception ex)
        {
            Log.Warning("IndexState", $"Failed to read state file: {ex.Message}. Starting fresh.");
            return new IndexState();
        }
    }

    /// <summary>
    /// Saves the index state to the state file in the root folder.
    /// </summary>
    public void Save(string rootFolder, IndexState state)
    {
        var statePath = Path.Combine(rootFolder, StateFileName);

        try
        {
            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(statePath, json);
            Log.Info("IndexState", $"Saved index state ({state.Files.Count} files tracked).");
        }
        catch (Exception ex)
        {
            Log.Error("IndexState", $"Failed to save state file: {ex.Message}");
        }
    }

    /// <summary>
    /// Computes a hash of the file content. Uses SHA-256 of the normalized content.
    /// </summary>
    public static string ComputeFileHash(string filePath)
    {
        var content = File.ReadAllText(filePath);

        // Normalize line endings to avoid false positives from CRLF vs LF
        var normalized = content.Replace("\r\n", "\n");

        var bytes = Encoding.UTF8.GetBytes(normalized);
        var hash = SHA256.HashData(bytes);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Determines which files have changed (new, modified, deleted) since the last index.
    /// </summary>
    public DeltaResult ComputeDelta(
        string rootFolder,
        IndexState state,
        IEnumerable<string> currentFiles)
    {
        var currentFileSet = new HashSet<string>(currentFiles);
        var result = new DeltaResult();

        // Files to add or update (new or modified)
        foreach (var file in currentFiles)
        {
            if (!state.Files.TryGetValue(file, out var fileState))
            {
                // New file
                result.NewOrModified.Add(file);
            }
            else
            {
                // Check if content changed
                var currentHash = ComputeFileHash(file);
                if (currentHash != fileState.ContentHash)
                {
                    result.NewOrModified.Add(file);
                }
            }
        }

        // Files that have been deleted
        foreach (var trackedFile in state.Files.Keys)
        {
            if (!currentFileSet.Contains(trackedFile))
            {
                result.Deleted.Add(trackedFile);
            }
        }

        Log.Info("IndexState",
            $"Delta: {result.NewOrModified.Count} new/modified, {result.Deleted.Count} deleted, " +
            $"{currentFileSet.Count - result.NewOrModified.Count} unchanged.");

        return result;
    }
}

public class DeltaResult
{
    public List<string> NewOrModified { get; set; } = [];
    public List<string> Deleted { get; set; } = [];
}