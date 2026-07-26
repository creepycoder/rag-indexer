using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Rag.Indexer.Models;

namespace Rag.Indexer.Services;

/// <summary>
/// Disk-based cache for embeddings. Stores each chunk's embedding as a JSON file
/// keyed by the content hash. This allows rebuilding the Qdrant collection from
/// cache without re-calling Ollama, reducing recovery from days to minutes.
/// </summary>
public class EmbeddingCacheService
{
    private static readonly LogStream Log = LogStream.Instance;

    private readonly string _cacheDir;

    public EmbeddingCacheService(string? cacheDir = null)
    {
        _cacheDir = cacheDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RagIndexer",
            "embedding-cache");

        Directory.CreateDirectory(_cacheDir);
    }

    /// <summary>
    /// Gets the cache directory path.
    /// </summary>
    public string CacheDirectory => _cacheDir;

    /// <summary>
    /// Computes a SHA-256 hash of the content for cache keying.
    /// Uses spans to avoid intermediate string allocations from Replace().
    /// </summary>
    public static string ComputeContentHash(string content)
    {
        // Normalize line endings: replace \r\n with \n
        // Use spans to avoid intermediate string allocations from Replace()
        var hasR = content.AsSpan().IndexOf('\r');

        if (hasR >= 0)
        {
            // Build normalized content without \r characters using stackalloc for small content
            Span<char> buffer = content.Length <= 4096
                ? stackalloc char[content.Length]
                : new char[content.Length];

            var writePos = 0;
            foreach (var c in content)
            {
                if (c != '\r')
                    buffer[writePos++] = c;
            }

            var normalized = buffer[..writePos];

            // Encode to UTF-8
            var maxByteCount = Encoding.UTF8.GetMaxByteCount(normalized.Length);
            Span<byte> utf8Bytes = maxByteCount <= 4096
                ? stackalloc byte[maxByteCount]
                : new byte[maxByteCount];

            var actualByteCount = Encoding.UTF8.GetBytes(normalized, utf8Bytes);

            // Hash directly from the span
            Span<byte> hashBytes = stackalloc byte[SHA256.HashSizeInBytes];
            SHA256.HashData(utf8Bytes[..actualByteCount], hashBytes);

            return Convert.ToHexStringLower(hashBytes);
        }
        else
        {
            // No \r characters, hash directly from the original string
            var maxByteCount = Encoding.UTF8.GetMaxByteCount(content.Length);
            Span<byte> utf8Bytes = maxByteCount <= 4096
                ? stackalloc byte[maxByteCount]
                : new byte[maxByteCount];

            var actualByteCount = Encoding.UTF8.GetBytes(content.AsSpan(), utf8Bytes);

            Span<byte> hashBytes = stackalloc byte[SHA256.HashSizeInBytes];
            SHA256.HashData(utf8Bytes[..actualByteCount], hashBytes);

            return Convert.ToHexStringLower(hashBytes);
        }
    }

    /// <summary>
    /// Attempts to retrieve a cached embedding for the given content hash.
    /// Returns null if not found.
    /// </summary>
    public EmbeddingCacheEntry? Get(string contentHash)
    {
        var cachePath = GetCachePath(contentHash);

        if (!File.Exists(cachePath))
            return null;

        try
        {
            var json = File.ReadAllText(cachePath);
            return JsonSerializer.Deserialize<EmbeddingCacheEntry>(json);
        }
        catch (Exception ex)
        {
            Log.Warning("EmbeddingCache", $"Failed to read cache entry {contentHash}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Stores an embedding in the cache.
    /// </summary>
    public void Set(string contentHash, float[] embedding, string sourceFile, string symbolType, string symbolName)
    {
        var cachePath = GetCachePath(contentHash);

        try
        {
            var entry = new EmbeddingCacheEntry
            {
                ContentHash = contentHash,
                Embedding = embedding,
                CreatedAt = DateTime.UtcNow,
                SourceFile = sourceFile,
                SymbolType = symbolType,
                SymbolName = symbolName
            };

            var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(cachePath, json);
        }
        catch (Exception ex)
        {
            Log.Warning("EmbeddingCache", $"Failed to write cache entry {contentHash}: {ex.Message}");
        }
    }

    /// <summary>
    /// Returns all cached entries. Useful for rebuilding Qdrant from cache.
    /// </summary>
    public List<EmbeddingCacheEntry> GetAll()
    {
        var entries = new List<EmbeddingCacheEntry>();

        if (!Directory.Exists(_cacheDir))
            return entries;

        foreach (var file in Directory.GetFiles(_cacheDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var entry = JsonSerializer.Deserialize<EmbeddingCacheEntry>(json);
                if (entry != null)
                    entries.Add(entry);
            }
            catch
            {
                // Skip corrupt entries
            }
        }

        return entries;
    }

    /// <summary>
    /// Returns the number of cached entries.
    /// </summary>
    public int Count()
    {
        if (!Directory.Exists(_cacheDir))
            return 0;

        return Directory.GetFiles(_cacheDir, "*.json").Length;
    }

    /// <summary>
    /// Clears the entire cache.
    /// </summary>
    public void Clear()
    {
        if (!Directory.Exists(_cacheDir))
            return;

        foreach (var file in Directory.GetFiles(_cacheDir, "*.json"))
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception ex)
            {
                Log.Warning("EmbeddingCache", $"Failed to delete cache file {file}: {ex.Message}");
            }
        }

        Log.Info("EmbeddingCache", "Cache cleared.");
    }

    /// <summary>
    /// Gets the total size of the cache in bytes.
    /// </summary>
    public long GetCacheSizeBytes()
    {
        if (!Directory.Exists(_cacheDir))
            return 0;

        return Directory.GetFiles(_cacheDir, "*.json")
            .Sum(f => new FileInfo(f).Length);
    }

    private string GetCachePath(string contentHash)
    {
        return Path.Combine(_cacheDir, $"{contentHash}.json");
    }
}