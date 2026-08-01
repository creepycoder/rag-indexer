# Rag.Indexer.Web — Angular Web UI

An **Angular 21** (standalone components, signals, `@ngrx/signals` stores, Angular Material) browser UI for **Rag.Indexer**. It talks to the ASP.NET Core API over HTTP and provides three screens:

- **Index** – pick a repository folder (filesystem browser backed by `GET /api/directories`), preview the pending scan (`POST /api/scan`), and trigger indexing (`POST /api/index`).
- **Search** – semantic search over indexed code (`POST /api/search`) with an aggregated-context view (`POST /api/context`).
- **Status** – live collection info (`GET /api/collections`, `GET /api/collections/{name}`) and the in-memory log stream (`GET /api/logs`).

## Prerequisites

- Node.js 22+ (npm is included).

## Running

### Via Aspire (recommended)

The AppHost starts the UI automatically as the **`rag-web`** resource — it runs `ng serve` on `http://localhost:4200`, waits for the API, and passes it the API endpoint. Just open the dashboard and click the web UI's URL.

### Standalone

```bash
npm install
ng serve
```

The app connects to the API at `apiUrl` from `src/environments/environment.ts` (default `http://localhost:5004`). Point it elsewhere by editing the environment file:

```ts
export const environment = {
  production: false,
  apiUrl: 'http://localhost:5004'
};
```

The API enables CORS for browser origins in development, so the UI works without extra setup when both run locally.

## Commands

| Command | Description |
|---------|-------------|
| `ng serve` | Development server with hot reload on `http://localhost:4200` |
| `ng build` | Production build (output in `dist/`) |
| `ng test` | Unit tests (Vitest) |
| `ng generate component <name>` | Scaffold a new component |

## Layout

```
src/app/
├── core/        # Models, API client (RagApiService), error helpers
├── features/    # index/, search/, status/ screens
├── stores/      # index, search, status state (@ngrx/signals)
├── app.ts       # Root component + routes
└── index.html   # Entry HTML (loads Material Icons font)
```
