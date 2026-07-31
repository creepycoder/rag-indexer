# Rag.Indexer

A **.NET 10** semantic code-indexing solution for Retrieval-Augmented Generation (RAG) workflows. It scans source repositories, parses C# code into semantic chunks (classes and methods) via Roslyn, generates vector embeddings with **Ollama** or **Azure OpenAI**, and stores them in **Qdrant** for similarity search.

The index is exposed through an **ASP.NET Core Web API** (`/api/index`, `/api/search`, `/api/context`) and an **MCP server** (`GetContext` tool), orchestrated with **.NET Aspire**.

---

## Table of Contents

- [Features](#features)
- [Solution Structure](#solution-structure)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
  - [Build](#build)
  - [Run the Web API (via Aspire)](#run-the-web-api-via-aspire)
  - [Run the Web API standalone](#run-the-web-api-standalone)
  - [Run the MCP server](#run-the-mcp-server)
- [API Reference](#api-reference)
  - [`POST /api/index`](#post-api-index)
  - [`POST /api/search`](#post-api-search)
  - [`POST /api/context`](#post-api-context)
- [MCP Server (`Rag.Indexer.Mcp`)](#mcp-server-ragindexermcp)
  - [Quick start](#quick-start)
  - [Transport modes](#transport-modes)
  - [Configuration (MCP-specific)](#configuration-mcp-specific)
  - [Tool available to AI clients](#tool-available-to-ai-clients)
  - [Claude Desktop setup](#claude-desktop-setup)
  - [Cursor setup](#cursor-setup)
  - [Detailed documentation](#detailed-documentation)
- [Configuration](#configuration)
  - [Embedding provider](#embedding-provider)
  - [Service endpoints](#service-endpoints)
  - [`.ragignore`](#ragignore)
  - [Supported file extensions](#supported-file-extensions)
  - [`.ragindex-state.json`](#ragindex-statejson)
- [How Incremental Indexing Works](#how-incremental-indexing-works)
- [License](#license)

---

## Features

- **Semantic C# chunking** – Roslyn (`Microsoft.CodeAnalysis.CSharp`) extracts classes and methods as separate searchable chunks; all other supported file types are indexed as whole documents.
- **Pluggable embeddings** – `IEmbeddingService` abstraction with two implementations: Ollama (local, default) and Azure OpenAI (cloud).
- **Incremental (delta) indexing** – Only new, modified, or deleted files are reprocessed on each run; unchanged files are skipped via a `.ragindex-state.json` state file.
- **Interrupt-resilient** – State is saved after every file, so a crash or Ctrl+C resumes from the last fully-processed file.
- **Deterministic chunk IDs** – Qdrant point UUIDs derive from content (SHA-256), so upserts naturally deduplicate.
- **Embedding cache** – Repeated queries reuse cached vectors instead of re-calling the embedding service.
- **Backup & snapshots** – `QdrantSnapshotService` creates collection snapshots through the Qdrant REST API for backup/restore.
- **Repository filtering** – Gitignore-style `.ragignore` patterns and an allow-listed set of file extensions.
- **Web API** – REST endpoints for indexing, semantic search, and retrieving aggregated context, with OpenAPI + Scalar UI in development.
- **MCP server** – Exposes a `GetContext` tool so AI assistants can query the index, over `stdio` or HTTP transport.
- **Aspire orchestration** – `Rag.Indexer.AppHost` runs the API with the Aspire dashboard, OpenTelemetry, and service discovery.
- **Graceful configuration** – `appsettings.json` is optional; falls back to environment variables and sensible defaults (`ollama`, `localhost:11434`).

---

## Solution Structure

```
Rag.slnx                                   # .NET solution file
src/
├── Rag.Indexer/                           # Core library (scanning, parsing, indexing, search)
│   ├── Services/
│   │   ├── Configuration/EmbeddingOptions.cs   # Strongly-typed options POCOs
│   │   ├── IEmbeddingService.cs                # Embedding abstraction
│   │   ├── OllamaEmbeddingService.cs           # Local embedding via Ollama
│   │   ├── AzureOpenAiEmbeddingService.cs      # Cloud embedding via Azure OpenAI
│   │   ├── RepositoryScanner.cs                # File discovery
│   │   ├── RagIgnore.cs                        # .ragignore pattern matching
│   │   ├── CSharpCodeParser.cs                 # Roslyn-based C# parser
│   │   ├── IndexingService.cs                  # Delta-aware indexing pipeline
│   │   ├── IndexStateManager.cs                # State file management & delta computation
│   │   ├── QdrantService.cs                    # Qdrant vector DB client (gRPC)
│   │   ├── QdrantSnapshotService.cs            # Qdrant snapshots via REST
│   │   ├── EmbeddingCacheService.cs            # In-memory embedding cache
│   │   ├── SearchService.cs                    # Semantic search
│   │   └── LogStream.cs                        # In-memory log buffer
│   └── Models/                                 # CodeChunk, IndexState, LogEntry, ...
├── Rag.Indexer.Api/                        # ASP.NET Core Web API
│   └── Controllers/RagController.cs         # /api/index, /api/search, /api/context
├── Rag.Indexer.Mcp/                        # MCP server (stdio + HTTP)
│   └── Tools/RagTools.cs                    # GetContext tool
├── Rag.Indexer.AppHost/                    # Aspire orchestrator
└── Rag.Indexer.ServiceDefaults/            # Shared Aspire defaults (telemetry, health, resilience)
```

---

## Prerequisites

| Tool | Purpose | Install |
|------|---------|---------|
| **.NET 10 SDK** | Build & run | `winget install Microsoft.DotNet.SDK.10` |
| **Qdrant** | Vector database (gRPC `:6334`, REST `:6333`) | Docker: `docker run -d --name qdrant -p 6333:6333 -p 6334:6334 qdrant/qdrant` |
| **Ollama** (default) | Local embeddings | `winget install Ollama.Ollama` then `ollama pull mxbai-embed-large` |
| **Azure OpenAI** (optional) | Cloud embeddings | A deployed embedding model, e.g. `text-embedding-ada-002` |

The `uefa_code` collection is created automatically on first indexing run.

---

## Getting Started

### Build

```powershell
dotnet build
```

### Run the Web API (via Aspire)

```powershell
dotnet run --project src/Rag.Indexer.AppHost
```

This starts the Aspire dashboard and the API. In development, OpenAPI is available at `/openapi/v1.json` and an interactive Scalar UI at `/scalar`.

### Run the Web API standalone

```powershell
dotnet run --project src/Rag.Indexer.Api
```

### Run the MCP server

See the [MCP Server README](src/Rag.Indexer.Mcp/README.md) for transport modes, configuration, and AI-client setup instructions.

---

## API Reference

Base path: `/api`

### `POST /api/index`

Index a repository folder into Qdrant.

```json
{ "repositoryPath": "C:\\Projects\\MyApp" }
```

```powershell
curl.exe -X POST http://localhost:5004/api/index `
  -H "Content-Type: application/json" `
  -d '{\"repositoryPath\":\"C:/Projects/MyApp\"}'
```

### `POST /api/search`

Semantic vector search over indexed chunks.

```json
{ "query": "how is delta indexing computed?", "limit": 5 }
```

Returns ranked results with `project`, `file`, `type` (`class` / `method` / `document`), `symbol`, `namespace`, `content`, and `score`.

### `POST /api/context`

Like `/api/search`, but returns an aggregated, ready-to-inject context string alongside the results. Supports optional payload filters:

```json
{
  "query": "authentication middleware",
  "limit": 5,
  "fileFilter": "Program.cs",
  "symbolTypeFilter": "class"
}
```

---

## MCP Server (`Rag.Indexer.Mcp`)

The solution includes a standalone **MCP (Model Context Protocol) server** that exposes the RAG indexer as an AI-assistant tool. Any MCP-compatible client — Claude Desktop, Cursor, Windsurf, Copilot CLI, etc. — can call `GetContext` to perform semantic code lookups without custom integration code.

### Quick start

```powershell
# stdio transport (default) — stdout is reserved for the MCP protocol
dotnet run --project src/Rag.Indexer.Mcp

# HTTP transport
dotnet run --project src/Rag.Indexer.Mcp -- --transport=http
```

### Transport modes

| Mode | Use case | How to start |
|------|----------|-------------|
| **stdio** *(default)* | Local AI clients that connect via stdin/stdout pipes (Claude Desktop, Cursor, etc.) | `dotnet run` |
| **HTTP** | Remote clients connecting over a network | `dotnet run -- --transport=http` |

#### stdio transport

- **stdout** is reserved exclusively for MCP protocol messages (JSON-RPC frames). Never write to stdout outside the MCP library.
- **stderr** is used for all logging (`LogLevel.Trace`). Stdout providers are cleared so they do not corrupt the protocol stream.
- A bare `Host` builder is used — Kestrel does **not** start, so no ports bind and there is zero risk of conflicting with MCP pipes.

#### HTTP transport

- Uses ASP.NET Core via `AddMcpServer().WithHttpTransport()`.
- Application logging is cleared to prevent polluting stdout.
- Exposes the MCP endpoint at `/mcp` (standard MCP-over-SSE path).

### Configuration (MCP-specific)

| Variable | Default | Description |
|----------|---------|-------------|
| `OLLAMA_BASE_URL` | `http://localhost:11434` | Ollama API endpoint for embedding generation |
| `OLLAMA_MODEL` | `mxbai-embed-large` | Ollama model identifier |
| `QDRANT_HOST` | `localhost` | Qdrant vector store hostname |
| `QDRANT_PORT` | `6334` | Qdrant gRPC port |
| `MCP_TRANSPORT` | `stdio` | Override: set to `http` to force HTTP mode without passing `--transport=http` |

### Tool available to AI clients

| Tool | Signature | Description |
|------|-----------|-------------|
| `GetContext` | `get_context(query, limit=5)` | Searches indexed code and returns relevant code snippets with file, symbol, namespace, and score. |

### Claude Desktop setup

```json
{
  "mcpServers": {
    "rag-indexer": {
      "command": "dotnet",
      "args": ["C:\\path\\to\\Rag.Indexer.Mcp.dll"]
    }
  }
}
```

### Cursor setup

Open **Settings → Features → MCP → Add new MCP server** and add the same config block as above.

### Detailed documentation

For full documentation — architecture, logging, dependencies, `GetContext` tool specification, extending with new tools, and troubleshooting — see **[Rag.Indexer.Mcp README](src/Rag.Indexer.Mcp/README.md)**.

---

## Configuration

### Embedding provider

`Rag.Indexer` reads the `Embedding` section from `appsettings.json` with environment-variable overlay (`__` = nested):

```json
{
  "Embedding": {
    "Provider": "ollama",
    "Ollama": {
      "BaseUrl": "http://localhost:11434",
      "Model": "mxbai-embed-large"
    },
    "AzureOpenAi": {
      "Endpoint": "",
      "Key": "",
      "DeploymentName": "text-embedding-ada-002"
    }
  }
}
```

Switch to Azure OpenAI via environment variables:

```powershell
$env:Embedding__Provider = "azure"
$env:Embedding__AzureOpenAi__Endpoint = "https://your-resource.openai.azure.com"
$env:Embedding__AzureOpenAi__Key = "your-api-key"
```

### Service endpoints

| Variable | Default | Purpose |
|----------|---------|---------|
| `QDRANT_HOST` | `localhost` | Qdrant host |
| `QDRANT_PORT` | `6334` | Qdrant gRPC port |
| `QDRANT_REST_PORT` | `6333` | Qdrant REST port (snapshots) |
| `OLLAMA_BASE_URL` | `http://localhost:11434` | Ollama endpoint (MCP server) |
| `OLLAMA_MODEL` | `mxbai-embed-large` | Ollama embedding model (MCP server) |
| `MCP_TRANSPORT` | `stdio` | MCP transport: `stdio` or `http` |

### `.ragignore`

Place a `.ragignore` file at the root of the repository being indexed. It uses gitignore-style patterns:

```
# Build artifacts
bin/
obj/

# Generated code
*.Designer.cs
*.g.cs

# Secrets
appsettings.Development.json
```

### Supported file extensions

`.cs`, `.csproj`, `.sln`, `.json`, `.yaml`, `.yml`, `.xml`, `.config`, `.sql`, `.md`

### `.ragindex-state.json`

Auto-generated in the indexed root folder; tracks content hashes for delta detection. **Do not commit it** (already in `.gitignore`). Delete it to force a full re-index.

---

## How Incremental Indexing Works

1. The state file stores a SHA-256 hash per indexed file.
2. On each run, the indexer computes the delta:
   - **New files** → indexed.
   - **Modified files** → old points deleted, file re-indexed.
   - **Deleted files** → their Qdrant points are removed.
   - **Unchanged files** → skipped entirely.
3. State is saved after every file, so an interrupt only loses the current file's work.
4. If the `uefa_code` collection is deleted, the stale state file is discarded and a full re-index runs.

---

## License

UNLICENSED – Internal / private project.
