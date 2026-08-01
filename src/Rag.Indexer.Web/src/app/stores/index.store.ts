import { inject } from '@angular/core';
import {
  patchState,
  signalStore,
  withHooks,
  withMethods,
  withState
} from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';

import type { LogEntry, ScanResponse } from '../core/models/rag.models';
import { RagApiService } from '../core/services/rag-api.service';
import { toErrorMessage } from '../core/utils/errors';

interface IndexState {
  repositoryPath: string;
  scanning: boolean;
  scanResult: ScanResponse | null;
  indexing: boolean;
  message: string | null;
  error: string | null;
  logs: LogEntry[];
  recentPaths: string[];
  configuredRepositories: string[];
}

const initialState: IndexState = {
  repositoryPath: '',
  scanning: false,
  scanResult: null,
  indexing: false,
  message: null,
  error: null,
  logs: [],
  recentPaths: [],
  configuredRepositories: []
};

const RECENT_PATHS_KEY = 'rag-indexer.recent-paths';
const RECENT_PATHS_LIMIT = 10;

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
        patchState(store, {
          repositoryPath,
          scanResult: null,
          message: null,
          error: null
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
          patchState(store, { configuredRepositories: response.repositories });
        } catch {
          // silent — autocomplete is a convenience, never surface transient errors
        }
      },
      async loadLogs(limit = 200) {
        try {
          const response = await firstValueFrom(api.getLogs(undefined, limit));
          patchState(store, { logs: response.entries });
        } catch {
          // silent — polling must never surface transient errors
        }
      }
    };
  }),
  withMethods((store) => {
    const api = inject(RagApiService);

    return {
      async indexRepository() {
        const path = store.repositoryPath().trim();
        if (!path) {
          patchState(store, { error: 'Repository path is required.' });
          return;
        }
        patchState(store, { indexing: true, message: null, error: null });
        try {
          const response = await firstValueFrom(api.indexRepository(path));
          store.rememberPath();
          patchState(store, { indexing: false, message: response.message });
          await store.loadLogs(200);
        } catch (err) {
          patchState(store, { indexing: false, error: toErrorMessage(err) });
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
      return () => clearInterval(timer);
    }
  })
);
