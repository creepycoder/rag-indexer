using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

LoadDotEnv(Path.Combine(builder.AppHostDirectory, ".env"), builder.Configuration);

var ollamaBaseUrl = builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
var ollamaModel = builder.Configuration["Ollama:Model"] ?? "mxbai-embed-large";

var ollama = builder.AddOllamaLocal("ollama");

var qdrantApiKey = builder.AddParameter(
    "qdrant-api-key",
    builder.Configuration["Qdrant:ApiKey"] ?? "",
    secret: true);

// Pin Qdrant to the default host ports (REST 6333, gRPC 6334) so standalone
// clients that connect to localhost:6334 (e.g. the MCP server) work without
// knowing Aspire's ephemeral container port mapping.
var qdrant = builder.AddQdrant("qdrant", qdrantApiKey, grpcPort: 6334, httpPort: 6333)
    .WithDataVolume();

var api = builder.AddProject<Projects.Rag_Indexer_Api>("rag-api")
    .WaitFor(qdrant)
    .WaitFor(ollama)
    .WithReference(qdrant)
    .WithReference(ollama)
    .WithEnvironment("OLLAMA_BASE_URL", ollamaBaseUrl)
    .WithEnvironment("OLLAMA_MODEL", ollamaModel)
    .WithEnvironment("Embedding__Ollama__BaseUrl", ollamaBaseUrl)
    .WithEnvironment("Embedding__Ollama__Model", ollamaModel);

var mcp = builder.AddProject<Projects.Rag_Indexer_Mcp>("rag-mcp")
    .WaitFor(qdrant)
    .WaitFor(ollama)
    .WithReference(qdrant)
    .WithReference(ollama)
    .WithArgs("--transport=http")
    .WithEnvironment("OLLAMA_BASE_URL", ollamaBaseUrl)
    .WithEnvironment("OLLAMA_MODEL", ollamaModel);

var worker = builder.AddProject<Projects.Rag_Indexer_Worker>("rag-indexer")
    .WaitFor(qdrant)
    .WaitFor(ollama)
    .WithReference(qdrant)
    .WithReference(ollama)
    .WithEnvironment("Embedding__Ollama__BaseUrl", ollamaBaseUrl)
    .WithEnvironment("Embedding__Ollama__Model", ollamaModel);

var repositories = builder.Configuration.GetSection("Indexing:Repositories").Get<string[]>() ?? [];
for (var i = 0; i < repositories.Length; i++)
{
    worker.WithEnvironment($"Indexing__Repositories__{i}", repositories[i]);
    api.WithEnvironment($"Indexing__Repositories__{i}", repositories[i]);
}

var web = builder.AddJavaScriptApp("rag-web", "../Rag.Indexer.Web", "start")
    .WaitFor(api)
    .WithReference(api)
    .WithHttpEndpoint(port: 4200, targetPort: 4200, isProxied: false);

builder.Build().Run();

static void LoadDotEnv(string path, ConfigurationManager configuration)
{
    if (!File.Exists(path))
    {
        return;
    }

    var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    foreach (var rawLine in File.ReadLines(path))
    {
        var line = rawLine.Trim();
        if (line.Length == 0 || line.StartsWith('#'))
        {
            continue;
        }

        if (line.StartsWith("export ", StringComparison.OrdinalIgnoreCase))
        {
            line = line["export ".Length..].TrimStart();
        }

        var separator = line.IndexOf('=');
        if (separator <= 0)
        {
            continue;
        }

        var key = line[..separator].Trim();
        var value = line[(separator + 1)..].Trim();

        if (value.Length >= 2
            && ((value.StartsWith('"') && value.EndsWith('"'))
                || (value.StartsWith('\'') && value.EndsWith('\''))))
        {
            value = value[1..^1];
        }

        values[key] = value;
    }

    configuration.AddInMemoryCollection(values);
}
