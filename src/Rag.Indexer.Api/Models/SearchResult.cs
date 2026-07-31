namespace Rag.Indexer.Api.Models;

public record SearchResult
{
    public string Id { get; init; } = "";
    public string Project { get; init; } = "";
    public string FilePath { get; init; } = "";
    public string SymbolType { get; init; } = "";
    public string SymbolName { get; init; } = "";
    public string Namespace { get; init; } = "";
    public string Content { get; init; } = "";
    public double Score { get; init; }
}