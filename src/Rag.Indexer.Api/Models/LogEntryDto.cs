namespace Rag.Indexer.Api.Models;

public record LogEntryDto
{
    public DateTimeOffset Timestamp { get; init; }
    public string Level { get; init; } = "";
    public string Source { get; init; } = "";
    public string Message { get; init; } = "";
    public string? Exception { get; init; }
}
