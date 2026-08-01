namespace Rag.Indexer.Api.Models;

public readonly record struct ScanRequest
{
    public string RepositoryPath { get; init; }

    public ScanRequest()
    {
        RepositoryPath = "";
    }
}
