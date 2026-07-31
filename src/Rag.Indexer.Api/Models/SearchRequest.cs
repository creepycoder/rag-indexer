namespace Rag.Indexer.Api.Models;

public readonly record struct SearchRequest
{
    public string Query { get; init; }
    public int Limit { get; init; }

    public SearchRequest()
    {
        Query = "";
        Limit = 5;
    }
}