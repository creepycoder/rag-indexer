using System.Text.Json.Serialization;

namespace Rag.Indexer.Models;

public class EmbeddingCacheEntry
{
    [JsonPropertyName("content_hash")]
    public string ContentHash { get; set; } = "";

    [JsonPropertyName("embedding")]
    public float[] Embedding { get; set; } = [];

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("source_file")]
    public string SourceFile { get; set; } = "";

    [JsonPropertyName("symbol_type")]
    public string SymbolType { get; set; } = "";

    [JsonPropertyName("symbol_name")]
    public string SymbolName { get; set; } = "";
}