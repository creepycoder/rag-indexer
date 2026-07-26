using Qdrant.Client;
using Qdrant.Client.Grpc;
using UEFA.Rag.Indexer.Models;

namespace UEFA.Rag.Indexer.Services;

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
}
