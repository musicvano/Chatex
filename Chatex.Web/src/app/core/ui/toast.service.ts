import { Injectable, signal } from '@angular/core';
import { problemMessage } from '../api/problem';

export interface Toast {
  id: number;
  kind: 'info' | 'success' | 'error';
  text: string;
}

/** Short, self-dismissing notices shown by the Toaster component. */
@Injectable({ providedIn: 'root' })
export class ToastService {
  private nextId = 1;
  readonly toasts = signal<Toast[]>([]);

  info(text: string): void {
    this.show('info', text);
  }

  success(text: string): void {
    this.show('success', text);
  }

  /** Shows an error; accepts a message or a failed HTTP response. */
  error(error: unknown): void {
    this.show('error', typeof error === 'string' ? error : problemMessage(error));
  }

  dismiss(id: number): void {
    this.toasts.update((list) => list.filter((t) => t.id !== id));
  }

  private show(kind: Toast['kind'], text: string): void {
    const id = this.nextId++;
    this.toasts.update((list) => [...list.slice(-3), { id, kind, text }]);
    setTimeout(() => this.dismiss(id), kind === 'error' ? 6_000 : 3_500);
  }
}
