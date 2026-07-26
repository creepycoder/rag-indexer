using Qdrant.Client;
using System.Net.Http.Json;
using System.Text.Json;

namespace Rag.Indexer.Services;

public class SearchService
{
    private static readonly LogStream Log = LogStream.Instance;

    private readonly HttpClient _http;
    private readonly QdrantClient _qdrant;
    private readonly string _qdrantHost;
    private readonly int _qdrantPort;

    public SearchService()
    {
        _http = new HttpClient();
        _qdrantHost = Environment.GetEnvironmentVariable("QDRANT_HOST") ?? "localhost";
        var portStr = Environment.GetEnvironmentVariable("QDRANT_PORT") ?? "6334";
        _qdrantPort = int.TryParse(portStr, out var p) ? p : 6334;
        _qdrant = new QdrantClient(_qdrantHost, _qdrantPort);
    }

    public async Task SearchAsync(string query)
    {
        try
        {
            var ollamaBaseUrl = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL") ?? "http://localhost:11434";
            var response = await _http.PostAsJsonAsync(
                $"{ollamaBaseUrl}/api/embed",
                new
                {
                    model = "mxbai-embed-large",
                    input = query
                });

            response.EnsureSuccessStatusCode();

            var embedding =
                await response.Content.ReadFromJsonAsync<EmbeddingResponse>();

            if (embedding?.Embeddings is not { Length: > 0 })
            {
                Console.WriteLine("Search: Ollama returned empty embeddings.");
                return;
            }

            var vector = embedding.Embeddings[0];

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
        catch (HttpRequestException ex)
        {
            Log.Error("Search", $"Cannot connect to Ollama. Ensure Ollama is running.", ex.ToString());
            Console.WriteLine("Search failed: Cannot connect to Ollama.");
        }
        catch (TaskCanceledException ex)
        {
            Log.Error("Search", $"Connection to Ollama timed out.", ex.ToString());
            Console.WriteLine("Search failed: Connection timed out.");
        }
        catch (JsonException ex)
        {
            Log.Error("Search", $"Invalid response from Ollama.", ex.ToString());
            Console.WriteLine("Search failed: Invalid response from Ollama.");
        }
        catch (Exception ex)
        {
            Log.Error("Search", $"Search failed for query: {query}", ex.ToString());
            Console.WriteLine("Search failed: Unable to complete search.");
        }
    }


    private class EmbeddingResponse
    {
        public float[][] Embeddings { get; set; } = [];
    }
}
