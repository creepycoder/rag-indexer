import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';

import { StatusStore } from '../../stores/status.store';

@Component({
  selector: 'app-status',
  imports: [
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatProgressBarModule,
    MatSelectModule
  ],
  templateUrl: './status.html',
  styleUrl: './status.scss'
})
export class Status {
  protected readonly store = inject(StatusStore);

  protected statusIcon(status: string): string {
    switch (status.toLowerCase()) {
      case 'green':
        return 'check_circle';
      case 'yellow':
        return 'warning';
      case 'red':
        return 'cancel';
      default:
        return 'help_outline';
    }
  }

  protected statusClass(status: string): string {
    switch (status.toLowerCase()) {
      case 'green':
        return 'status-green';
      case 'yellow':
        return 'status-yellow';
      case 'red':
        return 'status-red';
      default:
        return 'status-grey';
    }
  }
}
