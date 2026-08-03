using System.Text.Json;
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
    /// GET /api/indexing/progress — Snapshot of the current indexing run.
    /// </summary>
    [HttpGet("indexing/progress")]
    public IActionResult IndexingProgress()
    {
        return Ok(IndexProgressTracker.Instance.Snapshot());
    }

    /// <summary>
    /// GET /api/indexing/progress/stream — Server-Sent Events stream that pushes
    /// progress updates the moment the tracker changes, so the UI renders a
    /// real-time progress bar without polling.
    /// </summary>
    [HttpGet("indexing/progress/stream")]
    public async Task IndexingProgressStream(CancellationToken ct)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";

        var tracker = IndexProgressTracker.Instance;
        string? lastSignature = null;

        while (!ct.IsCancellationRequested)
        {
            var snapshot = tracker.Snapshot();
            var signature = BuildSignature(snapshot);

            // Only push a new event when something the UI needs actually changed.
            // (ElapsedSeconds/id tick constantly, so they are excluded — otherwise
            //  we'd re-push the identical frame every 500ms forever.)
            if (signature != lastSignature)
            {
                var json = JsonSerializer.Serialize(snapshot);
                await Response.WriteAsync($"id: {DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}\n", ct);
                await Response.WriteAsync($"data: {json}\n\n", ct);
                lastSignature = signature;
            }
            else
            {
                // Comment frame keeps the connection alive through proxies/load balancers
                await Response.WriteAsync(": ping\n\n", ct);
            }

            await Response.Body.FlushAsync(ct);
            await Task.Delay(500, ct);
        }
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

    private static string BuildSignature(IndexProgress p) =>
        string.Join('|',
            p.IsRunning,
            p.Phase,
            p.RootFolder,
            p.FilesToIndex,
            p.FilesToDelete,
            p.FilesDeleted,
            p.ChunksIndexed,
            p.TotalChunks,
            p.FilesDone,
            p.TotalFiles,
            p.CurrentFile,
            p.PercentComplete,
            p.EstimatedRemainingSeconds,
            string.Join(';', p.Errors),
            p.Phase == "completed" || p.Phase == "failed" ? p.Summary : null);

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
