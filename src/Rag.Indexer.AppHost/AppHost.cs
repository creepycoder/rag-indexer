using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

LoadDotEnv(Path.Combine(builder.AppHostDirectory, ".env"), builder.Configuration);

var ollamaBaseUrl = builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
var ollamaModel = builder.Configuration["Ollama:Model"] ?? "mxbai-embed-large";

var qdrantApiKey = builder.AddParameter(
    "qdrant-api-key",
    builder.Configuration["Qdrant:ApiKey"] ?? "",
    secret: true);

var qdrant = builder.AddQdrant("qdrant", qdrantApiKey)
    .WithDataVolume();

var api = builder.AddProject<Projects.Rag_Indexer_Api>("rag-api")
    .WaitFor(qdrant)
    .WithReference(qdrant)
    .WithEnvironment("OLLAMA_BASE_URL", ollamaBaseUrl)
    .WithEnvironment("OLLAMA_MODEL", ollamaModel)
    .WithEnvironment("Embedding__Ollama__BaseUrl", ollamaBaseUrl)
    .WithEnvironment("Embedding__Ollama__Model", ollamaModel);

var mcp = builder.AddProject<Projects.Rag_Indexer_Mcp>("rag-mcp")
    .WaitFor(qdrant)
    .WithReference(qdrant)
    .WithArgs("--transport=http")
    .WithEnvironment("OLLAMA_BASE_URL", ollamaBaseUrl)
    .WithEnvironment("OLLAMA_MODEL", ollamaModel);

var worker = builder.AddProject<Projects.Rag_Indexer_Worker>("rag-indexer")
    .WaitFor(qdrant)
    .WithReference(qdrant)
    .WithEnvironment("Embedding__Ollama__BaseUrl", ollamaBaseUrl)
    .WithEnvironment("Embedding__Ollama__Model", ollamaModel);

var repositories = builder.Configuration.GetSection("Indexing:Repositories").Get<string[]>() ?? [];
for (var i = 0; i < repositories.Length; i++)
{
    worker.WithEnvironment($"Indexing__Repositories__{i}", repositories[i]);
}

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
