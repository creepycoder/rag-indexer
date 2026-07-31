namespace Rag.Indexer.Api.Models;

public record ContextResult
{
    public List<SearchResult> Results { get; init; } = [];
    public string? AggregatedContext { get; init; }
}