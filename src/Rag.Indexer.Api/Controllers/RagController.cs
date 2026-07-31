using System.Text;
using Microsoft.AspNetCore.Mvc;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Rag.Indexer.Services;
using Rag.Indexer.Api.Models;

namespace Rag.Indexer.Api.Controllers;

[ApiController]
[Route("api")]
public class RagController : ControllerBase
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IndexingService _indexingService;
    private readonly QdrantService _qdrantService;
    private readonly EmbeddingCacheService _cacheService;
    private readonly ILogger<RagController> _logger;

    public RagController(
        IEmbeddingService embeddingService,
        IndexingService indexingService,
        QdrantService qdrantService,
        EmbeddingCacheService cacheService,
        ILogger<RagController> logger)
    {
        _embeddingService = embeddingService;
        _indexingService = indexingService;
        _qdrantService = qdrantService;
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/search — Vector search against indexed code chunks.
    /// </summary>
    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] SearchRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest(new { error = "Query is required." });

        try
        {
            var vector = await GetEmbeddingAsync(request.Query);
            if (vector is null)
                return StatusCode(502, new { error = "Failed to generate embedding." });

            var qdrantHost = Environment.GetEnvironmentVariable("QDRANT_HOST") ?? "localhost";
            var qdrantPortStr = Environment.GetEnvironmentVariable("QDRANT_PORT") ?? "6334";
            int.TryParse(qdrantPortStr, out var qdrantPort);

            var qdrant = new QdrantClient(qdrantHost, qdrantPort > 0 ? qdrantPort : 6334);
            var results = await qdrant.SearchAsync(
                collectionName: "uefa_code",
                vector: vector,
                limit: (ulong)Math.Max(1, request.Limit));

            var output = new List<SearchResult>();
            foreach (var r in results)
            {
                output.Add(new SearchResult
                {
                    Id = r.Id?.Uuid ?? "",
                    Score = r.Score,
                    Project = GetPayload(r, "project"),
                    FilePath = GetPayload(r, "file"),
                    SymbolType = GetPayload(r, "type"),
                    SymbolName = GetPayload(r, "symbol"),
                    Namespace = GetPayload(r, "namespace"),
                    Content = GetPayload(r, "content")
                });
            }

            return Ok(new { results = output });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Search failed — Ollama connection error.");
            return StatusCode(502, new { error = "Cannot connect to Ollama. Ensure Ollama is running." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Search failed for query: {Query}", request.Query);
            return StatusCode(500, new { error = "Search failed." });
        }
    }

    /// <summary>
    /// POST /api/index — Index a repository folder into Qdrant.
    /// </summary>
    [HttpPost("index")]
    public async Task<IActionResult> Index([FromBody] IndexRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RepositoryPath))
            return BadRequest(new { error = "RepositoryPath is required." });

        if (!Directory.Exists(request.RepositoryPath))
            return BadRequest(new { error = $"Folder not found: {request.RepositoryPath}" });

        try
        {
            await _indexingService.IndexAsync(request.RepositoryPath);
            return Ok(new { message = "Indexing completed.", path = request.RepositoryPath });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Indexing failed for {Path}", request.RepositoryPath);
            return StatusCode(500, new { error = "Indexing failed." });
        }
    }

    /// <summary>
    /// POST /api/context — Get relevant context chunks for a query, with optional filters.
    /// Returns both individual results and an aggregated context string.
    /// </summary>
    [HttpPost("context")]
    public async Task<IActionResult> GetContext([FromBody] ContextRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest(new { error = "Query is required." });

        try
        {
            var vector = await GetEmbeddingAsync(request.Query);
            if (vector is null)
                return StatusCode(502, new { error = "Failed to generate embedding." });

            var qdrantHost = Environment.GetEnvironmentVariable("QDRANT_HOST") ?? "localhost";
            var qdrantPortStr = Environment.GetEnvironmentVariable("QDRANT_PORT") ?? "6334";
            int.TryParse(qdrantPortStr, out var qdrantPort);

            var qdrant = new QdrantClient(qdrantHost, qdrantPort > 0 ? qdrantPort : 6334);

            // Build filter if any optional filters are provided
            Filter? filter = null;
            if (!string.IsNullOrWhiteSpace(request.FileFilter) ||
                !string.IsNullOrWhiteSpace(request.SymbolTypeFilter))
            {
                filter = new Filter();
                if (!string.IsNullOrWhiteSpace(request.FileFilter))
                {
                    filter.Must.Add(new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "file",
                            Match = new Match { Text = request.FileFilter }
                        }
                    });
                }
                if (!string.IsNullOrWhiteSpace(request.SymbolTypeFilter))
                {
                    filter.Must.Add(new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "type",
                            Match = new Match { Text = request.SymbolTypeFilter }
                        }
                    });
                }
            }

            var results = await qdrant.SearchAsync(
                collectionName: "uefa_code",
                vector: vector,
                filter: filter,
                limit: (ulong)Math.Max(1, request.Limit));

            var output = new List<SearchResult>();
            var contextBuilder = new StringBuilder();

            foreach (var r in results)
            {
                var sr = new SearchResult
                {
                    Id = r.Id?.Uuid ?? "",
                    Score = r.Score,
                    Project = GetPayload(r, "project"),
                    FilePath = GetPayload(r, "file"),
                    SymbolType = GetPayload(r, "type"),
                    SymbolName = GetPayload(r, "symbol"),
                    Namespace = GetPayload(r, "namespace"),
                    Content = GetPayload(r, "content")
                };
                output.Add(sr);

                contextBuilder.AppendLine($"--- {sr.SymbolType}: {sr.SymbolName} ({sr.FilePath}) ---");
                contextBuilder.AppendLine(sr.Content);
                contextBuilder.AppendLine();
            }

            return Ok(new ContextResult
            {
                Results = output,
                AggregatedContext = contextBuilder.ToString()
            });
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Context search failed — Ollama connection error.");
            return StatusCode(502, new { error = "Cannot connect to Ollama. Ensure Ollama is running." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Context search failed for query: {Query}", request.Query);
            return StatusCode(500, new { error = "Context search failed." });
        }
    }

    // ── helpers ──────────────────────────────────────

    private async Task<float[]?> GetEmbeddingAsync(string text)
    {
        // Try cache first
        var contentHash = EmbeddingCacheService.ComputeContentHash(text);
        var cached = _cacheService.Get(contentHash);
        if (cached is not null)
        {
            _logger.LogInformation("Using cached embedding for query.");
            return cached.Embedding;
        }

        // Call Ollama for embedding
        var ollamaBaseUrl = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL") ?? "http://localhost:11434";
        using var http = new HttpClient();
        var response = await http.PostAsJsonAsync(
            $"{ollamaBaseUrl}/api/embed",
            new { model = "mxbai-embed-large", input = text });

        response.EnsureSuccessStatusCode();

        var embedding = await response.Content.ReadFromJsonAsync<EmbeddingResponse>();
        if (embedding?.Embeddings is not { Length: > 0 } || embedding.Embeddings[0].Length == 0)
            return null;

        var vector = embedding.Embeddings[0];

        // Store in cache
        _cacheService.Set(contentHash, vector, "query", "query", "search");
        return vector;
    }

    private static string GetPayload(ScoredPoint point, string key)
    {
        return point.Payload.TryGetValue(key, out var val) ? val.StringValue : "";
    }

    private class EmbeddingResponse
    {
        public float[][] Embeddings { get; set; } = [];
    }
}