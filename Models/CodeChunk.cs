namespace UEFA.Rag.Indexer.Models;

public class CodeChunk
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

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
}