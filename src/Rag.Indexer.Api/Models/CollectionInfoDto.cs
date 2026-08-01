namespace Rag.Indexer.Api.Models;

public record CollectionInfoDto
{
    public string Name { get; init; } = "";
    public string Status { get; init; } = "";
    public string OptimizerStatus { get; init; } = "";
    public ulong SegmentsCount { get; init; }
    public ulong PointsCount { get; init; }
    public ulong IndexedVectorsCount { get; init; }
    public ulong VectorSize { get; init; }
    public string Distance { get; init; } = "";
}
