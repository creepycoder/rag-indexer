import { inject } from '@angular/core';
import {
  patchState,
  signalStore,
  withHooks,
  withMethods,
  withState
} from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';

import type { CollectionInfo } from '../core/models/rag.models';
import { RagApiService } from '../core/services/rag-api.service';
import { toErrorMessage } from '../core/utils/errors';

interface StatusState {
  collections: string[];
  selectedName: string | null;
  selected: CollectionInfo | null;
  connected: boolean;
  loading: boolean;
  error: string | null;
}

const initialState: StatusState = {
  collections: [],
  selectedName: null,
  selected: null,
  connected: false,
  loading: false,
  error: null
};

export const StatusStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withMethods((store) => {
    const api = inject(RagApiService);

    const selectCollection = async (name: string) => {
      patchState(store, { selectedName: name, loading: true, error: null });
      try {
        const info = await firstValueFrom(api.getCollectionInfo(name));
        patchState(store, { selected: info, connected: true, loading: false });
      } catch (err) {
        patchState(store, { selected: null, loading: false, error: toErrorMessage(err) });
      }
    };

    return {
      async loadCollections() {
        patchState(store, { loading: true, error: null });
        try {
          const response = await firstValueFrom(api.listCollections());
          patchState(store, {
            collections: response.collections,
            connected: true,
            loading: false
          });
          const current = store.selectedName();
          const next =
            current && response.collections.includes(current)
              ? current
              : response.collections[0];
          if (next) {
            await selectCollection(next);
          } else {
            patchState(store, { selected: null, selectedName: null });
          }
        } catch (err) {
          patchState(store, {
            connected: false,
            loading: false,
            error: toErrorMessage(err)
          });
        }
      },
      selectCollection
    };
  }),
  withHooks({
    onInit(store) {
      store.loadCollections();
      const timer = setInterval(() => store.loadCollections(), 15000);
      return () => clearInterval(timer);
    }
  })
);
