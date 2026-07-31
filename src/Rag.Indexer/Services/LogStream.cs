using System.Collections.Concurrent;
using Rag.Indexer.Models;
using LogLevel = Rag.Indexer.Models.LogLevel;

namespace Rag.Indexer.Services;

/// <summary>
/// Singleton log stream that collects structured log entries
/// and allows consumers to subscribe for live updates.
/// </summary>
public class LogStream
{
    private static readonly Lazy<LogStream> _instance = new(() => new LogStream());
    public static LogStream Instance => _instance.Value;

    // Ring buffer: keep the last 10 000 entries in memory
    private const int MaxEntries = 10_000;
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    // Event for live subscribers
    public event Action<LogEntry>? OnEntry;

    private LogStream() { }

    public void Write(LogLevel level, string source, string message, string? exception = null)
    {
        var entry = new LogEntry
        {
            Level = level,
            Source = source,
            Message = message,
            Exception = exception
        };

        _entries.Enqueue(entry);

        // Trim oldest when exceeding capacity
        while (_entries.Count > MaxEntries)
            _entries.TryDequeue(out _);

        // Always write to console so progress is visible during normal operation
        Console.WriteLine(entry.Formatted);

        // Notify live subscribers
        OnEntry?.Invoke(entry);
    }

    public void Debug(string source, string message) => Write(LogLevel.Debug, source, message);
    public void Info(string source, string message) => Write(LogLevel.Info, source, message);
    public void Warning(string source, string message) => Write(LogLevel.Warning, source, message);
    public void Error(string source, string message, string? exception = null) => Write(LogLevel.Error, source, message, exception);

    /// <summary>
    /// Returns a snapshot of all buffered entries (oldest first).
    /// </summary>
    public IReadOnlyList<LogEntry> GetSnapshot()
    {
        return _entries.ToArray();
    }

    /// <summary>
    /// Returns entries matching the given level and/or source filter.
    /// </summary>
    public IReadOnlyList<LogEntry> GetFiltered(LogLevel? minLevel = null, string? sourceFilter = null)
    {
        var query = _entries.AsEnumerable();

        if (minLevel.HasValue)
            query = query.Where(e => e.Level >= minLevel.Value);

        if (!string.IsNullOrWhiteSpace(sourceFilter))
            query = query.Where(e => e.Source.Contains(sourceFilter, StringComparison.OrdinalIgnoreCase));

        return query.ToArray();
    }
}
