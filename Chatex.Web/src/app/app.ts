import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ThemeService } from './core/ui/theme.service';
import { Toaster } from './shared/ui/toaster';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, Toaster],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<router-outlet /><app-toaster />`,
})
export class App {
  constructor() {
    // Created eagerly so the saved theme and OS changes apply on every page.
    inject(ThemeService);
  }
}
