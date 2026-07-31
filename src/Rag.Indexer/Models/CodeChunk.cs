using System.Security.Cryptography;
using System.Text;

namespace Rag.Indexer.Models;

public record CodeChunk
{
    /// <summary>
    /// Unique identifier for this chunk, used as the Qdrant point UUID.
    /// Generated deterministically from the chunk content to support delta indexing via upsert.
    /// </summary>
    public string Id { get; set; } = "";

    public string Project { get; init; } = "";

    public string FilePath { get; init; } = "";

    public string Namespace { get; init; } = "";

    public string SymbolType { get; init; } = "";

    public string SymbolName { get; init; } = "";

    public string? ParentSymbol { get; init; }

    public List<string> Usings { get; init; } = [];

    public List<string> Attributes { get; init; } = [];

    public List<string> Dependencies { get; init; } = [];

    public string Content { get; init; } = "";

    /// <summary>
    /// Computes a deterministic UUID from the chunk's file path, symbol name, and content.
    /// This ensures the same chunk always gets the same Qdrant point ID, enabling upsert deduplication.
    /// Call this before upserting to Qdrant.
    /// </summary>
    public void ComputeDeterministicId()
    {
        var raw = $"{FilePath}::{SymbolType}::{SymbolName}::{Content}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));

        // Convert first 16 bytes of SHA-256 to a UUID-formatted string
        var guid = new Guid(bytes[..16]);
        Id = guid.ToString();
    }
}
