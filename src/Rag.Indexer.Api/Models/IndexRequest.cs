namespace Rag.Indexer.Api.Models;

public readonly record struct IndexRequest
{
    public string RepositoryPath { get; init; }

    public IndexRequest()
    {
        RepositoryPath = "";
    }
}