using System.Text.Json.Serialization;

namespace Rag.Indexer.Models;

public record EmbeddingCacheEntry
{
    [JsonPropertyName("content_hash")]
    public string ContentHash { get; init; } = "";

    [JsonPropertyName("embedding")]
    public float[] Embedding { get; init; } = [];

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("source_file")]
    public string SourceFile { get; init; } = "";

    [JsonPropertyName("symbol_type")]
    public string SymbolType { get; init; } = "";

    [JsonPropertyName("symbol_name")]
    public string SymbolName { get; init; } = "";
}