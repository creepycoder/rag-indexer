using System.Text.Json.Serialization;

namespace Rag.Indexer.Models;

public class IndexState
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("files")]
    public Dictionary<string, FileState> Files { get; set; } = [];
}

public record FileState
{
    [JsonPropertyName("content_hash")]
    public string ContentHash { get; init; } = "";

    [JsonPropertyName("last_indexed")]
    public DateTime LastIndexed { get; init; }
}
