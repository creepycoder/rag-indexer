import { Routes } from '@angular/router';

import { Search } from './features/search/search';
import { Index } from './features/index/index';
import { Status } from './features/status/status';

export const routes: Routes = [
  { path: '', component: Search },
  { path: 'index', component: Index },
  { path: 'status', component: Status },
  { path: '**', redirectTo: '' }
];
