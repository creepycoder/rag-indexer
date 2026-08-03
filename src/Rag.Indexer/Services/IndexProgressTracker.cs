using Rag.Indexer.Models;

namespace Rag.Indexer.Services;

/// <summary>
/// Singleton that tracks the overall status and incremental progress of the
/// current indexing run. The worker reports into it as it processes files and
/// chunks; the API exposes a snapshot so the UI can render a live progress bar.
/// </summary>
public class IndexProgressTracker
{
    private static readonly Lazy<IndexProgressTracker> _instance = new(() => new IndexProgressTracker());
    public static IndexProgressTracker Instance => _instance.Value;

    private readonly object _lock = new();
    private readonly List<string> _errors = [];
    private IndexProgress _state = new();
    private int _filesDone;
    private int _filesToIndex;
    private int _filesToDelete;
    private int _totalChunks;

    public bool IsRunning
    {
        get { lock (_lock) return _state.IsRunning; }
    }

    public void Begin(string rootFolder)
    {
        lock (_lock)
        {
            _errors.Clear();
            _filesToIndex = 0;
            _filesToDelete = 0;
            _filesDone = 0;
            _totalChunks = 0;
            _state = new IndexProgress
            {
                IsRunning = true,
                Phase = "starting",
                RootFolder = rootFolder,
                StartedAt = DateTime.UtcNow
            };
        }
    }

    public void SetPhase(string phase)
    {
        lock (_lock) _state.Phase = phase;
    }

    /// <summary>
    /// Announces how much work this run will do, including the number of chunks
    /// that will be embedded (known after a pre-flight parse). Must be called
    /// before any per-file reporting so percentages have a denominator.
    /// </summary>
    public void StartWork(int filesToIndex, int filesToDelete, int totalChunks)
    {
        lock (_lock)
        {
            _filesToIndex = filesToIndex;
            _filesToDelete = filesToDelete;
            _totalChunks = totalChunks;
            _state.FilesToIndex = filesToIndex;
            _state.FilesToDelete = filesToDelete;
            _state.TotalChunks = totalChunks;
        }
    }

    public void SetCurrentFile(string file)
    {
        lock (_lock)
        {
            _state.CurrentFile = file;
            _state.Phase = string.Equals(_state.Phase, "deleting", System.StringComparison.OrdinalIgnoreCase)
                ? _state.Phase
                : "indexing";
        }
    }

    public void FileDeleted()
    {
        lock (_lock)
        {
            _state.FilesDeleted++;
            _filesDone++;
        }
    }

    public void FileIndexed()
    {
        lock (_lock)
        {
            _state.FilesIndexed++;
            _filesDone++;
        }
    }

    public void AddChunk()
    {
        lock (_lock) _state.ChunksIndexed++;
    }

    public void HasError(string message)
    {
        lock (_lock)
        {
            if (_errors.Count < 100)
                _errors.Add(message);
        }
    }

    public void End(string phase, string summary)
    {
        lock (_lock)
        {
            _state.Phase = phase;
            _state.Summary = summary;
            _state.IsRunning = false;
            _state.CompletedAt = DateTime.UtcNow;
            _state.Errors = _errors.ToArray();
            RefreshCountersLocked();
        }
    }

    public IndexProgress Snapshot()
    {
        lock (_lock)
        {
            RefreshCountersLocked();
            return new IndexProgress
            {
                IsRunning = _state.IsRunning,
                Phase = _state.Phase,
                RootFolder = _state.RootFolder,
                FilesToIndex = _state.FilesToIndex,
                FilesToDelete = _state.FilesToDelete,
                FilesIndexed = _state.FilesIndexed,
                FilesDeleted = _state.FilesDeleted,
                ChunksIndexed = _state.ChunksIndexed,
                TotalChunks = _state.TotalChunks,
                CurrentFile = _state.CurrentFile,
                TotalFiles = _state.TotalFiles,
                FilesDone = _state.FilesDone,
                RemainingFiles = _state.RemainingFiles,
                PercentComplete = _state.PercentComplete,
                ElapsedSeconds = _state.ElapsedSeconds,
                EstimatedRemainingSeconds = _state.EstimatedRemainingSeconds,
                Errors = _state.Errors,
                StartedAt = _state.StartedAt,
                CompletedAt = _state.CompletedAt,
                Summary = _state.Summary
            };
        }
    }

    private void RefreshCountersLocked()
    {
        _state.TotalFiles = _filesToIndex + _filesToDelete;
        _state.FilesDone = _filesDone;
        _state.RemainingFiles = Math.Max(0, _state.TotalFiles - _state.FilesDone);

        // Percentage is based on chunks (the dominant cost: embedding) when the
        // pre-flight count is known, and falls back to file counts otherwise.
        var denominator = _totalChunks > 0 ? _totalChunks : _state.TotalFiles;
        var numerator = _totalChunks > 0 ? _state.ChunksIndexed : _state.FilesDone;
        _state.PercentComplete = denominator > 0
            ? Math.Clamp((int)(numerator * 100.0 / denominator), 0, 100)
            : 0;

        var started = _state.StartedAt;
        _state.ElapsedSeconds = started.HasValue
            ? Math.Max(0, (int)(DateTime.UtcNow - started.Value).TotalSeconds)
            : 0;

        // Estimate remaining time from measured chunk throughput.
        _state.EstimatedRemainingSeconds = null;
        if (_totalChunks > 0 && started.HasValue && _state.ChunksIndexed > 0)
        {
            var elapsed = (DateTime.UtcNow - started.Value).TotalSeconds;
            var rate = _state.ChunksIndexed / Math.Max(1.0, elapsed);
            var remaining = _totalChunks - _state.ChunksIndexed;
            if (remaining > 0)
                _state.EstimatedRemainingSeconds = (int)(remaining / rate);
            else
                _state.EstimatedRemainingSeconds = 0;
        }
    }
}