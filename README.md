# Rag.Indexer

A **.NET 10** semantic code-indexing solution for Retrieval-Augmented Generation (RAG) workflows. It scans source repositories, parses C# code into semantic chunks (classes and methods) via Roslyn, generates vector embeddings with **Ollama** or **Azure OpenAI**, and stores them in **Qdrant** for similarity search.

The index is exposed through an **ASP.NET Core Web API** (`/api/index`, `/api/search`, `/api/context`, …), an **Angular web UI** for indexing and search, and an **MCP server** (`GetContext` tool), orchestrated with **.NET Aspire**.

---

## Table of Contents

- [Features](#features)
- [Solution Structure](#solution-structure)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
  - [Build](#build)
  - [Run the Web API (via Aspire)](#run-the-web-api-via-aspire)
  - [Run the Web UI (Angular)](#run-the-web-ui-angular)
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
- [VS Code / GitHub Copilot setup](#vs-code--github-copilot-setup)
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
- **Web UI (Angular)** – a browser dashboard for indexing folders, searching the index, and viewing collection/status info, served during development by `rag-web` on `http://localhost:4200`.
- **MCP server** – Exposes a `GetContext` tool so AI assistants can query the index, over `stdio` or HTTP transport.
- **Aspire orchestration** – `Rag.Indexer.AppHost` runs the Qdrant container, a locally-installed Ollama, the indexer worker, the API, the MCP server, and the web UI, all visible in the Aspire dashboard with OpenTelemetry and service discovery.
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
│   └── Controllers/                        # RagController, FolderController, StatusController
├── Rag.Indexer.Mcp/                        # MCP server (stdio + HTTP)
│   └── Tools/RagTools.cs                    # GetContext tool
├── Rag.Indexer.Worker/                   # Continuous indexer worker (long-running)
│   └── IndexerWorker.cs                   # Periodic delta indexing of configured repos
├── Rag.Indexer.Web/                        # Angular 21 web UI (indexing, search, status)
│   └── src/app/                            # Features: index, search, status
├── Rag.Indexer.AppHost/                    # Aspire orchestrator (Qdrant + Ollama + API + MCP + worker + web)
└── Rag.Indexer.ServiceDefaults/            # Shared Aspire defaults (telemetry, health, resilience)
```

---

## Prerequisites

| Tool | Purpose | Install |
|------|---------|---------|
| **.NET 10 SDK** | Build & run | `winget install Microsoft.DotNet.SDK.10` |
| **Node.js 22+** | Build & serve the Angular web UI | `winget install OpenJS.NodeJS.LTS` |
| **Qdrant** | Vector database (gRPC `:6334`, REST `:6333`) | **Via Aspire** – automatically provisioned as a container resource by the AppHost (no manual install). **Standalone:** `docker run -d --name qdrant -p 6333:6333 -p 6334:6334 qdrant/qdrant` |
| **Ollama** (default) | Local embeddings | `winget install Ollama.Ollama` then `ollama pull mxbai-embed-large` |
| **Azure OpenAI** (optional) | Cloud embeddings | A deployed embedding model, e.g. `text-embedding-ada-002` |

The `test_code` collection is created automatically on first indexing run.

> **Ollama via Aspire:** when run through the AppHost, Ollama is started automatically as a managed **`ollama serve`** process (an executable resource, not a container) on `http://localhost:11434`, with a health check and dashboard commands (List All Models / List Running Models). The `mxbai-embed-large` model must still be pulled once: `ollama pull mxbai-embed-large`.

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

This starts the Aspire dashboard together with the full stack:

- **`qdrant`** – the Qdrant vector database as an Aspire-managed container (data persisted in a named volume). Authentication is disabled by default, so the Qdrant web dashboard opens directly without asking for a key. Set `Qdrant:ApiKey` in the AppHost `.env` file to enable a key.
- **`ollama`** – a locally-installed Ollama started as a managed executable (`ollama serve`), with a health check and dashboard commands to list all or running models.
- **`rag-api`** – the REST API. In development, OpenAPI is available at `/openapi/v1.json` and an interactive Scalar UI at `/scalar`.
- **`rag-mcp`** – the MCP server, started in **HTTP transport** mode (`/mcp`) so it can be monitored from the dashboard.
- **`rag-indexer`** – the continuous indexer worker, which periodically runs delta indexing for the repositories listed under `Indexing:Repositories`.
- **`rag-web`** – the Angular web UI, served on `http://localhost:4200` (`ng serve`), with hot reload.

All resources appear as cards in the dashboard with live logs, traces, and metrics. Qdrant exposes a web UI too — open its **HTTP** endpoint in the dashboard and append `/dashboard`.

### Run the Web UI (Angular)

The web UI is started automatically by the AppHost as the `rag-web` resource (it runs `ng serve` on `http://localhost:4200` and connects to the API at `http://localhost:5004`). To run it standalone:

```powershell
cd src/Rag.Indexer.Web
npm install
ng serve
```

The UI talks to the API over `http://localhost:5004`. The API enables CORS for browser origins in development, so no extra setup is required when both run locally.

### Run the Web API standalone

```powershell
dotnet run --project src/Rag.Indexer.Api
```

### Run the MCP server

See the [MCP Server README](src/Rag.Indexer.Mcp/README.md) for transport modes, configuration, and AI-client setup instructions.

### Run the indexer worker (standalone)

The `Rag.Indexer.Worker` project is a long-running process that keeps the index fresh. It scans the repositories under `Indexing:Repositories` on startup and re-runs **delta indexing** every `Indexing:IntervalSeconds` (default 300), so only changed files are reprocessed.

```powershell
$env:Indexing__Repositories__0 = "C:\Projects\MyApp"
dotnet run --project src/Rag.Indexer.Worker
```

When running via Aspire, configure the same settings in `src/Rag.Indexer.AppHost/appsettings.json` (or user secrets):

```json
{
  "Indexing": {
    "Repositories": ["C:\\Projects\\MyApp"]
  }
}
```

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

### `POST /api/scan`

Preview which files would be indexed for a repository path. Does **not** modify anything — it runs the scanner and computes the pending delta against the current state file.

```json
{ "repositoryPath": "C:\\Projects\\MyApp" }
```

Returns `totalFiles`, `newOrModified`, `deleted`, `unchanged`, `needsIndexing`, and the relative file list.

### `GET /api/directories`

Browse the filesystem on the API machine (used by the web UI's folder picker). With no `path` (or empty), returns drive roots; otherwise returns the subfolders of `path` plus its parent:

```
GET /api/directories?path=C:\Projects
```

### `GET /api/repositories`

The repositories configured for the indexer worker (`Indexing:Repositories`), so the UI can offer them as quick choices.

### `GET /api/collections`

Lists all collections in the vector store. Individual collection details (status, points, segments, vector size, distance) are available at `GET /api/collections/{name}`.

### `GET /api/logs`

Recent entries from the in-memory log stream. Optional filters: `?source=` (substring match) and `?limit=` (most recent N entries).

---

## MCP Server (`Rag.Indexer.Mcp`)

The solution includes a standalone **MCP (Model Context Protocol) server** that exposes the RAG indexer as an AI-assistant tool. Any MCP-compatible client — such as VS Code with GitHub Copilot — can call `GetContext` to perform semantic code lookups without custom integration code.

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
| **stdio** *(default)* | Local AI clients that connect via stdin/stdout pipes (VS Code with GitHub Copilot, etc.) | `dotnet run` |
| **HTTP** | Remote clients connecting over a network | `dotnet run -- --transport=http` |

> When launched from the Aspire AppHost, the MCP server always starts in **HTTP** mode (`--transport=http`) so it appears in the dashboard as a resource with a reachable `/mcp` endpoint and emits OpenTelemetry logs/traces.

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

### VS Code / GitHub Copilot setup

1. Create (or edit) `.vscode/mcp.json` in the workspace root:

```json
{
  "servers": {
    "rag-indexer": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["C:\\path\\to\\Rag.Indexer.Mcp.dll"]
    }
  }
}
```

2. Reload the VS Code window (**Developer: Reload Window**).
3. Open **GitHub Copilot Chat**, then open the MCP server list (the **Tools** menu in the chat panel) and confirm `rag-indexer` is active.
4. Ask a question in Copilot Chat — Copilot can call `GetContext` to search the indexed codebase.

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

### AppHost `.env` file

The AppHost loads an optional `.env` file from `src/Rag.Indexer.AppHost/` on startup. The file is **git-ignored** (a committed `src/Rag.Indexer.AppHost/.env.example` shows the format), so it is the right place for local secrets.

| Variable | Default | Description |
|----------|---------|-------------|
| `Qdrant:ApiKey` | *(empty)* | Qdrant API key. Empty disables Qdrant authentication so the dashboard doesn't ask for a key. Set a value to enable authentication. |
| `Ollama:BaseUrl` | `http://localhost:11434` | Ollama API endpoint used by the AppHost for embedding configuration |
| `Ollama:Model` | `mxbai-embed-large` | Ollama embedding model used by the AppHost |

### Service endpoints

| Variable | Default | Purpose |
|----------|---------|---------|
| `QDRANT_HOST` | `localhost` | Qdrant host (standalone) |
| `QDRANT_PORT` | `6334` | Qdrant gRPC port (standalone) |
| `QDRANT_REST_PORT` | `6333` | Qdrant REST port, snapshots (standalone) |
| `QDRANT_APIKEY` | *(empty)* | Qdrant API key. Injected automatically by Aspire when running via the AppHost |
| `OLLAMA_BASE_URL` | `http://localhost:11434` | Ollama endpoint (MCP server) |
| `OLLAMA_MODEL` | `mxbai-embed-large` | Ollama embedding model (MCP server) |
| `MCP_TRANSPORT` | `stdio` | MCP transport: `stdio` or `http` |
| `Indexing__Repositories` | *(none)* | Repository folder(s) the indexer worker keeps indexed (array) |
| `Indexing__IntervalSeconds` | `300` | How often the worker re-runs delta indexing |

> **CORS (web UI):** the API enables a `rag-web` CORS policy that allows any origin in development (`builder.Environment.IsDevelopment()`), which covers the Angular dev server on `http://localhost:4200`.

### `.ragignore`

Place a `.ragignore` file at the root of the repository being indexed. It uses gitignore-style glob patterns matched against the full file set (`**/` semantics), applied by the scanner before indexing:

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

Patterns that do not start with `**/` or `/` are treated as if prefixed with `**/` (matching at any depth), mirroring gitignore behavior.

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
4. If the `_code` collection is deleted, the stale state file is discarded and a full re-index runs.

---

## License

UNLICENSED – Internal / private project.
