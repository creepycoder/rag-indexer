using System.Net.Http.Json;
using System.Text.Json;
using Rag.Indexer.Services.Configuration;

namespace Rag.Indexer.Services;

public class OllamaEmbeddingService : IEmbeddingService
{
    private static readonly LogStream Log = LogStream.Instance;

    private readonly HttpClient _http;
    private readonly OllamaOptions _options;

    public OllamaEmbeddingService(OllamaOptions options)
    {
        _http = new HttpClient();
        _options = options;
    }

    public async Task<float[]> CreateAsync(string text)
    {
        HttpResponseMessage? response = null;
        try
        {
            response = await _http.PostAsJsonAsync(
                $"{_options.BaseUrl}/api/embed",
                new
                {
                    model = _options.Model,
                    input = text
                });

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<EmbeddingResponse>();

            if (result?.Embeddings is not { Length: > 0 })
            {
                throw new InvalidOperationException(
                    $"Ollama returned empty embeddings for input of length {text.Length}. " +
                    $"Check that the model '{_options.Model}' is available and the input is not empty.");
            }

            return result.Embeddings[0];
        }
        catch (HttpRequestException ex)
        {
            Log.Error("Ollama", $"Cannot connect to Ollama at {_options.BaseUrl}. Ensure Ollama is running.", ex.ToString());
            throw;
        }
        catch (TaskCanceledException ex)
        {
            Log.Error("Ollama", $"Connection to Ollama at {_options.BaseUrl} timed out.", ex.ToString());
            throw;
        }
        catch (JsonException ex)
        {
            Log.Error("Ollama", $"Invalid response from Ollama at {_options.BaseUrl}.", ex.ToString());
            throw;
        }
    }

    private class EmbeddingResponse
    {
        public float[][] Embeddings { get; set; } = [];
    }
}
