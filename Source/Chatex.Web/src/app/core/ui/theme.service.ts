import { DOCUMENT, Injectable, computed, effect, inject, signal } from '@angular/core';

export type ThemePreference = 'light' | 'dark' | 'system';

export const THEME_STORAGE_KEY = 'chatex.theme';

/** Light, dark or follow the OS. The choice is kept per browser; index.html applies it before paint. */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);
  private readonly media = this.document.defaultView?.matchMedia?.('(prefers-color-scheme: dark)');
  private readonly systemDark = signal(this.media?.matches ?? false);

  readonly preference = signal<ThemePreference>(this.load());
  readonly isDark = computed(() => {
    const preference = this.preference();
    return preference === 'dark' || (preference === 'system' && this.systemDark());
  });

  constructor() {
    this.media?.addEventListener?.('change', (event) => this.systemDark.set(event.matches));
    effect(() => {
      const dark = this.isDark();
      this.document.documentElement.classList.toggle('dark', dark);
      this.document
        .querySelector('meta[name="theme-color"]')
        ?.setAttribute('content', dark ? '#18181b' : '#4f46e5');
    });
  }

  set(preference: ThemePreference): void {
    this.preference.set(preference);
    try {
      localStorage.setItem(THEME_STORAGE_KEY, preference);
    } catch {
      // Storage can be blocked; the choice then lasts for this tab only.
    }
  }

  private load(): ThemePreference {
    try {
      const saved = localStorage.getItem(THEME_STORAGE_KEY);
      return saved === 'light' || saved === 'dark' ? saved : 'system';
    } catch {
      return 'system';
    }
  }
}
