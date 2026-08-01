import { computed, inject } from '@angular/core';
import {
  patchState,
  signalStore,
  withComputed,
  withMethods,
  withState
} from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';

import type { SearchResult } from '../core/models/rag.models';
import { RagApiService } from '../core/services/rag-api.service';
import { toErrorMessage } from '../core/utils/errors';

interface SearchState {
  query: string;
  limit: number;
  results: SearchResult[];
  aggregatedContext: string | null;
  showContext: boolean;
  loading: boolean;
  error: string | null;
}

const initialState: SearchState = {
  query: '',
  limit: 5,
  results: [],
  aggregatedContext: null,
  showContext: false,
  loading: false,
  error: null
};

export const SearchStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withComputed((store) => ({
    hasResults: computed(() => store.results().length > 0)
  })),
  withMethods((store) => {
    const api = inject(RagApiService);

    const fetchResults = async (): Promise<SearchResult[]> => {
      const query = store.query().trim();
      if (!query) {
        patchState(store, { results: [], aggregatedContext: null });
        return [];
      }
      patchState(store, { loading: true, error: null });
      try {
        const response = await firstValueFrom(api.search(query, store.limit()));
        patchState(store, { results: response.results, loading: false });
        return response.results;
      } catch (err) {
        patchState(store, { error: toErrorMessage(err), loading: false });
        return [];
      }
    };

    return {
      setQuery(query: string) {
        patchState(store, { query });
      },
      setLimit(limit: number) {
        patchState(store, { limit });
      },
      search: fetchResults,
      async getContext() {
        const query = store.query().trim();
        if (!query) return;
        patchState(store, { loading: true, error: null });
        try {
          const response = await firstValueFrom(api.getContext(query, store.limit()));
          patchState(store, {
            results: response.results,
            aggregatedContext: response.aggregatedContext,
            showContext: true,
            loading: false
          });
        } catch (err) {
          patchState(store, { error: toErrorMessage(err), loading: false });
        }
      }
    };
  }),
  withMethods((store) => ({
    async toggleContext() {
      if (store.showContext()) {
        patchState(store, { showContext: false });
        return;
      }
      await store.getContext();
    },
    async copyContext() {
      const context = store.aggregatedContext();
      if (!context) return;
      try {
        await navigator.clipboard.writeText(context);
      } catch {
        // clipboard unavailable (e.g. non-secure context) — ignore
      }
    }
  }))
);
