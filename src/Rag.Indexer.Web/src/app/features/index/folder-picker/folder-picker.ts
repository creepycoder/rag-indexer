import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { firstValueFrom } from 'rxjs';
import { NgScrollbarModule } from 'ngx-scrollbar';
import { NgScrollbarMatDialog } from 'ngx-scrollbar/mat';

import { toErrorMessage } from '../../../core/utils/errors';
import { RagApiService } from '../../../core/services/rag-api.service';

@Component({
  selector: 'folder-picker',
  imports: [
    MatButtonModule,
    MatDialogModule,
    MatIconModule,
    MatProgressBarModule,
    MatTooltipModule,
    NgScrollbarModule,
    NgScrollbarMatDialog
  ],
  templateUrl: './folder-picker.html',
  styleUrl: './folder-picker.scss'
})
export class FolderPicker {
  protected readonly dialogRef = inject(MatDialogRef<FolderPicker>);
  private readonly data = inject<FolderPickerData | null>(MAT_DIALOG_DATA);
  private readonly api = inject(RagApiService);

  protected readonly currentPath = signal('');
  protected readonly parent = signal<string | null>(null);
  protected readonly directories = signal<string[]>([]);
  protected readonly selected = signal<string | null>(null);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.navigate(this.data?.initialPath?.trim() || null);
  }

  protected async navigate(path: string | null): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    this.selected.set(null);
    try {
      const listing = await firstValueFrom(this.api.listDirectories(path ?? undefined));
      this.currentPath.set(listing.path);
      this.parent.set(listing.parent);
      this.directories.set(listing.directories);
    } catch (err) {
      this.error.set(toErrorMessage(err));
    } finally {
      this.loading.set(false);
    }
  }

  protected select(row: string): void {
    this.selected.set(row);
  }

  protected open(row: string): void {
    this.navigate(row);
  }

  protected goUp(): void {
    if (this.parent()) {
      this.navigate(this.parent());
    }
  }

  protected confirm(): void {
    this.dialogRef.close(this.selected() ?? this.currentPath());
  }
}

export interface FolderPickerData {
  initialPath: string;
}
