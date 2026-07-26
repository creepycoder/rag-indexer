using Qdrant.Client;
using Qdrant.Client.Grpc;
using Rag.Indexer.Models;

namespace Rag.Indexer.Services;

public class QdrantService
{
    private static readonly LogStream Log = LogStream.Instance;

    private readonly QdrantClient _client;
    private readonly string _host;
    private readonly int _port;

    public QdrantService()
    {
        _host = Environment.GetEnvironmentVariable("QDRANT_HOST") ?? "localhost";
        var portStr = Environment.GetEnvironmentVariable("QDRANT_PORT") ?? "6334";
        _port = int.TryParse(portStr, out var p) ? p : 6334;
        _client = new QdrantClient(_host, _port);
    }


    private const string CollectionName = "uefa_code";

    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            await _client.ListCollectionsAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> EnsureCollectionExistsAsync()
    {
        try
        {
            var collections = await _client.ListCollectionsAsync();

            if (!collections.Contains(CollectionName))
            {
                await _client.CreateCollectionAsync(
                    CollectionName,
                    new VectorParams
                    {
                        Size = 1024,
                        Distance = Distance.Cosine
                    });

                Log.Info("Qdrant", $"Created collection '{CollectionName}'.");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Log.Error("Qdrant", $"Cannot connect to Qdrant at {_host}:{_port}. Ensure Qdrant is running.", ex.ToString());
            return false;
        }
    }

    public async Task InsertAsync(
        CodeChunk chunk,
        float[] vector)
    {
        try
        {
            await _client.UpsertAsync(
                CollectionName,
                new[]
                {
                    new PointStruct
                    {
                        Id = new PointId
                        {
                            Uuid = chunk.Id
                        },
                        Vectors = vector,
                        Payload =
                        {
                            ["project"] = chunk.Project,
                            ["file"] = chunk.FilePath,
                            ["type"] = chunk.SymbolType,
                            ["symbol"] = chunk.SymbolName,
                            ["namespace"] = chunk.Namespace,
                            ["content"] = chunk.Content
                        }
                    }
                });
        }
        catch (Exception ex)
        {
            Log.Error("Qdrant", $"Failed to insert chunk '{chunk.SymbolName}' for {chunk.FilePath}", ex.ToString());
        }
    }

    public async Task DeleteByFilePathAsync(string filePath)
    {
        try
        {
            await _client.DeleteAsync(
                CollectionName,
                new Filter
                {
                    Must =
                    {
                        new Condition
                        {
                            Field = new FieldCondition
                            {
                                Key = "file",
                                Match = new Match
                                {
                                    Text = filePath
                                }
                            }
                        }
                    }
                });

            Log.Info("Qdrant", $"Deleted points for: {filePath}");
        }
        catch (Exception ex)
        {
            Log.Error("Qdrant", $"Failed to delete points for: {filePath}", ex.ToString());
        }
    }

    public async Task DeleteCollectionAsync()
    {
        try
        {
            var collections = await _client.ListCollectionsAsync();

            if (!collections.Contains(CollectionName))
            {
                Log.Warning("Qdrant", $"Collection '{CollectionName}' does not exist. Nothing to delete.");
                return;
            }

            await _client.DeleteCollectionAsync(CollectionName);
            Log.Info("Qdrant", $"Collection '{CollectionName}' deleted successfully.");
        }
        catch (Exception ex)
        {
            Log.Error("Qdrant", $"Failed to delete collection '{CollectionName}'", ex.ToString());
        }
    }

    public async Task<List<string>> ListCollectionsAsync()
    {
        try
        {
            var collections = await _client.ListCollectionsAsync();
            return [.. collections];
        }
        catch (Exception ex)
        {
            Log.Error("Qdrant", "Failed to list collections", ex.ToString());
            return [];
        }
    }

    public async Task<CollectionInfo?> GetCollectionInfoAsync(string name)
    {
        try
        {
            return await _client.GetCollectionInfoAsync(name);
        }
        catch (Exception ex)
        {
            Log.Error("Qdrant", $"Failed to get collection info for '{name}'", ex.ToString());
            return null;
        }
    }

    /// <summary>
    /// Retrieves all points from the collection along with their payloads and vectors.
    /// Used by BackupService to export the entire collection to JSON.
    /// </summary>
    public async Task<List<ExportedPoint>> GetAllPointsAsync()
    {
        var points = new List<ExportedPoint>();

        try
        {
            // Scroll through the collection in batches
            PointId? offset = null;
            const uint limit = 100;

            while (true)
            {
                var result = await _client.ScrollAsync(
                    CollectionName,
                    limit: limit,
                    offset: offset,
                    payloadSelector: true,
                    vectorsSelector: new[] { "" } // Select the default vector
                );

                if (result.Result is null || result.Result.Count == 0)
                    break;

                foreach (var point in result.Result)
                {
                    var payload = point.Payload;
#pragma warning disable CS0612 // VectorOutput.Data is obsolete but still the correct API for this version
                    var vector = point.Vectors?.Vector?.Data?.ToArray() ?? [];
#pragma warning restore CS0612

                    // We always use UUID-based IDs, so extract from Uuid field
                    var pointId = point.Id?.Uuid ?? "";

                    points.Add(new ExportedPoint
                    {
                        Id = pointId,
                        Vector = vector,
                        Project = payload.TryGetValue("project", out var p) ? p.StringValue : "",
                        File = payload.TryGetValue("file", out var f) ? f.StringValue : "",
                        Type = payload.TryGetValue("type", out var t) ? t.StringValue : "",
                        Symbol = payload.TryGetValue("symbol", out var s) ? s.StringValue : "",
                        Namespace = payload.TryGetValue("namespace", out var ns) ? ns.StringValue : "",
                        Content = payload.TryGetValue("content", out var c) ? c.StringValue : ""
                    });
                }

                if (result.NextPageOffset is null)
                    break;

                offset = result.NextPageOffset;
            }
        }
        catch (Exception ex)
        {
            Log.Error("Qdrant", $"Failed to scroll all points: {ex.Message}");
        }

        Log.Info("Qdrant", $"Retrieved {points.Count} total points from collection.");
        return points;
    }
}
