# UEFA.Rag.Indexer

A **.NET 10 console application** that indexes source code repositories into a vector database for Retrieval-Augmented Generation (RAG) workflows. It parses C# code into semantic chunks (classes and methods), generates vector embeddings via Ollama, and stores them in Qdrant for semantic search.

---

## What's New

### v3 — Containerized with Podman Compose

| Feature | Description |
|---------|-------------|
| **Dockerfile** | Multi-stage build for a tiny runtime image (`.NET 10 AOT-ready` SDK → publish → `aspnet:10.0` runtime). |
| **Podman Compose** | `podman-compose.yml` orchestrates a Qdrant service (with health check) + the indexer container, wired via `depends_on`. |
| **Env‑Var Configuration** | `QDRANT_HOST`, `QDRANT_PORT`, and `OLLAMA_BASE_URL` are read from environment variables. Defaults preserve local development behaviour. |
| **Read-Only Volume** | The repository to index is mounted as `/repo` (read-only) so the indexer never mutates your source tree. |

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
│  │  │ RepositoryScan │→│ CSharpCode   │→│ EmbeddingService│→│ Qdrant    │ │   │
│  │  │ ner            │  │ Parser       │  │ (Ollama)       │  │ Service   │ │   │
│  │  └────────────────┘  └──────────────┘  └───────┬────────┘  └───────────┘ │   │
│  │                                                 │                          │   │
│  │  ┌──────────────────────────────────────────────┘                          │   │
│  │  │                                                                         │   │
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
- **Incremental (Delta) Indexing** – Only processes files that have changed since the last run. State tracked via `.ragindex-state.json`.
- **Interrupt-Resilient** – State saved after every file. Crash recovery resumes from the last fully-processed file.
- **Repository Scanning** – Recursively walks a folder, filtering by supported extensions and respecting `.ragignore` patterns.
- **C# Semantic Parsing** – Uses Roslyn (`Microsoft.CodeAnalysis.CSharp`) to parse `.cs` files into two levels of granularity: **class** definitions and **method** definitions.
- **Document Indexing** – Non-C# files (`.csproj`, `.json`, `.yaml`, `.md`, `.sql`, `.xml`, `.config`, `.sln`) are indexed as whole-document chunks.
- **Vector Embedding** – Calls [Ollama](https://ollama.com/) hosted at `localhost:11434` using the `mxbai-embed-large` model to generate float embeddings.
- **Vector Storage** – Stores embeddings along with rich metadata (project, file path, namespace, symbol type/name, source content) in [Qdrant](https://qdrant.tech/) at `localhost:6334` (gRPC).
- **Live Log Stream** – Real-time log viewer with level filtering. Shows buffered history + live events via event subscription.
- **Semantic Search** – `SearchService` accepts a natural-language query, embeds it via Ollama, and returns the top-5 most similar code chunks from Qdrant.

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
├── Dockerfile                     # Multi-stage container build
├── podman-compose.yml             # Podman Compose orchestration (Qdrant + indexer)
├── Program.cs                     # Entry point (interactive menu)
├── UEFA.Rag.Indexer.csproj        # .NET 10 project file
├── .ragignore                     # Ignore patterns (gitignore-style)
├── .ragindex-state.json           # Auto-generated index state (do not commit)
├── README.md                      # This file
├── Models/
│   ├── CodeChunk.cs               # Chunk data model
│   ├── IndexState.cs              # Index state data model
│   └── LogEntry.cs                # Structured log entry model
└── Services/
    ├── RepositoryScanner.cs       # File discovery
    ├── RagIgnore.cs               # .ragignore pattern matching
    ├── CSharpCodeParser.cs        # Roslyn-based C# parser
    ├── EmbeddingService.cs        # Ollama embedding client
    ├── QdrantService.cs           # Qdrant vector DB client
    ├── IndexingService.cs         # Orchestration pipeline (delta-aware)
    ├── IndexStateManager.cs       # State file management & delta computation
    ├── SearchService.cs           # Semantic search
    ├── LogStream.cs               # Singleton in-memory log buffer with event subscription
    └── ...
```

---

## Pipeline Flow

### Indexing (`IndexingService.IndexAsync`)

1. **Scan** – `RepositoryScanner` enumerates all files under the root folder with supported extensions.
2. **Filter** – `RagIgnore` excludes files matching patterns defined in `.ragignore`.
3. **Load State** – `IndexStateManager` reads `.ragindex-state.json` (if it exists).
4. **Compute Delta** – Compares current files against the state to find new, modified, and deleted files.
5. **Remove Deleted** – For each deleted file, its Qdrant points are removed via `DeleteByFilePathAsync`.
6. **Process Changes** – For each new/modified file:
   - Old points are removed (if re-indexing a modified file).
   - **C# files** (`.cs`) → `CSharpCodeParser` extracts every **class** and its **methods** as separate chunks.
   - **Other files** → a single `"document"` chunk with the full file content.
   - Each chunk gets a deterministic ID via `ComputeDeterministicId()`.
   - Each chunk's `Content` is sent to Ollama's `/api/embed` endpoint to produce a `float[]` vector.
   - The vector and payload are upserted into the Qdrant collection `uefa_code`.
   - State is saved after every file (interrupt-resilient).
7. **Final Save** – State file is saved one last time.

### Search (`SearchService.SearchAsync`)

1. **Query Embedding** – The user's natural-language query is sent to Ollama to generate an embedding.
2. **Vector Search** – The embedding is searched against the `uefa_code` collection in Qdrant (top-5 results).
3. **Display** – Results are printed with similarity score and full payload metadata.

---

## Dependencies

| Package                                   | Version   | Purpose                         |
|-------------------------------------------|-----------|---------------------------------|
| `Microsoft.CodeAnalysis.CSharp`           | 5.6.0     | C# syntax parsing (Roslyn)      |
| `Microsoft.Extensions.FileSystemGlobbing` | 10.0.10   | `.ragignore` glob pattern matching |
| `Qdrant.Client`                           | 1.18.1    | gRPC client for Qdrant          |
| `Spectre.Console`                         | 0.57.2    | Interactive console UI          |

### External Services

| Service      | Endpoint             | Purpose                        |
|--------------|----------------------|--------------------------------|
| **Ollama**   | `localhost:11434`    | Embedding inference (`mxbai-embed-large`) |
| **Qdrant**   | `localhost:6334`     | Vector database (gRPC)         |

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

### 2. Ollama (Embedding Inference)

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

> **Note:** The Ollama endpoint can be configured via the `OLLAMA_BASE_URL` environment variable (defaults to `http://localhost:11434`). Likewise, Qdrant's host and port are configurable via `QDRANT_HOST` and `QDRANT_PORT`.

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
│  Clean  - Delete the collection  │
│  Logs   - View live log stream   │
│  Exit                            │
└──────────────────────────────────┘
```

You can also use CLI arguments for non-interactive use:

```powershell
# Index a repository
dotnet run -- run "C:\Projects\MyApp"

# List Qdrant collections
dotnet run -- list

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

## Containerized App (Podman Compose)

The project includes a `Dockerfile` and a `podman-compose.yml` that orchestrate a **Qdrant** instance together with the indexer, so you never need to install Qdrant directly.

### Prerequisites

| Tool | Purpose | Install |
|------|---------|---------|
| **Podman** | Container engine | [podman.io](https://podman.io/docs/installation) |
| **podman-compose** | Compose orchestration | `pip install podman-compose` |
| **Ollama** | Embedding service (host) | [ollama.com](https://ollama.com/) – must be running on the host with `mxbai-embed-large` pulled |

> The compose file expects Ollama to be accessible from inside the container. On Windows/macOS it uses the special DNS name `host.docker.internal`; on Linux you may need to set `OLLAMA_BASE_URL=http://host.containers.internal:11434` or the host's LAN IP.

### Build the Image

```bash
podman-compose build
```

### Run (Interactive Menu)

```bash
podman-compose up      # or podman-compose up -d for detached mode
```

Attach to the indexer container with:

```bash
podman attach uefa-indexer
```

You'll see the interactive menu. When you select **Run**, enter `/repo` as the folder path (the host directory mapped via the volume).

### Run (One‑Shot, Non‑Interactive)

Specify the repository path via the `REPO_PATH` environment variable and override the command:

```bash
# PowerShell
$env:REPO_PATH = "C:\Projects\MyApp"
podman-compose run --rm indexer dotnet UEFA.Rag.Indexer.dll run /repo
```

```bash
# Linux / macOS
REPO_PATH=/home/user/projects/myapp \
  podman-compose run --rm indexer dotnet UEFA.Rag.Indexer.dll run /repo
```

### Compose Topology

```
┌─────────────────────────────────────────┐
│  Host                                    │
│  ┌──────────────┐    ┌────────────────┐  │
│  │  Qdrant       │    │  Indexer        │  │
│  │  :6333 (REST) │◄───│  ( .NET 10 )   │  │
│  │  :6334 (gRPC) │    │                │  │
│  └──────────────┘    └───────┬─────────┘  │
│                              │ /repo:ro   │
│                  ┌───────────▼──────────┐ │
│                  │  Host source folder  │ │
│                  └──────────────────────┘ │
│                              │            │
│                  ┌───────────▼──────────┐ │
│                  │  Ollama              │ │
│                  │  host.docker.internal│ │
│                  │  :11434              │ │
│                  └──────────────────────┘ │
└─────────────────────────────────────────┘
```

### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `QDRANT_HOST` | `localhost` | Qdrant gRPC hostname |
| `QDRANT_PORT` | `6334` | Qdrant gRPC port |
| `OLLAMA_BASE_URL` | `http://localhost:11434` | Ollama HTTP endpoint |
| `REPO_PATH` | `.` (current dir) | Host path to the repository to index (used by `podman-compose.yml` for the volume mount) |

---

## Configuration

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

Defined in `RepositoryScanner.cs`:

`.cs`, `.csproj`, `.sln`, `.json`, `.yaml`, `.yml`, `.xml`, `.config`, `.sql`, `.md`

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