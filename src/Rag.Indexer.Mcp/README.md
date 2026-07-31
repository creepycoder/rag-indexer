# Rag.Indexer.Mcp — MCP Server for Semantic Code Search

A **.NET 10** [Model Context Protocol](https://modelcontextprotocol.io/) (MCP) server that exposes **RAG.Indexer**'s semantic code search as an AI-assistant tool. Let your favourite LLM client query indexed C# codebases via a standardised protocol.

## What it does

The project hosts a single MCP tool:

| Tool | Description |
|------|-------------|
| `GetContext` | Search the Qdrant vector index for code chunks relevant to a natural-language query and return matching snippets with file, namespace, symbol, and score metadata. |

This enables **any MCP-compatible AI assistant** (Claude Desktop, Cursor, Windsurf, Copilot CLI, etc.) to perform contextual code lookups without writing custom integration code.

## Architecture overview

```
┌──────────────────┐     MCP protocol      ┌─────────────────────┐
│  AI Assistant    │ ◄──────────────────►  │ Rag.Indexer.Mcp     │
│  (Claude, Cursor, │   stdio or HTTP     │ (.exe / Hosted)     │
│   Copilot CLI…)   │                       │                     │
│                  │                        │  IEmbeddingService  │
└──────────────────┘                        │  (Ollama default)   │
                                            └──────┬──────────────┘
                                                   │ vector
                                             ┌─────▼───────┐
                                             │     Qdrant    │
                                             │  uefa_code    │
                                             │  collection   │
                                             └───────────────┘
```

- **Embedded service** — The MCP server embeds `IEmbeddingService` (Ollama, default; pluggable) to convert query text into embedding vectors.
- **Direct Qdrant client** — Each tool call opens a lightweight `QdrantClient` connected to the configured host and queries the `uefa_code` collection with cosine similarity.
- **Dual transport** — Runs over **stdio** (for local editor integration) or **HTTP** (for remote/networked scenarios).

## Table of Contents

- [What it does](#what-it-does)
- [Architecture overview](#architecture-overview)
- [Transport modes](#transport-modes)
  - [stdio transport details](#stdio-transport-details)
  - [HTTP transport details](#http-transport-details)
- [Configuration](#configuration)
- [Getting Started](#getting-started)
  - [Claude Desktop example](#getting-started-claude-desktop-example)
  - [Cursor example](#getting-started-cursor-example)
- [Building & Debugging](#building-debugging)
  - [Logging in stdio mode](#logging-in-stdio-mode)
- [Project dependencies](#project-dependencies)
- [Tool specification: `GetContext`](#tool-specification-getcontext)
  - [Parameters](#parameters)
  - [Response format](#response-format)
  - [Error handling](#error-handling)
- [Extending this project](#extending-this-project)
- [Troubleshooting](#troubleshooting)

## Transport modes

| Mode | Use case | How to start |
|------|----------|-------------|
| **stdio** *(default)* | Local AI clients that connect via stdin/stdout pipes (Claude Desktop, Cursor, etc.) | `dotnet run` |
| **HTTP** | Remote clients connecting over a network | `dotnet run -- --transport=http` |

### stdio transport details

- **stdout** — reserved exclusively for MCP protocol messages (JSON-RPC frames). *Never* write to stdout outside the MCP library.
- **stderr** — used for all logging (`LogLevel.Trace`). Logs are cleared from stdout providers so they don't corrupt the protocol stream.
- **Kestrel is NOT started** — a bare `Host` builder is used instead of `WebApplication.CreateBuilder`, so no ports bind and there is zero risk of conflicting with MCP pipes.

### HTTP transport details

- Uses ASP.NET Core (`WebApplication`) with `AddMcpServer().WithHttpTransport()`.
- All application logging is cleared to prevent polluting stdout.
- Exposes the MCP endpoint at `/mcp` (standard MCP-over-SSE path).

## Configuration

All settings come from environment variables — no config files required.

| Variable | Default | Description |
|----------|---------|-------------|
| `OLLAMA_BASE_URL` | `http://localhost:11434` | Ollama API endpoint for embedding generation |
| `OLLAMA_MODEL` | `mxbai-embed-large` | Ollama model identifier |
| `QDRANT_HOST` | `localhost` | Qdrant vector store hostname |
| `QDRANT_PORT` | `6334` | Qdrant gRPC port |
| `MCP_TRANSPORT` | `stdio` | Override: set to `http` to force HTTP mode without passing `--transport=http` |

## Getting Started (Claude Desktop example)

1. Ensure Ollama and Qdrant are running locally:
   ```powershell
   ollama pull mxbai-embed-large
   docker run -d --name qdrant -p 6333:6333 -p 6334:6334 qdrant/qdrant
   ```

2. Start the MCP server manually to verify it works:
   ```powershell
   dotnet run --project src/Rag.Indexer.Mcp
   ```

3. Add the server to your Claude Desktop config (`claude_desktop_config.json`):
   ```json
   {
     "mcpServers": {
       "rag-indexer": {
         "command": "dotnet",
         "args": [
           "C:\\path\\to\\Rag.Indexer.Mcp.dll"
         ]
       }
     }
   }
   ```

4. Restart Claude Desktop. You should see `GetContext` appear as an available tool in the agent panel.

## Getting Started (Cursor example)

In Cursor, open **Settings → Features → MCP → Add new MCP server**:

```json
{
  "rag-indexer": {
    "command": "dotnet",
    "args": ["C:\\path\\to\\Rag.Indexer.Mcp.dll"]
  }
}
```

## Building & Debugging

| Command | Description |
|---------|-------------|
| `dotnet build src/Rag.Indexer.Mcp` | Build the project |
| `dotnet run --project src/Rag.Indexer.Mcp` | Run (stdio mode) |
| `dotnet run --project src/Rag.Indexer.Mcp -- --transport=http` | Run (HTTP mode) |
| `MCP_TRANSPORT=http dotnet run --project src/Rag.Indexer.Mcp` | Run via env var |

### Logging in stdio mode

Logs go to stderr with trace-level verbosity. To inspect them:

```powershell
dotnet run --project src/Rag.Indexer.Mcp 2> mcp-logs.txt
```

In HTTP mode, logs are suppressed entirely (Kestrel output is cleared). Adjust via standard ASP.NET Core logging configuration if needed.

## Project dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| `ModelContextProtocol` | 1.4.1 | MCP server SDK (.NET client/server protocol) |
| `ModelContextProtocol.AspNetCore` | 1.4.1 | ASP.NET Core integration (HTTP transport) |
| `Qdrant.Client` | — | Qdrant gRPC client |

## Tool specification: `GetContext`

### Parameters

| Name | Type | Required | Description |
|------|------|----------|-------------|
| `query` | string | Yes | Natural-language search query (e.g., "find the authentication middleware") |
| `limit` | int | No (default 5) | Maximum number of results to return |

### Response format

A markdown-formatted string containing matched code chunks, each block prefixed with metadata:

```
--- [class] UserController (Controllers/UserController.cs) score=0.847 ---
namespace MyApp.Controllers
public class UserController : ControllerBase { ... }

--- [method] GetUser (Controllers/UserController.cs) score=0.821 ---
namespace MyApp.Controllers
public async Task<IActionResult> GetUser(int id) { ... }
```

### Error handling

- **No results** → returns `"No relevant code found."`
- **Embedding failure** → propagates the exception to the MCP client with an error result.
- **Qdrant unreachable** → returns an error result describing the connection failure.

## Extending this project

To add a new tool, create another class in `Tools/`:

```csharp
[McpServerToolType]
public sealed class MyTools
{
    [McpServerTool]
    [Description("My custom description")]
    public Task<string> MyToolAsync([Description("param desc")] string param) => …;
}
```

The server uses `WithToolsFromAssembly()` which automatically discovers and registers every `[McpServerTool]`-annotated method in the project's compiled assembly. Inject dependencies via constructor injection just like a standard ASP.NET Core service.

## Troubleshooting

| Problem | Solution |
|---------|----------|
| AI client can't find tools | Verify `GetContext` appears in the MCP tool list (Claude Desktop: agent panel; Cursor: Settings → MCP). |
| Connection refused to Qdrant | Ensure `docker ps` shows a running Qdrant container on port 6334. |
| Ollama errors | Run `curl http://localhost:11434/api/tags` to confirm the API is responsive and `mxbai-embed-large` is pulled. |
| stdio mode stalls silently | Check stderr (`2> logs.txt`) — MCP protocol messages on stdout may be corrupted if logging was not cleared properly. |
