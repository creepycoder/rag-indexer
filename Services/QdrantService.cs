using Qdrant.Client;
using Qdrant.Client.Grpc;
using UEFA.Rag.Indexer.Models;

namespace UEFA.Rag.Indexer.Services;

public class QdrantService
{
    private static readonly LogStream Log = LogStream.Instance;

    private readonly QdrantClient _client =
        new("localhost",6334);


    private const string CollectionName = "uefa_code";

    /// <summary>
    /// Ensures the collection exists. Returns true if the collection was just created.
    /// </summary>
    public async Task<bool> EnsureCollectionExistsAsync()
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
            return true; // was just created — state is stale
        }

        return false; // already existed
    }

    public async Task InsertAsync(
        CodeChunk chunk,
        float[] vector)
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

    /// <summary>
    /// Deletes all points whose file path starts with the given prefix.
    /// Used to remove chunks from deleted or renamed files during delta indexing.
    /// </summary>
    public async Task DeleteByFilePathAsync(string filePath)
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

    public async Task DeleteCollectionAsync()
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

    public async Task ListCollectionsAsync()
    {
        var collections = await _client.ListCollectionsAsync();

        Log.Info("Qdrant", "Qdrant Collections:");
        Log.Info("Qdrant", "-------------------");

        if (collections.Count == 0)
        {
            Log.Info("Qdrant", "(no collections found)");
            return;
        }

        foreach (var name in collections)
        {
            Log.Info("Qdrant", $"  - {name}");
        }
    }
}
