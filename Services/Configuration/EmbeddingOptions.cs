namespace Rag.Indexer.Services.Configuration;

public class EmbeddingOptions
{
    public const string SectionName = "Embedding";

    public string Provider { get; set; } = "ollama";
    public OllamaOptions Ollama { get; set; } = new();
    public AzureOpenAiOptions AzureOpenAi { get; set; } = new();
}

public class OllamaOptions
{
    public const string SectionName = "Ollama";

    public string BaseUrl { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "mxbai-embed-large";
}

public class AzureOpenAiOptions
{
    public const string SectionName = "AzureOpenAi";

    public string Endpoint { get; set; } = "";
    public string Key { get; set; } = "";
    public string DeploymentName { get; set; } = "text-embedding-ada-002";
}
