using Rag.Indexer.Services;
using Rag.Indexer.Services.Configuration;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Aspire service defaults (OpenTelemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Controllers
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// CORS for the Angular web UI (localhost dev server)
builder.Services.AddCors(options =>
{
    options.AddPolicy("rag-web", policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .SetIsOriginAllowed(_ => builder.Environment.IsDevelopment());
    });
});

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
builder.Services.AddSingleton<SearchService>();
builder.Services.AddTransient<IndexingService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseCors("rag-web");
// Only enforce HTTPS redirect outside Development. The web UI is configured to
// talk to plain http://localhost:5004, and forcing a 307 to https:// breaks the
// progress EventSource in a browser (cross-origin http->https redirect is not
// followed), making the progress bar/status appear dead for a background index.
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseAuthorization();
app.MapControllers();
app.MapDefaultEndpoints();

app.Run();