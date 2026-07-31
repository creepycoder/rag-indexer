using Rag.Indexer.Services;
using Rag.Indexer.Services.Configuration;

var transport = args.FirstOrDefault(a => a.StartsWith("--transport="))?.Split('=')[1]
    ?? Environment.GetEnvironmentVariable("MCP_TRANSPORT")
    ?? "stdio";

// Embedding — reads from env, defaults to localhost
static void RegisterServices(IServiceCollection services)
{
    services.AddSingleton<IEmbeddingService>(sp =>
    {
        var opts = new OllamaOptions
        {
            BaseUrl = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL") ?? "http://localhost:11434",
            Model = Environment.GetEnvironmentVariable("OLLAMA_MODEL") ?? "mxbai-embed-large"
        };
        return new OllamaEmbeddingService(opts);
    });
}

if (transport.Equals("http", StringComparison.OrdinalIgnoreCase))
{
    var builder = WebApplication.CreateBuilder(args);

    // Prevent ASP.NET logs from polluting stdout
    builder.Logging.ClearProviders();

    RegisterServices(builder.Services);

    builder.Services.AddMcpServer().WithHttpTransport().WithToolsFromAssembly();

    var app = builder.Build();
    app.MapMcp();
    app.Run();
}
else
{
    // stdio transport: use a plain host so Kestrel never starts and does not
    // bind ports or interfere with the stdin/stdout MCP protocol.
    var builder = Host.CreateApplicationBuilder(args);

    // Send all logs to stderr — stdout is reserved for the MCP protocol.
    builder.Logging.ClearProviders();
    builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

    RegisterServices(builder.Services);

    builder.Services.AddMcpServer().WithStdioServerTransport().WithToolsFromAssembly();

    await builder.Build().RunAsync();
}
