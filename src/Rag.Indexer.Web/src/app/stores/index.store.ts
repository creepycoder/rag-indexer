import { inject } from '@angular/core';
import {
  patchState,
  signalStore,
  withHooks,
  withMethods,
  withState
} from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';

import type {
  IndexedRepository,
  IndexProgress,
  LogEntry,
  ScanResponse
} from '../core/models/rag.models';
import { RagApiService } from '../core/services/rag-api.service';
import { toErrorMessage } from '../core/utils/errors';

interface IndexState {
  repositoryPath: string;
  scanning: boolean;
  scanResult: ScanResponse | null;
  indexing: boolean;
  progress: IndexProgress | null;
  message: string | null;
  error: string | null;
  logs: LogEntry[];
  latestLogTimestamp: string | null;
  recentPaths: string[];
  configuredRepositories: string[];
  indexedRepositories: IndexedRepository[];
}

const initialState: IndexState = {
  repositoryPath: '',
  scanning: false,
  scanResult: null,
  indexing: false,
  progress: null,
  message: null,
  error: null,
  logs: [],
  latestLogTimestamp: null,
  recentPaths: [],
  configuredRepositories: [],
  indexedRepositories: []
};

const RECENT_PATHS_KEY = 'rag-indexer.recent-paths';
const RECENT_PATHS_LIMIT = 10;

// Progress is fetched by polling the snapshot endpoint while a run is active.
// This is more robust than a live SSE push (no cross-origin EventSource or
// redirect nuances), and guarantees a "completed" frame is always observed so
// controls are re-enabled even after fast runs.
const PROGRESS_POLL_MS = 1000;
let progressPollTimer: ReturnType<typeof setInterval> | null = null;

function stopProgressPolling() {
  if (progressPollTimer != null) {
    clearInterval(progressPollTimer);
    progressPollTimer = null;
  }
}

function startProgressPolling(poll: () => void, intervalMs = PROGRESS_POLL_MS) {
  stopProgressPolling();
  poll();
  progressPollTimer = setInterval(poll, intervalMs);
}

function readRecentPaths(): string[] {
  try {
    const raw = localStorage.getItem(RECENT_PATHS_KEY);
    if (!raw) return [];
    const parsed = JSON.parse(raw) as unknown;
    return Array.isArray(parsed) ? parsed.filter((x): x is string => typeof x === 'string') : [];
  } catch {
    return [];
  }
}

function writeRecentPaths(paths: string[]): void {
  try {
    localStorage.setItem(RECENT_PATHS_KEY, JSON.stringify(paths));
  } catch {
    // localStorage unavailable — ignore
  }
}

function finishIndexingRun(store: any, summary: string | null) {
  stopProgressPolling();

  const completedPath = store.progress()?.rootFolder?.trim();
  const selectedPath = store.repositoryPath().trim();
  const shouldClearSelection =
    !!completedPath &&
    !!selectedPath &&
    completedPath.localeCompare(selectedPath, undefined, { sensitivity: 'accent' }) === 0;

  patchState(store, {
    indexing: false,
    scanResult: null,
    message: summary,
    error: store.progress()?.errors.length > 0 ? 'Completed with errors — see details.' : null,
    ...(shouldClearSelection ? { repositoryPath: '' } : {})
  });
}

function isCompletedLogEntry(log: LogEntry): boolean {
  return log.message.includes('Completed.');
}

export const IndexStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withMethods((store) => ({
    rememberPath() {
      const path = store.repositoryPath().trim();
      if (!path) return;
      const recentPaths = [
        path,
        ...store.recentPaths().filter((p) => p.toLowerCase() !== path.toLowerCase())
      ].slice(0, RECENT_PATHS_LIMIT);
      writeRecentPaths(recentPaths);
      patchState(store, { recentPaths });
    }
  })),
  withMethods((store) => {
    const api = inject(RagApiService);

    return {
      setRepositoryPath(repositoryPath: string) {
        const progress = store.progress();
        const indexing = store.indexing();
        const allowReset = !indexing || progress == null || !progress.isRunning;

        patchState(store, {
          repositoryPath,
          scanResult: null,
          ...(allowReset
            ? {
                indexing: false,
                progress: null,
                message: null,
                error: null
              }
            : {
                message: null,
                error: null
              })
        });
      },
      async scanRepository() {
        const path = store.repositoryPath().trim();
        if (!path) {
          patchState(store, { error: 'Repository path is required.' });
          return;
        }
        patchState(store, { scanning: true, scanResult: null, message: null, error: null });
        try {
          const response = await firstValueFrom(api.scanRepository(path));
          store.rememberPath();
          patchState(store, { scanning: false, scanResult: response });
        } catch (err) {
          patchState(store, { scanning: false, error: toErrorMessage(err) });
        }
      },
      async loadPathHistory() {
        patchState(store, { recentPaths: readRecentPaths() });
      },
      async loadRepositories() {
        try {
          const response = await firstValueFrom(api.getRepositories());
          patchState(store, {
            configuredRepositories: response.repositories,
            indexedRepositories: response.indexedRepositories
          });
        } catch {
          // silent — autocomplete is a convenience, never surface transient errors
        }
      },
      async loadLogs(limit = 200) {
        try {
          const response = await firstValueFrom(api.getLogs(undefined, limit));
          const logs = [...response.entries].sort(
            (left, right) => Date.parse(right.timestamp) - Date.parse(left.timestamp)
          );

          const latestLogTimestamp = logs[0]?.timestamp ?? store.latestLogTimestamp();
          const lastSeenTimestamp = store.latestLogTimestamp();
          const newLogs = lastSeenTimestamp
            ? logs.filter((log) => Date.parse(log.timestamp) > Date.parse(lastSeenTimestamp))
            : logs;

          patchState(store, { logs, latestLogTimestamp });

          if (store.indexing() && newLogs.some(isCompletedLogEntry)) {
            const completedLog = newLogs.find(isCompletedLogEntry) ?? newLogs[0] ?? null;
            finishIndexingRun(store, completedLog?.message ?? store.progress()?.summary ?? null);
          }
        } catch {
          // silent — polling must never surface transient errors
        }
      }
    };
  }),
  withMethods((store) => {
    const api = inject(RagApiService);

    const indexStore = store as unknown as {
      repositoryPath: () => string;
      indexing: () => boolean;
      rememberPath: () => void;
      pollIndexingProgress: () => Promise<void>;
      handleIndexingProgress: (progress: IndexProgress) => void;
    };

    return {
      indexRepository() {
        const path = indexStore.repositoryPath().trim();
        if (!path) {
          patchState(store, { error: 'Repository path is required.' });
          return;
        }
        patchState(store, { indexing: true, progress: null, message: null, error: null });
        // POST returns immediately; the run continues in the background and we
        // poll the progress snapshot until it reports no longer running.
        api.indexRepository(path).subscribe({
          next: (response) => {
            indexStore.rememberPath();
            patchState(store, { message: response.message });
          },
          error: (err) => {
            stopProgressPolling();
            patchState(store, { indexing: false, error: toErrorMessage(err) });
          }
        });
        startProgressPolling(() => indexStore.pollIndexingProgress());
      },
      async pollIndexingProgress() {
        try {
          const progress = await firstValueFrom(api.getIndexingProgress());
          indexStore.handleIndexingProgress(progress);
        } catch {
          // transient fetch failure — keep polling, the next tick may succeed
        }
      }
    };
  }),
  withMethods((store) => {
    return {
      handleIndexingProgress(progress: IndexProgress) {
        if (progress.isRunning) {
          patchState(store, { indexing: true, progress, error: null });
        } else if (store.indexing()) {
          // Run finished while we were tracking it.
          patchState(store, { progress });
          finishIndexingRun(store, progress.summary ?? null);
        } else {
          patchState(store, { progress });
        }
      }
    };
  }),
  withHooks({
    onInit(store) {
      store.loadPathHistory();
      store.loadRepositories();
      store.loadLogs(200);
      const timer = setInterval(() => store.loadLogs(200), 4000);

      return () => {
        clearInterval(timer);
        stopProgressPolling();
      };
    }
  })
);
