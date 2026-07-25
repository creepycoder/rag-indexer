using System.Security.Cryptography;
using System.Text;

namespace UEFA.Rag.Indexer.Models;

public class CodeChunk
{
    /// <summary>
    /// Unique identifier for this chunk, used as the Qdrant point UUID.
    /// Generated deterministically from the chunk content to support delta indexing via upsert.
    /// </summary>
    public string Id { get; set; } = "";

    public string Project { get; set; } = "";

    public string FilePath { get; set; } = "";

    public string Namespace { get; set; } = "";

    public string SymbolType { get; set; } = "";

    public string SymbolName { get; set; } = "";

    public string? ParentSymbol { get; set; }

    public List<string> Usings { get; set; } = [];

    public List<string> Attributes { get; set; } = [];

    public List<string> Dependencies { get; set; } = [];

    public string Content { get; set; } = "";

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