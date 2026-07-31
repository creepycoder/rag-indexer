namespace Rag.Indexer.Api.Models;

public record ContextRequest
{
    public string Query { get; init; } = "";
    public int Limit { get; init; } = 5;
    public string? FileFilter { get; init; }
    public string? SymbolTypeFilter { get; init; }
}