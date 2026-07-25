# UEFA.Rag.Indexer

A **.NET 10 console application** that indexes source code repositories into a vector database for Retrieval-Augmented Generation (RAG) workflows. It parses C# code into semantic chunks (classes and methods), generates vector embeddings via Ollama, and stores them in Qdrant for semantic search.

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────────┐
│  Program.cs                                                             │
│  ┌──────────────────────────────────────────────────────────────────┐   │
│  │  IndexingService                                                  │   │
│  │  ┌────────────────┐  ┌──────────────┐  ┌────────────────┐       │   │
│  │  │ RepositoryScan │→│ CSharpCode   │→│ EmbeddingService│       │   │
│  │  │ ner            │  │ Parser       │  │ (Ollama)       │       │   │
│  │  └────────────────┘  └──────────────┘  └───────┬────────┘       │   │
│  │                                                 │                │   │
│  │                                                 ▼                │   │
│  │                                        ┌────────────────┐       │   │
│  │                                        │ QdrantService  │       │   │
│  │                                        │ (Vector DB)    │       │   │
│  │                                        └────────────────┘       │   │
│  └──────────────────────────────────────────────────────────────────┘   │
│                                                                         │
│  SearchService (standalone query tool, not wired in Program.cs yet)     │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## Features

- **Repository Scanning** – Recursively walks a folder, filtering by supported extensions and respecting `.ragignore` patterns.
- **C# Semantic Parsing** – Uses Roslyn (`Microsoft.CodeAnalysis.CSharp`) to parse `.cs` files into two levels of granularity: **class** definitions and **method** definitions.
- **Document Indexing** – Non-C# files (`.csproj`, `.json`, `.yaml`, `.md`, `.sql`, `.xml`, `.config`, `.sln`) are indexed as whole-document chunks.
- **Vector Embedding** – Calls [Ollama](https://ollama.com/) hosted at `localhost:11434` using the `mxbai-embed-large` model to generate float embeddings.
- **Vector Storage** – Stores embeddings along with rich metadata (project, file path, namespace, symbol type/name, source content) in [Qdrant](https://qdrant.tech/) at `localhost:6334` (gRPC).
- **Semantic Search** – `SearchService` accepts a natural-language query, embeds it via Ollama, and returns the top-5 most similar code chunks from Qdrant.

---

## Data Model

**`CodeChunk`** (in `Models/CodeChunk.cs`):

| Property       | Type           | Description                                                    |
|----------------|----------------|----------------------------------------------------------------|
| `Id`           | `string`       | Unique identifier (auto-generated GUID)                        |
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

**Qdrant Payload:** Each point stores a 1-to-1 mapping of the `CodeChunk` fields except `Usings`, `Attributes`, `Dependencies` (reserved for future use).

---

## Project Structure

```
UEFA.Rag.Indexer/
├── Program.cs                     # Entry point
├── UEFA.Rag.Indexer.csproj        # .NET 10 project file
├── .ragignore                     # Ignore patterns (gitignore-style)
├── README.md                      # This file
├── Models/
│   └── CodeChunk.cs               # Chunk data model
└── Services/
    ├── RepositoryScanner.cs       # File discovery
    ├── RagIgnore.cs               # .ragignore pattern matching
    ├── CSharpCodeParser.cs        # Roslyn-based C# parser
    ├── EmbeddingService.cs        # Ollama embedding client
    ├── QdrantService.cs           # Qdrant vector DB client
    ├── IndexingService.cs         # Orchestration pipeline
    └── SearchService.cs           # Semantic search
```

---

## Pipeline Flow

### Indexing (`IndexingService.IndexAsync`)

1. **Scan** – `RepositoryScanner` enumerates all files under the root folder with supported extensions (`.cs`, `.csproj`, `.sln`, `.json`, `.yaml`, `.yml`, `.xml`, `.config`, `.sql`, `.md`).
2. **Filter** – `RagIgnore` excludes files matching patterns defined in `.ragignore`.
3. **Parse** – For each file:
   - **C# files** (`.cs`) → `CSharpCodeParser` extracts every **class** and its **methods** as separate chunks.
   - **Other files** → a single `"document"` chunk with the full file content.
4. **Embed** – Each chunk's `Content` is sent to Ollama's `/api/embed` endpoint to produce a `float[]` vector.
5. **Store** – The vector and associated payload are upserted into the Qdrant collection `uefa_code`.

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

> **Note:** The Ollama endpoint is hardcoded in `EmbeddingService.cs` and `SearchService.cs` as `http://localhost:11434`. If you change the port, update those files accordingly.

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

**Create the collection (required before first index run):**

The application expects a collection named `uefa_code` with vectors of dimension **1024** (the output size of `mxbai-embed-large`). Use the REST API to create it:

```powershell
curl -X PUT http://localhost:6333/collections/uefa_code `
  -H "Content-Type: application/json" `
  -d '{"vectors": {"size": 1024, "distance": "Cosine"}}'
```

> If the collection does not exist, `QdrantService.InsertAsync` will fail. Run this command once before the first indexing session.

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

The indexer accepts a target folder path as the first argument. If omitted, it defaults to `C:\UEFA`.

```powershell
# Index a repository at C:\Projects\MyApp
dotnet run -- "C:\Projects\MyApp"

# Or use the default path
dotnet run
```

During indexing, the console prints each file being processed and each chunk being upserted:

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
Completed. Indexed 142 chunks.
```

### Run a Semantic Search

The `SearchService` is a standalone component (not wired into `Program.cs` yet). To test search functionality, create a temporary script or use the .NET Interactive console:

```powershell
# Example: quick search via dotnet-script or a temporary console snippet
# (SearchService is ready for integration in your own entry point)
```

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

### Supported File Extensions

Defined in `RepositoryScanner.cs`:

`.cs`, `.csproj`, `.sln`, `.json`, `.yaml`, `.yml`, `.xml`, `.config`, `.sql`, `.md`

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