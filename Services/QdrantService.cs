using Qdrant.Client;
using Qdrant.Client.Grpc;
using UEFA.Rag.Indexer.Models;

namespace UEFA.Rag.Indexer.Services;

public class QdrantService
{
    private readonly QdrantClient _client =
        new("localhost",6334);


    private const string CollectionName = "uefa_code";

    public async Task EnsureCollectionExistsAsync()
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

            Console.WriteLine($"Created collection '{CollectionName}'.");
        }
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

    public async Task DeleteCollectionAsync()
    {
        await _client.DeleteCollectionAsync(CollectionName);
        Console.WriteLine($"Collection '{CollectionName}' deleted successfully.");
    }

    public async Task ListCollectionsAsync()
    {
        var collections = await _client.ListCollectionsAsync();

        Console.WriteLine("Qdrant Collections:");
        Console.WriteLine("-------------------");

        if (collections.Count == 0)
        {
            Console.WriteLine("(no collections found)");
            return;
        }

        foreach (var name in collections)
        {
            Console.WriteLine($"  - {name}");
        }
    }
}
