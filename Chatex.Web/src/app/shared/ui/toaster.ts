import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ToastService } from '../../core/ui/toast.service';
import { ICONS } from './icons';

@Component({
  selector: 'app-toaster',
  imports: [ICONS],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="pt-safe pointer-events-none fixed inset-x-0 top-0 z-[1100] flex flex-col items-center gap-2 p-3 sm:top-auto sm:bottom-0 sm:items-end sm:p-6"
      aria-live="polite"
    >
      @for (toast of toasts.toasts(); track toast.id) {
        <div
          class="pointer-events-auto flex w-full max-w-sm items-start gap-3 rounded-xl border px-4 py-3 text-sm shadow-lg"
          [class]="
            toast.kind === 'error'
              ? 'border-red-200 bg-red-50 text-red-800 dark:border-red-900 dark:bg-red-950 dark:text-red-200'
              : 'border-line bg-surface text-fg'
          "
          role="status"
        >
          @if (toast.kind === 'error') {
            <svg lucideCircleExclamationPoint class="mt-0.5 size-4 shrink-0"></svg>
          } @else if (toast.kind === 'success') {
            <svg lucideCheck class="mt-0.5 size-4 shrink-0 text-emerald-500"></svg>
          } @else {
            <svg lucideInfo class="mt-0.5 size-4 shrink-0 text-brand-500"></svg>
          }
          <span class="flex-1">{{ toast.text }}</span>
          <button
            type="button"
            class="-m-1 rounded p-1 opacity-60 hover:opacity-100"
            aria-label="Dismiss"
            (click)="toasts.dismiss(toast.id)"
          >
            <svg lucideX class="size-4"></svg>
          </button>
        </div>
      }
    </div>
  `,
})
export class Toaster {
  protected readonly toasts = inject(ToastService);
}
