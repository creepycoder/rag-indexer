using System.Net.Http.Json;
using System.Text.Json.Serialization;
using UEFA.Rag.Indexer.Services.Configuration;

namespace UEFA.Rag.Indexer.Services;

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

    public async Task<float[]> CreateAsync(string text)
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