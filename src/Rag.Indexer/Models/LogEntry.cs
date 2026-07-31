namespace Rag.Indexer.Models;

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

public record LogEntry
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public LogLevel Level { get; init; }
    public string Source { get; init; } = "";
    public string Message { get; init; } = "";
    public string? Exception { get; init; }

    public string Formatted =>
        $"{Timestamp:HH:mm:ss.fff} [{Level.ToString().ToUpperInvariant(),-7}] [{Source}] {Message}{(Exception is not null ? $"\n  {Exception}" : "")}";
}
