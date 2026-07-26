using System.Net.Http.Json;
using UEFA.Rag.Indexer.Services.Configuration;

namespace UEFA.Rag.Indexer.Services;

public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _http;
    private readonly OllamaOptions _options;

    public OllamaEmbeddingService(OllamaOptions options)
    {
        _http = new HttpClient();
        _options = options;
    }

    public async Task<float[]> CreateAsync(string text)
    {
        var response = await _http.PostAsJsonAsync(
            $"{_options.BaseUrl}/api/embed",
            new
            {
                model = _options.Model,
                input = text
            });

        var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>();

        if (result?.Embeddings is not { Length: > 0 })
        {
            throw new InvalidOperationException(
                $"Ollama returned empty embeddings for input of length {text.Length}. " +
                $"Check that the model '{_options.Model}' is available and the input is not empty.");
        }

        return result.Embeddings[0];
    }

    private class EmbeddingResponse
    {
        public float[][] Embeddings { get; set; } = [];
    }
}