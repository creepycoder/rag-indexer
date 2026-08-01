using System.ComponentModel;
using System.Text;
using ModelContextProtocol.Server;
using Rag.Indexer.Services;

namespace Rag.Indexer.Mcp.Tools;

[McpServerToolType]
public sealed class RagTools
{
    private readonly IEmbeddingService _embedding;

    public RagTools(IEmbeddingService embedding)
    {
        _embedding = embedding;
    }

    [McpServerTool]
    [Description("Search indexed code for relevant context chunks. Returns code snippets matching the query.")]
    public async Task<string> GetContext(
        [Description("The search query to find relevant code for")] string query,
        [Description("Maximum number of results to return (default 5)")] int limit = 5)
    {
        var vector = await _embedding.CreateAsync(query);

        var qdrant = QdrantConnection.CreateClient();

        var results = await qdrant.SearchAsync(
            collectionName: "uefa_code",
            vector: vector,
            limit: (ulong)Math.Max(1, limit));

        if (results.Count == 0)
            return "No relevant code found.";

        var sb = new StringBuilder();

        foreach (var r in results)
        {
            var file = r.Payload.TryGetValue("file", out var f) ? f.StringValue : "";
            var type = r.Payload.TryGetValue("type", out var t) ? t.StringValue : "";
            var symbol = r.Payload.TryGetValue("symbol", out var s) ? s.StringValue : "";
            var ns = r.Payload.TryGetValue("namespace", out var nsVal) ? nsVal.StringValue : "";
            var content = r.Payload.TryGetValue("content", out var c) ? c.StringValue : "";

            sb.AppendLine($"--- [{type}] {symbol} ({file}) score={r.Score:F3} ---");
            if (!string.IsNullOrEmpty(ns))
                sb.AppendLine($"namespace {ns}");
            sb.AppendLine(content);
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
