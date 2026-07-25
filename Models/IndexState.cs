using System.Text.Json.Serialization;

namespace UEFA.Rag.Indexer.Models;

public class IndexState
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("files")]
    public Dictionary<string, FileState> Files { get; set; } = [];
}

public class FileState
{
    [JsonPropertyName("content_hash")]
    public string ContentHash { get; set; } = "";

    [JsonPropertyName("last_indexed")]
    public DateTime LastIndexed { get; set; }
}