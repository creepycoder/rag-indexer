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
        var qdrantHost = Environment.GetEnvironmentVariable("QDRANT_HOST") ?? "localhost";
        var portStr = Environment.GetEnvironmentVariable("QDRANT_PORT") ?? "6334";
        var port = int.TryParse(portStr, out var p) ? p : 6334;
        _qdrant = new QdrantClient(qdrantHost, port);
    }

    public async Task SearchAsync(string query)
    {
        var ollamaBaseUrl = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL") ?? "http://localhost:11434";
        var response = await _http.PostAsJsonAsync(
            $"{ollamaBaseUrl}/api/embed",
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