using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Rag.Indexer.Services.Configuration;

namespace Rag.Indexer.Services;

public class AzureOpenAiEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _http;
    private readonly AzureOpenAiOptions _options;

    public AzureOpenAiEmbeddingService(AzureOpenAiOptions options)
    {
        if (string.IsNullOrEmpty(options.Endpoint) || string.IsNullOrEmpty(options.Key))
        {
            throw new InvalidOperationException(
                "Azure OpenAI configuration is missing. Configure 'Embedding:AzureOpenAi:Endpoint' and 'Embedding:AzureOpenAi:Key' in appsettings.json.");
        }

        _http = new HttpClient();
        _options = options;
    }

    private static readonly LogStream Log = LogStream.Instance;

    public async Task<float[]> CreateAsync(string text)
    {
        try
        {
            var url = $"{_options.Endpoint.TrimEnd('/')}/openai/deployments/{_options.DeploymentName}/embeddings?api-version=2024-02-01";

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("api-key", _options.Key);
            request.Content = JsonContent.Create(new { input = text });

            var response = await _http.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<AzureEmbeddingResponse>();
            return result!.Data[0].Embedding;
        }
        catch (HttpRequestException ex)
        {
            Log.Error("AzureOpenAI", $"Cannot connect to Azure OpenAI at {_options.Endpoint}.", ex.ToString());
            throw;
        }
        catch (TaskCanceledException ex)
        {
            Log.Error("AzureOpenAI", $"Connection to Azure OpenAI at {_options.Endpoint} timed out.", ex.ToString());
            throw;
        }
        catch (JsonException ex)
        {
            Log.Error("AzureOpenAI", $"Invalid response from Azure OpenAI at {_options.Endpoint}.", ex.ToString());
            throw;
        }
    }

    private class AzureEmbeddingResponse
    {
        [JsonPropertyName("data")]
        public AzureEmbeddingData[] Data { get; set; } = [];
    }

    private class AzureEmbeddingData
    {
        [JsonPropertyName("embedding")]
        public float[] Embedding { get; set; } = [];
    }
}
