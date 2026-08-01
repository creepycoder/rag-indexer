import { Component, computed, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { firstValueFrom } from 'rxjs';

import { IndexStore } from '../../stores/index.store';
import { FolderPicker } from './folder-picker/folder-picker';

@Component({
  selector: 'app-index',
  imports: [
    FormsModule,
    MatAutocompleteModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatTooltipModule
  ],
  templateUrl: './index.html',
  styleUrl: './index.scss'
})
export class Index {
  private readonly dialog = inject(MatDialog);

  protected readonly store = inject(IndexStore);

  protected readonly pathOptions = computed(() => {
    const value = this.store.repositoryPath().trim().toLowerCase();
    const seen = new Set<string>();
    const options: string[] = [];

    for (const path of [...this.store.configuredRepositories(), ...this.store.recentPaths()]) {
      const key = path.toLowerCase();
      if (!path || seen.has(key)) continue;
      seen.add(key);
      if (!value || key.includes(value)) options.push(path);
    }

    return options.slice(0, 10);
  });

  protected async openFolderPicker(): Promise<void> {
    const result = await firstValueFrom(
      this.dialog.open(FolderPicker, {
        width: '580px',
        data: { initialPath: this.store.repositoryPath() }
      }).afterClosed()
    );

    if (result) {
      this.store.setRepositoryPath(result);
    }
  }

  protected levelClass(level: string): string {
    return `level-${level.toLowerCase()}`;
  }
}
