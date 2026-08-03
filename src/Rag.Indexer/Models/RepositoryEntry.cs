using System.Text.Json.Serialization;

namespace Rag.Indexer.Models;

/// <summary>
/// A single entry in the global repository registry: the root folder of a
/// repository that has been indexed, plus the metadata captured at index time.
/// </summary>
public sealed class RepositoryEntry
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("project")]
    public string Project { get; set; } = "";

    [JsonPropertyName("last_indexed")]
    public DateTime LastIndexed { get; set; }

    [JsonPropertyName("file_count")]
    public int FileCount { get; set; }
}