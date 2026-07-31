using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Rag.Indexer.Services;

/// <summary>
/// Manages Qdrant collection snapshots for backup and restore.
/// Uses the Qdrant REST API (port 6333) for snapshot operations,
/// since the gRPC client doesn't expose snapshot methods directly.
/// </summary>
public class QdrantSnapshotService
{
    private static readonly LogStream Log = LogStream.Instance;

    private readonly HttpClient _http;
    private readonly string _restUrl;
    private readonly string _collectionName;

    public QdrantSnapshotService()
    {
        var host = Environment.GetEnvironmentVariable("QDRANT_HOST") ?? "localhost";
        var restPort = Environment.GetEnvironmentVariable("QDRANT_REST_PORT") ?? "6333";
        _restUrl = $"http://{host}:{restPort}";
        _collectionName = "uefa_code";
        _http = new HttpClient
        {
            BaseAddress = new Uri(_restUrl),
            Timeout = TimeSpan.FromMinutes(10) // Snapshots can take a while
        };
    }

    /// <summary>
    /// Creates a snapshot of the collection via the Qdrant REST API.
    /// Returns the snapshot file name on success, or null on failure.
    /// </summary>
    public async Task<string?> CreateSnapshotAsync()
    {
        try
        {
            Log.Info("QdrantSnapshot", $"Creating snapshot of collection '{_collectionName}'...");

            var response = await _http.PostAsync(
                $"/collections/{_collectionName}/snapshots", null);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<SnapshotResponse>();

            if (result?.Result?.Name is not null)
            {
                Log.Info("QdrantSnapshot", $"Snapshot created: {result.Result.Name}");
                return result.Result.Name;
            }

            Log.Error("QdrantSnapshot", "Snapshot response did not contain a name.");
            return null;
        }
        catch (Exception ex)
        {
            Log.Error("QdrantSnapshot", $"Failed to create snapshot: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Lists all snapshots for the collection.
    /// </summary>
    public async Task<List<SnapshotInfo>> ListSnapshotsAsync()
    {
        try
        {
            var response = await _http.GetAsync(
                $"/collections/{_collectionName}/snapshots");

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<SnapshotListResponse>();

            return result?.Result ?? [];
        }
        catch (Exception ex)
        {
            Log.Error("QdrantSnapshot", $"Failed to list snapshots: {ex.Message}");
            return [];
        }
    }

    /// <summary>
    /// Deletes a specific snapshot by name.
    /// </summary>
    public async Task<bool> DeleteSnapshotAsync(string snapshotName)
    {
        try
        {
            var response = await _http.DeleteAsync(
                $"/collections/{_collectionName}/snapshots/{snapshotName}");

            response.EnsureSuccessStatusCode();
            Log.Info("QdrantSnapshot", $"Snapshot deleted: {snapshotName}");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("QdrantSnapshot", $"Failed to delete snapshot '{snapshotName}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Gets the download URL for a snapshot file.
    /// </summary>
    public string GetSnapshotDownloadUrl(string snapshotName)
    {
        return $"{_restUrl}/collections/{_collectionName}/snapshots/{snapshotName}";
    }

    /// <summary>
    /// Downloads a snapshot file to the specified local path.
    /// </summary>
    public async Task<bool> DownloadSnapshotAsync(string snapshotName, string outputPath)
    {
        try
        {
            var url = GetSnapshotDownloadUrl(snapshotName);
            Log.Info("QdrantSnapshot", $"Downloading snapshot from {url}...");

            var response = await _http.GetAsync(url);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = File.Create(outputPath);
            await stream.CopyToAsync(fileStream);

            Log.Info("QdrantSnapshot", $"Snapshot downloaded to: {outputPath}");
            return true;
        }
        catch (Exception ex)
        {
            Log.Error("QdrantSnapshot", $"Failed to download snapshot: {ex.Message}");
            return false;
        }
    }

    private class SnapshotResponse
    {
        [JsonPropertyName("result")]
        public SnapshotResult? Result { get; set; }
    }

    private class SnapshotResult
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("creation_time")]
        public DateTime? CreationTime { get; set; }

        [JsonPropertyName("size")]
        public long? Size { get; set; }
    }

    private class SnapshotListResponse
    {
        [JsonPropertyName("result")]
        public List<SnapshotInfo>? Result { get; set; }
    }
}

public class SnapshotInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("creation_time")]
    public DateTime? CreationTime { get; set; }

    [JsonPropertyName("size")]
    public long? Size { get; set; }
}