using Microsoft.AspNetCore.Mvc;
using Qdrant.Client.Grpc;
using Rag.Indexer.Api.Models;
using Rag.Indexer.Services;
using Rag.Indexer.Models;

namespace Rag.Indexer.Api.Controllers;

[ApiController]
[Route("api")]
public class StatusController : ControllerBase
{
    private readonly QdrantService _qdrantService;

    public StatusController(QdrantService qdrantService)
    {
        _qdrantService = qdrantService;
    }

    /// <summary>
    /// GET /api/collections — Lists all collections in the vector store.
    /// </summary>
    [HttpGet("collections")]
    public async Task<IActionResult> Collections()
    {
        var collections = await _qdrantService.ListCollectionsAsync();
        return Ok(new { collections });
    }

    /// <summary>
    /// GET /api/collections/{name} — Detailed info for a single collection.
    /// </summary>
    [HttpGet("collections/{name}")]
    public async Task<IActionResult> CollectionInfo(string name)
    {
        var info = await _qdrantService.GetCollectionInfoAsync(name);
        if (info is null)
            return NotFound(new { error = $"Collection '{name}' not found." });

        return Ok(Map(name, info));
    }

    /// <summary>
    /// GET /api/logs — Recent entries from the in-memory log stream.
    /// </summary>
    [HttpGet("logs")]
    public IActionResult Logs([FromQuery] string? source = null, [FromQuery] int? limit = null)
    {
        var entries = LogStream.Instance.GetSnapshot();
        if (!string.IsNullOrWhiteSpace(source))
            entries = entries.Where(e => e.Source.Contains(source, StringComparison.OrdinalIgnoreCase)).ToList();
        if (limit is > 0)
            entries = entries.TakeLast(limit.Value).ToList();

        return Ok(new { entries = entries.Select(Map) });
    }

    private static CollectionInfoDto Map(string name, CollectionInfo info) => new()
    {
        Name = name,
        Status = info.Status.ToString(),
        OptimizerStatus = info.OptimizerStatus.ToString(),
        SegmentsCount = info.SegmentsCount,
        PointsCount = info.PointsCount,
        IndexedVectorsCount = info.IndexedVectorsCount,
        VectorSize = info.Config?.Params?.VectorsConfig?.Params?.Size ?? 0,
        Distance = info.Config?.Params?.VectorsConfig?.Params?.Distance.ToString() ?? ""
    };

    private static LogEntryDto Map(LogEntry entry) => new()
    {
        Timestamp = entry.Timestamp,
        Level = entry.Level.ToString().ToUpperInvariant(),
        Source = entry.Source,
        Message = entry.Message,
        Exception = entry.Exception
    };
}
