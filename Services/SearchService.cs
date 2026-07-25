using Qdrant.Client;
using System.Net.Http.Json;

namespace UEFA.Rag.Indexer.Services;

public class SearchService
{
    private readonly HttpClient _http;
    private readonly QdrantClient _qdrant;

    public SearchService()
    {
        _http = new HttpClient();
        _qdrant = new QdrantClient("localhost", 6334);
    }

    public async Task SearchAsync(string query)
    {
        var response = await _http.PostAsJsonAsync(
            "http://localhost:11434/api/embed",
            new
            {
                model = "mxbai-embed-large",
                input = query
            });

        var embedding =
            await response.Content.ReadFromJsonAsync<EmbeddingResponse>();

        var vector = embedding!.Embeddings[0];

        var results = await _qdrant.SearchAsync(
            collectionName: "uefa_code",
            vector: vector,
            limit: 5);

        foreach (var result in results)
        {
            Console.WriteLine($"Score: {result.Score}");

            foreach (var payload in result.Payload)
            {
                Console.WriteLine($"{payload.Key}: {payload.Value}");
            }

            Console.WriteLine("----------------------");
        }
    }


    private class EmbeddingResponse
    {
        public float[][] Embeddings { get; set; } = [];
    }
}