using System.Net.Http.Json;

namespace UEFA.Rag.Indexer.Services;

public class EmbeddingService
{
    private readonly HttpClient _http = new();


    public async Task<float[]> CreateAsync(string text)
    {
        var response = await _http.PostAsJsonAsync(
            "http://localhost:11434/api/embed",
            new
            {
                model = "mxbai-embed-large",
                input = text
            });


        var result =
            await response.Content
                .ReadFromJsonAsync<EmbeddingResponse>();

        return result!.Embeddings[0];
    }


    private class EmbeddingResponse
    {
        public float[][] Embeddings { get; set; } = [];
    }
}