using System.Text.Json;
using Rag.Indexer.Models;

namespace Rag.Indexer.Services;

/// <summary>
/// Exports all indexed points from Qdrant to a portable JSON file.
/// This allows migration to another vector DB, analysis, or manual inspection.
/// </summary>
public class BackupService
{
    private static readonly LogStream Log = LogStream.Instance;

    private readonly QdrantService _qdrant;

    public BackupService()
    {
        _qdrant = new QdrantService();
    }

    /// <summary>
    /// Exports all points from the Qdrant collection to a JSON file.
    /// </summary>
    public async Task<int> ExportToJsonAsync(string outputPath)
    {
        Log.Info("Backup", $"Exporting all points to: {outputPath}");

        var points = await _qdrant.GetAllPointsAsync();

        if (points.Count == 0)
        {
            Log.Warning("Backup", "No points found in collection. Nothing to export.");
            return 0;
        }

        var json = JsonSerializer.Serialize(points, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(outputPath, json);

        Log.Info("Backup", $"Exported {points.Count} points to {outputPath}");
        return points.Count;
    }

    /// <summary>
    /// Rebuilds the Qdrant collection from a JSON backup file.
    /// This re-inserts all points without re-calling Ollama.
    /// </summary>
    public async Task<int> RestoreFromJsonAsync(string inputPath)
    {
        Log.Info("Backup", $"Restoring points from: {inputPath}");

        if (!File.Exists(inputPath))
        {
            Log.Error("Backup", $"Backup file not found: {inputPath}");
            return 0;
        }

        var json = await File.ReadAllTextAsync(inputPath);
        var points = JsonSerializer.Deserialize<List<ExportedPoint>>(json);

        if (points is null || points.Count == 0)
        {
            Log.Warning("Backup", "No points found in backup file.");
            return 0;
        }

        // Ensure the collection exists
        await _qdrant.EnsureCollectionExistsAsync();

        var count = 0;
        foreach (var point in points)
        {
            try
            {
                var chunk = new CodeChunk
                {
                    Id = point.Id,
                    Project = point.Project,
                    FilePath = point.File,
                    Namespace = point.Namespace,
                    SymbolType = point.Type,
                    SymbolName = point.Symbol,
                    Content = point.Content
                };

                await _qdrant.InsertAsync(chunk, point.Vector);
                count++;
            }
            catch (Exception ex)
            {
                Log.Error("Backup", $"Failed to restore point {point.Id}: {ex.Message}");
            }
        }

        Log.Info("Backup", $"Restored {count}/{points.Count} points from backup.");
        return count;
    }

    /// <summary>
    /// Rebuilds the Qdrant collection from the local embedding cache.
    /// This is the fastest recovery path — no Ollama calls needed.
    /// </summary>
    public async Task<int> RestoreFromCacheAsync(EmbeddingCacheService cache)
    {
        Log.Info("Backup", "Rebuilding Qdrant collection from embedding cache...");

        var entries = cache.GetAll();

        if (entries.Count == 0)
        {
            Log.Warning("Backup", "No cached entries found. Nothing to restore.");
            return 0;
        }

        // Ensure the collection exists
        await _qdrant.EnsureCollectionExistsAsync();

        var count = 0;
        foreach (var entry in entries)
        {
            try
            {
                var chunk = new CodeChunk
                {
                    Project = ExtractProject(entry.SourceFile),
                    FilePath = entry.SourceFile,
                    SymbolType = entry.SymbolType,
                    SymbolName = entry.SymbolName,
                    Content = "" // We don't cache the full content, only the embedding
                };

                // Compute deterministic ID from what we have
                chunk.ComputeDeterministicId();

                await _qdrant.InsertAsync(chunk, entry.Embedding);
                count++;
            }
            catch (Exception ex)
            {
                Log.Error("Backup", $"Failed to restore cached entry: {ex.Message}");
            }
        }

        Log.Info("Backup", $"Restored {count}/{entries.Count} points from cache.");
        return count;
    }

    private static string ExtractProject(string filePath)
    {
        // Try to extract the project name from the file path using span
        var span = filePath.AsSpan();
        var sep1 = Path.DirectorySeparatorChar;
        var sep2 = Path.AltDirectorySeparatorChar;

        for (var i = 0; i < span.Length; i++)
        {
            if (span[i] == sep1 || span[i] == sep2)
            {
                return span[..i].ToString();
            }
        }

        return span.Length > 0 ? span.ToString() : "unknown";
    }
}

/// <summary>
/// Represents a point exported from Qdrant in a portable format.
/// </summary>
public class ExportedPoint
{
    public string Id { get; set; } = "";
    public float[] Vector { get; set; } = [];
    public string Project { get; set; } = "";
    public string File { get; set; } = "";
    public string Type { get; set; } = "";
    public string Symbol { get; set; } = "";
    public string Namespace { get; set; } = "";
    public string Content { get; set; } = "";
}