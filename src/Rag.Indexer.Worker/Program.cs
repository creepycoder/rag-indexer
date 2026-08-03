using Rag.Indexer.Services;
using Rag.Indexer.Services.Configuration;
using Rag.Indexer.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Aspire service defaults (OpenTelemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Configuration
var embeddingOptions = builder.Configuration
    .GetSection(EmbeddingOptions.SectionName)
    .Get<EmbeddingOptions>() ?? new EmbeddingOptions();
var registryPath = RepositoryRegistry.ResolveRegistryPath(builder.Configuration);

// Register services
builder.Services.AddSingleton(embeddingOptions);
builder.Services.AddSingleton(_ => new RepositoryRegistry(registryPath));
builder.Services.AddSingleton<IEmbeddingService>(sp =>
{
    var options = sp.GetRequiredService<EmbeddingOptions>();
    try
    {
        return options.Provider.ToLowerInvariant() switch
        {
            "azure" => new AzureOpenAiEmbeddingService(options.AzureOpenAi),
            _ => new OllamaEmbeddingService(options.Ollama)
        };
    }
    catch (InvalidOperationException)
    {
        return new OllamaEmbeddingService(options.Ollama);
    }
});
builder.Services.AddSingleton<QdrantService>();
builder.Services.AddSingleton<EmbeddingCacheService>();
builder.Services.AddTransient<IndexingService>();
builder.Services.AddHostedService<IndexerWorker>();

await builder.Build().RunAsync();
