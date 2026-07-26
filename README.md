# UEFA.Rag.Indexer

A **.NET 10 console application** that indexes source code repositories into a vector database for Retrieval-Augmented Generation (RAG) workflows. It parses C# code into semantic chunks (classes and methods), generates vector embeddings via Ollama or Azure OpenAI, and stores them in Qdrant for semantic search.

---

## What's New

### v6 — Graceful Configuration Handling

| Feature | Description |
|---------|-------------|
| **Optional `appsettings.json`** | The application no longer crashes if `appsettings.json` is missing. Falls back to environment variables and defaults. |
| **Malformed JSON Recovery** | If `appsettings.json` contains invalid JSON, the error is displayed and the app falls back to environment variables and defaults. |
| **Graceful Config Degradation** | All `EmbeddingOptions` have sensible defaults (`ollama` provider, `localhost:11434` base URL), so indexing can proceed without any configuration file. |
| **Environment Variable Priority** | Environment variables (via `__` separator, e.g. `Embedding__Provider`) always override JSON values, matching standard .NET configuration behavior. |

### v5 — Configurable Embedding Providers

| Feature | Description |
|---------|-------------|
| **Provider Abstraction** | `IEmbeddingService` interface decouples indexing from any specific embedding backend. |
| **Ollama (Default)** | `OllamaEmbeddingService` — local inference via `localhost:11434`. |
| **Azure OpenAI** | `AzureOpenAiEmbeddingService` — cloud embeddings via REST API. |
| **JSON Configuration** | `appsettings.json` with `Embedding` section. Provider selected via `Embedding:Provider` (`"ollama"` or `"azure"`). |
| **Strongly-Typed Options** | `EmbeddingOptions`, `OllamaOptions`, `AzureOpenAiOptions` POCOs in `Services/Configuration/`. |
| **Env Var Overlay** | `IConfiguration` builder reads `appsettings.json` + environment variables (standard .NET pattern). |
| **Empty Content Skip** | Empty `version.json` / `README.md` files are skipped before sending to the embedding service. |
| **Empty Embedding Guard** | `OllamaEmbeddingService` now throws a descriptive error instead of `IndexOutOfRangeException`. |
| **State File Exclusion** | `.ragindex-state.json` is excluded from the scan to prevent cyclic re-indexing. |

### v4 — Qdrant-Only Compose

| Feature | Description |
|---------|-------------|
| **Podman Compose** | `podman-compose.yml` runs only Qdrant — no indexer container. The console app connects from the host via `localhost:6334`. |
| **Lighter Workflow** | No need to build a container image or mount volumes for the indexer. Just `podman compose up -d` and run `dotnet run` on the host. |

### v2 — Incremental Indexing & Live Log Stream

| Feature | Description |
|---------|-------------|
| **Incremental (Delta) Indexing** | Only new, modified, or deleted files are re-processed on each run. Unchanged files are skipped entirely — no wasted parsing, embedding, or Qdrant writes. |
| **Interrupt-Resilient** | State is saved after every file. If you Ctrl+C or crash mid-way, the next run picks up exactly where you left off. |
| **Collection Deletion Detection** | If you delete the Qdrant collection (e.g. via "Clean"), the stale state file is automatically discarded and a full re-index is performed. |
| **Deterministic Chunk IDs** | Each chunk's Qdrant UUID is derived from its content (SHA-256). Same content → same UUID → upsert naturally deduplicates. No duplicate points. |
| **Live Log Stream** | Real-time log viewer with level filtering (Debug/Info/Warning/Error). Shows buffered history + live events. Press Q or Esc to stop. |
| **Interactive Menu** | Full console UI with arrow-key navigation, status spinners, and graceful Ctrl+C handling. |

---

## Architecture Overview

```
┌──────────────────────────────────────────────────────────────────────────────────┐
│  Program.cs (Interactive Menu)                                                    │
│  ┌───────────────────────────────────────────────────────────────────────────┐   │
│  │  IndexingService                                                           │   │
│  │  ┌────────────────┐  ┌──────────────┐  ┌────────────────┐  ┌───────────┐ │   │
│  │  │ RepositoryScan │→│ CSharpCode   │→│ IEmbeddingService│→│ Qdrant    │ │   │
│  │  │ ner            │  │ Parser       │  │ ┌────────────┐ │  │ Service   │ │   │
│  │  └────────────────┘  └──────────────┘  │ │ Ollama     │ │  └───────────┘ │   │
│  │                                         │ │ AzureOpenAI│ │                 │   │
│  │  ┌───────────────────────────────────────┘ └────────────┘ │                 │   │
│  │  │                                                         │                 │   │
│  │  │  IndexStateManager (delta detection via .ragindex-state.json)           │   │
│  │  └─────────────────────────────────────────────────────────────────────────┘   │
│  │                                                                                 │
│  │  LogStream (singleton, event-driven, in-memory log buffer)                     │
│  └───────────────────────────────────────────────────────────────────────────┘   │
│                                                                                   │
│  SearchService (standalone query tool, not wired in Program.cs yet)               │
└──────────────────────────────────────────────────────────────────────────────────┘
```

---

## Features

- **Interactive Console UI** – Arrow-key menu navigation, status spinners, graceful exit on Ctrl+C or Esc.
- **Pluggable Embeddings** – `IEmbeddingService` abstraction with Ollama (local) and Azure OpenAI (cloud) implementations.
- **JSON Configuration** – `appsettings.json` with environment variable overlay. Standard .NET options pattern.
- **Optional Configuration** – `appsettings.json` is optional. Falls back to environment variables and defaults if missing or malformed.
- **Incremental (Delta) Indexing** – Only processes files that have changed since the last run. State tracked via `.ragindex-state.json`.
- **Interrupt-Resilient** – State saved after every file. Crash recovery resumes from the last fully-processed file.
- **Repository Scanning** – Recursively walks a folder, filtering by supported extensions and respecting `.ragignore` patterns.
- **C# Semantic Parsing** – Uses Roslyn (`Microsoft.CodeAnalysis.CSharp`) to parse `.cs` files into two levels of granularity: **class** definitions and **method** definitions.
- **Document Indexing** – Non-C# files (`.csproj`, `.json`, `.yaml`, `.md`, `.sql`, `.xml`, `.config`, `.sln`) are indexed as whole-document chunks.
- **Vector Embedding** – Calls Ollama (`mxbai-embed-large`) or Azure OpenAI (`text-embedding-ada-002`) to generate float embeddings.
- **Vector Storage** – Stores embeddings along with rich metadata (project, file path, namespace, symbol type/name, source content) in [Qdrant](https://qdrant.tech/) at `localhost:6334` (gRPC).
- **Live Log Stream** – Real-time log viewer with level filtering. Shows buffered history + live events via event subscription.
- **Semantic Search** – `SearchService` accepts a natural-language query, embeds it, and returns the top-5 most similar code chunks from Qdrant.

---

## Data Model

### `CodeChunk` (in `Models/CodeChunk.cs`)

| Property       | Type           | Description                                                    |
|----------------|----------------|----------------------------------------------------------------|
| `Id`           | `string`       | Deterministic UUID derived from content (SHA-256 → first 16 bytes as GUID) |
| `Project`      | `string`       | Top-level folder name under the scanned root                   |
| `FilePath`     | `string`       | Full path to the source file                                   |
| `Namespace`    | `string`       | .NET namespace (extracted for C# files)                        |
| `SymbolType`   | `string`       | `"class"`, `"method"`, or `"document"`                          |
| `SymbolName`   | `string`       | Name of the class, method, or file                              |
| `ParentSymbol` | `string?`      | _(reserved)_ Parent symbol name                                 |
| `Usings`       | `List<string>` | _(reserved)_ Using directives                                   |
| `Attributes`   | `List<string>` | _(reserved)_ Custom attributes                                  |
| `Dependencies` | `List<string>` | _(reserved)_ Inferred dependencies                              |
| `Content`      | `string`       | Full source text of the chunk                                   |

> **Note:** `Id` is no longer a random GUID. It is deterministically computed from `filePath::symbolType::symbolName::content` via SHA-256. This ensures the same chunk always maps to the same Qdrant point, enabling upsert-based deduplication during delta indexing.

### `IndexState` (in `Models/IndexState.cs`)

| Property  | Type                          | Description                                    |
|-----------|-------------------------------|------------------------------------------------|
| `Version` | `int`                         | State file format version                      |
| `Files`   | `Dictionary<string, FileState>` | Maps file paths to their content hashes + timestamps |

### `LogEntry` (in `Models/LogEntry.cs`)

| Property    | Type       | Description                              |
|-------------|------------|------------------------------------------|
| `Timestamp` | `DateTime` | When the log entry was created           |
| `Level`     | `LogLevel` | Debug, Info, Warning, Error              |
| `Source`    | `string`   | Component that emitted the log           |
| `Message`   | `string`   | Log message text                         |
| `Formatted` | `string`   | Pre-formatted string for display         |

---

## Project Structure

```
UEFA.Rag.Indexer/
├── appsettings.json                 # Application configuration (embedding provider, etc.)
├── Dockerfile                       # Multi-stage container build
├── podman-compose.yml               # Podman Compose orchestration (Qdrant + indexer)
├── Program.cs                       # Entry point (interactive menu)
├── UEFA.Rag.Indexer.csproj          # .NET 10 project file
├── .ragignore                       # Ignore patterns (gitignore-style)
├── .ragindex-state.json             # Auto-generated index state (do not commit)
├── README.md                        # This file
├── Models/
│   ├── CodeChunk.cs                 # Chunk data model
│   ├── IndexState.cs                # Index state data model
│   └── LogEntry.cs                  # Structured log entry model
└── Services/
    ├── Configuration/
    │   └── EmbeddingOptions.cs      # Strongly-typed options POCOs
    ├── AzureOpenAiEmbeddingService.cs  # Azure OpenAI embedding client
    ├── IEmbeddingService.cs         # Embedding service abstraction
    ├── OllamaEmbeddingService.cs    # Ollama embedding client
    ├── RepositoryScanner.cs         # File discovery
    ├── RagIgnore.cs                 # .ragignore pattern matching
    ├── CSharpCodeParser.cs          # Roslyn-based C# parser
    ├── EmbeddingService.cs          # (removed) — replaced by IEmbeddingService
    ├── QdrantService.cs             # Qdrant vector DB client
    ├── IndexingService.cs           # Orchestration pipeline (delta-aware)
    ├── IndexStateManager.cs         # State file management & delta computation
    ├── SearchService.cs             # Semantic search
    ├── LogStream.cs                 # Singleton in-memory log buffer with event subscription
    └── ...
```

---

## Pipeline Flow

### Indexing (`IndexingService.IndexAsync`)

1. **Scan** – `RepositoryScanner` enumerates all files under the root folder with supported extensions (excluding the state file `.ragindex-state.json`).
2. **Filter** – `RagIgnore` excludes files matching patterns defined in `.ragignore`.
3. **Load State** – `IndexStateManager` reads `.ragindex-state.json` (if it exists).
4. **Compute Delta** – Compares current files against the state to find new, modified, and deleted files.
5. **Remove Deleted** – For each deleted file, its Qdrant points are removed via `DeleteByFilePathAsync`.
6. **Process Changes** – For each new/modified file:
   - Old points are removed (if re-indexing a modified file).
   - **C# files** (`.cs`) → `CSharpCodeParser` extracts every **class** and its **methods** as separate chunks.
   - **Other files** → a single `"document"` chunk with the full file content.
   - Empty chunks are skipped.
   - Each chunk gets a deterministic ID via `ComputeDeterministicId()`.
   - Each chunk's `Content` is sent to the configured `IEmbeddingService` to produce a `float[]` vector.
   - The vector and payload are upserted into the Qdrant collection `uefa_code`.
   - State is saved after every file (interrupt-resilient).
7. **Final Save** – State file is saved one last time.

### Search (`SearchService.SearchAsync`)

1. **Query Embedding** – The user's natural-language query is sent to the configured embedding service to generate an embedding.
2. **Vector Search** – The embedding is searched against the `uefa_code` collection in Qdrant (top-5 results).
3. **Display** – Results are printed with similarity score and full payload metadata.

---

## Dependencies

| Package                                   | Version   | Purpose                         |
|-------------------------------------------|-----------|---------------------------------|
| `Microsoft.CodeAnalysis.CSharp`           | 5.6.0     | C# syntax parsing (Roslyn)      |
| `Microsoft.Extensions.Configuration.Binder` | 10.0.10 | Configuration binding           |
| `Microsoft.Extensions.Configuration.EnvironmentVariables` | 10.0.10 | Env var config overlay |
| `Microsoft.Extensions.Configuration.Json` | 10.0.10   | JSON configuration reader       |
| `Microsoft.Extensions.FileSystemGlobbing` | 10.0.10   | `.ragignore` glob pattern matching |
| `Qdrant.Client`                           | 1.18.1    | gRPC client for Qdrant          |
| `Spectre.Console`                         | 0.57.2    | Interactive console UI          |

### External Services

| Service      | Endpoint             | Purpose                        |
|--------------|----------------------|--------------------------------|
| **Ollama**   | `localhost:11434`    | Embedding inference (default, `mxbai-embed-large`) |
| **Azure OpenAI** | (configurable)  | Embedding inference (`text-embedding-ada-002` or custom) |
| **Qdrant**   | `localhost:6334`     | Vector database (gRPC)         |

---

## Configuration

### `appsettings.json`

`appsettings.json` is **optional**. If missing or malformed, the application falls back to environment variables and defaults. The `Embedding` section controls which provider is used:

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

Switch to Azure OpenAI by changing the provider and filling in the credentials:

```json
{
  "Embedding": {
    "Provider": "azure",
    "AzureOpenAi": {
      "Endpoint": "https://your-resource.openai.azure.com",
      "Key": "your-api-key",
      "DeploymentName": "text-embedding-ada-002"
    }
  }
}
```

Environment variables override JSON keys using the `__` (double underscore) separator. For example:

```powershell
$env:Embedding__Provider = "azure"
$env:Embedding__AzureOpenAi__Endpoint = "https://your-resource.openai.azure.com"
$env:Embedding__AzureOpenAi__Key = "your-api-key"
```

### `.ragignore`

Place a `.ragignore` file at the root of the repository being indexed. It follows the same pattern syntax as `.gitignore` (comments with `#`, glob patterns). Example:

```
# Build artifacts
bin/
obj/

# Dependencies
node_modules/
packages/

# Generated code
*.Designer.cs
*.g.cs

# Secrets
appsettings.Development.json
```

### `.ragindex-state.json`

This file is auto-generated in the root folder being indexed. It tracks content hashes and timestamps for every indexed file. **Do not commit this file** — it is already in `.gitignore`.

Delete it if you want to force a full re-index on the next run.

### Supported File Extensions

Defined in `IndexingService.cs`:

`.cs`, `.csproj`, `.sln`, `.json`, `.yaml`, `.yml`, `.xml`, `.config`, `.sql`, `.md`

---

## Prerequisites (Windows)

The application depends on three external components. Below are step-by-step instructions for installing each one on Windows.

### 1. .NET 10 SDK

The project targets `net10.0`. Install the SDK via **winget** (included with Windows 11 / App Installer):

```powershell
winget install Microsoft.DotNet.SDK.10
```

Alternatively, download the installer from the [official .NET 10 download page](https://dotnet.microsoft.com/download/dotnet/10.0).

Verify the installation:

```powershell
dotnet --list-sdks
```

### 2. Ollama (Embedding Inference — Default Provider)

Ollama runs as a local service and serves the embedding model over HTTP.

**Install via winget:**

```powershell
winget install Ollama.Ollama
```

This installs Ollama and registers it to start automatically as a background service. After installation, pull the embedding model:

```powershell
ollama pull mxbai-embed-large
```

Verify the service is running:

```powershell
curl http://localhost:11434/api/version
```

### 3. Qdrant (Vector Database)

Qdrant is a vector search engine. The application connects via gRPC on port **6334**.

**Option A — Docker (recommended):**

If Docker Desktop for Windows is installed:

```powershell
docker run -d --name qdrant -p 6333:6333 -p 6334:6334 qdrant/qdrant
```

- Port `6333` → REST API (health checks, UI).
- Port `6334` → gRPC interface (used by this application).

**Option B — Windows native binary:**

1. Download the latest Windows release from [Qdrant releases](https://github.com/qdrant/qdrant/releases).
2. Extract the archive.
3. Run from PowerShell:

   ```powershell
   .\qdrant.exe
   ```

   By default it listens on `localhost:6334` for gRPC.

> The application automatically creates the `uefa_code` collection on first run if it doesn't exist.

---

## Getting Started

### Clone & Build

```powershell
# Clone the repository
git clone <repo-url>
cd UEFA.Rag.Indexer

# Restore dependencies and build
dotnet build
```

### Run the Indexer

```powershell
dotnet run
```

This launches the interactive menu:

```
┌──────────────────────────────────┐
│     UEFA RAG Indexer             │
├──────────────────────────────────┤
│  Run    - Index a repository     │
│  List   - List all collections   │
│  Info   - Collection details     │
│  Clean  - Delete the collection  │
│  Exit                            │
└──────────────────────────────────┘
```

You can also use CLI arguments for non-interactive use:

```powershell
# Index a repository (uses configured embedding provider)
dotnet run -- run "C:\Projects\MyApp"

# List Qdrant collections
dotnet run -- list

# Show collection details
dotnet run -- info

# Delete the Qdrant collection
dotnet run -- clean
```

### First Run (Full Index)

```
Scanning: C:\Projects\MyApp
Processing: C:\Projects\MyApp\src\Services\IndexingService.cs
  Indexed class: IndexingService
  Indexed method: IndexAsync
  Indexed method: ParseCSharp
  Indexed method: ParseText
  Indexed method: GetProjectName
  Indexed method: IsSupportedFile
Processing: C:\Projects\MyApp\README.md
  Indexed document: README.md
...
Completed. Indexed 142 chunks (50 files processed, 0 files removed, 0 files unchanged).
```

### Second Run (Delta — No Changes)

```
Completed. Indexed 0 chunks (0 files processed, 0 files removed, 50 files unchanged).
```

### After Editing a File

```
Processing: C:\Projects\MyApp\src\Services\IndexingService.cs
  Indexed class: IndexingService
  Indexed method: IndexAsync
  ...
Completed. Indexed 6 chunks (1 file processed, 0 files removed, 49 files unchanged).
```

### Live Log Stream

Select "Logs" from the menu, choose a minimum log level, and watch real-time log output. Press Q or Esc to stop and return to the menu.

---

## Qdrant via Podman Compose

The `podman-compose.yml` runs **only Qdrant** in a container, letting the console app connect from the host. No container image to build, no volume mounts for source code.

### Prerequisites

| Tool | Purpose | Install |
|------|---------|---------|
| **Podman** | Container engine | [podman.io](https://podman.io/docs/installation) |
| **Ollama** (or Azure OpenAI) | Embedding service | See configuration section above |

### Start Qdrant

```bash
podman compose up -d
```

Qdrant is now available at:
- `localhost:6333` — REST API (health checks, UI)
- `localhost:6334` — gRPC (used by the indexer)

### Stop Qdrant

```bash
podman compose down
```

### Topology

```
┌─────────────────────────────────────┐
│  Host                                │
│                                      │
│  ┌──────────────┐                   │
│  │  Qdrant       │                   │
│  │  :6333 (REST) │                   │
│  │  :6334 (gRPC) │◄── ─┐            │
│  └──────────────┘      │            │
│                         │            │
│  ┌──────────────────┐   │            │
│  │  .NET 10 Console │───┘            │
│  │  (dotnet run)    │                │
│  └──────────────────┘                │
│                                      │
│  Embedding Provider (one of):        │
│  ┌──────────────────┐                │
│  │  Ollama           │  localhost:11434│
│  │  or Azure OpenAI  │  cloud         │
│  └──────────────────┘                │
└─────────────────────────────────────┘
```

---

## How Incremental Indexing Works

1. **State file** (`.ragindex-state.json`) stores a SHA-256 hash for each indexed file.
2. On each run, the indexer computes the delta:
   - **New files** → not in state → indexed.
   - **Modified files** → hash mismatch → old points deleted → re-indexed.
   - **Deleted files** → in state but not on disk → points removed from Qdrant.
   - **Unchanged files** → hash matches → skipped entirely.
3. State is saved **after every file**, so Ctrl+C or a crash only loses the current file's work.
4. If the Qdrant collection is deleted (e.g. via "Clean"), the state file is automatically discarded and a full re-index is performed.
5. The state file itself (`.ragindex-state.json`) is excluded from the scan to prevent cyclic re-indexing.

---

## Future / Reserved Fields

The `CodeChunk` model includes several fields that are reserved but not yet populated:

- `ParentSymbol` – For nesting methods under their parent class in the payload.
- `Usings` – Extracted using directives.
- `Attributes` – Extracted custom attributes on symbols.
- `Dependencies` – Inferred symbol dependencies (e.g., references to other classes).

These are stubbed for future enrichment of the index.

---

## License

UNLICENSED – Internal / private project.