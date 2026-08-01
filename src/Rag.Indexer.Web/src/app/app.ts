import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatButtonModule, MatIconModule],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  private static readonly storageKey = 'rag-indexer-theme';

  isDark = false;

  constructor() {
    this.isDark = this.initialTheme();
    this.applyTheme();
  }

  toggleTheme(): void {
    this.isDark = !this.isDark;
    localStorage.setItem(App.storageKey, this.isDark ? 'dark' : 'light');
    this.applyTheme();
  }

  private initialTheme(): boolean {
    const saved = localStorage.getItem(App.storageKey);
    if (saved === 'dark') return true;
    if (saved === 'light') return false;
    return (
      typeof window.matchMedia === 'function' &&
      window.matchMedia('(prefers-color-scheme: dark)').matches
    );
  }

  private applyTheme(): void {
    document.documentElement.classList.toggle('dark', this.isDark);
  }
}
