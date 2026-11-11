import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { ICONS } from '../../shared/ui/icons';

/** Centred card used by the sign-in and register pages. */
@Component({
  selector: 'app-auth-layout',
  imports: [ICONS],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <main
      class="flex min-h-dvh items-start justify-center bg-gradient-to-br from-brand-50 via-canvas to-canvas px-4 py-10 sm:items-center dark:from-brand-950/40"
    >
      <div class="w-full max-w-md">
        <div class="mb-8 flex flex-col items-center gap-3 text-center">
          <span class="flex size-14 items-center justify-center rounded-2xl bg-brand-600 text-white shadow-lg shadow-brand-600/30">
            <svg lucideMessagesSquare class="size-7"></svg>
          </span>
          <h1 class="text-2xl font-bold tracking-tight">{{ heading() }}</h1>
          <p class="text-sm text-muted">{{ subheading() }}</p>
        </div>
        <div class="card p-6 shadow-sm sm:p-8">
          <ng-content />
        </div>
      </div>
    </main>
  `,
})
export class AuthLayout {
  readonly heading = input.required<string>();
  readonly subheading = input('');
}
