import { TestBed } from '@angular/core/testing';
import { THEME_STORAGE_KEY, ThemeService } from './theme.service';

describe('ThemeService', () => {
  beforeEach(() => {
    localStorage.clear();
    document.documentElement.classList.remove('dark');
  });

  it('follows the system by default', () => {
    const theme = TestBed.inject(ThemeService);
    expect(theme.preference()).toBe('system');
  });

  it('switches to dark, toggles the html class and remembers the choice', () => {
    const theme = TestBed.inject(ThemeService);

    theme.set('dark');
    TestBed.tick();

    expect(theme.isDark()).toBe(true);
    expect(document.documentElement.classList.contains('dark')).toBe(true);
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark');

    theme.set('light');
    TestBed.tick();
    expect(document.documentElement.classList.contains('dark')).toBe(false);
  });

  it('loads the saved choice', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'dark');
    expect(TestBed.inject(ThemeService).preference()).toBe('dark');
  });
});
