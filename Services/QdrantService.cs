using Qdrant.Client;
using Qdrant.Client.Grpc;
using UEFA.Rag.Indexer.Models;

namespace UEFA.Rag.Indexer.Services;

public class QdrantService
{
    private readonly QdrantClient _client =
        new("localhost",6334);


    public async Task InsertAsync(
        CodeChunk chunk,
        float[] vector)
    {
        await _client.UpsertAsync(
            "uefa_code",
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
}