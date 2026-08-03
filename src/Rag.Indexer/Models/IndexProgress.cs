namespace Rag.Indexer.Models;

/// <summary>
/// A snapshot of the current (or most recent) indexing operation.
/// Serialized to clients so they can render a real-time progress bar.
/// </summary>
public class IndexProgress
{
    public bool IsRunning { get; set; }
    public string Phase { get; set; } = "idle";
    public string? RootFolder { get; set; }

    public int FilesToIndex { get; set; }
    public int FilesToDelete { get; set; }
    public int FilesIndexed { get; set; }
    public int FilesDeleted { get; set; }
    public int ChunksIndexed { get; set; }
    public int TotalChunks { get; set; }

    public string? CurrentFile { get; set; }

    public int TotalFiles { get; set; }
    public int FilesDone { get; set; }
    public int RemainingFiles { get; set; }
    public int PercentComplete { get; set; }

    public int ElapsedSeconds { get; set; }
    public int? EstimatedRemainingSeconds { get; set; }

    public IReadOnlyList<string> Errors { get; set; } = [];

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Summary { get; set; }
}