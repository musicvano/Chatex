import { DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ChatsApi } from '../../core/api/chatex-api';
import { UserSummaryDto } from '../../core/api/models';
import { fieldErrors, problemMessage, toProblem } from '../../core/api/problem';
import { ChatsStore } from '../../state/chats.store';
import { ICONS } from '../../shared/ui/icons';
import { UserSearch } from '../../shared/ui/user-search';

type Tab = 'direct' | 'group' | 'channel';

/** Public handles: 5–32 characters, a letter first, then letters, digits or _. */
export const HANDLE_PATTERN = /^[a-zA-Z][a-zA-Z0-9_]{4,31}$/;

@Component({
  selector: 'app-new-chat-dialog',
  imports: [ReactiveFormsModule, UserSearch, ICONS],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dialog-card flex flex-col" role="dialog" aria-labelledby="new-chat-title">
      <header class="flex items-center gap-2 border-b border-line px-5 py-3">
        <h2 id="new-chat-title" class="flex-1 text-lg font-semibold">New chat</h2>
        <button type="button" class="icon-btn -mr-2" aria-label="Close" (click)="ref.close()">
          <svg lucideX class="size-5"></svg>
        </button>
      </header>

      <div class="px-5 pt-4">
        <div class="grid grid-cols-3 gap-1 rounded-xl bg-subtle p-1" role="tablist">
          @for (t of tabs; track t.id) {
            <button
              type="button"
              role="tab"
              class="flex items-center justify-center gap-1.5 rounded-lg px-2 py-2 text-sm font-medium transition-colors"
              [class]="tab() === t.id ? 'bg-surface text-fg shadow-sm' : 'text-muted hover:text-fg'"
              [attr.aria-selected]="tab() === t.id"
              (click)="tab.set(t.id)"
            >
              @switch (t.id) {
                @case ('direct') { <svg lucideUser class="size-4"></svg> }
                @case ('group') { <svg lucideUsers class="size-4"></svg> }
                @case ('channel') { <svg lucideMegaphone class="size-4"></svg> }
              }
              {{ t.label }}
            </button>
          }
        </div>
      </div>

      <div class="px-5 py-4">
        @if (tab() === 'direct') {
          @if (error()) {
            <p class="mb-3 rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300" role="alert">{{ error() }}</p>
          }
          <p class="mb-3 text-sm text-muted">Find someone to message directly.</p>
          <app-user-search (picked)="startDirect($event)" />
        } @else {
          <form [formGroup]="form" (ngSubmit)="create()" class="space-y-4" novalidate>
            @if (error()) {
              <p class="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300" role="alert">{{ error() }}</p>
            }
            <div>
              <label class="label" for="chat-title">{{ tab() === 'channel' ? 'Channel name' : 'Group name' }}</label>
              <input id="chat-title" class="input" formControlName="title" maxlength="128" [class.input-invalid]="titleInvalid()" />
              @if (titleInvalid()) { <p class="field-error">A name is required.</p> }
            </div>
            <div>
              <label class="label" for="chat-description">Description <span class="font-normal text-muted">(optional)</span></label>
              <textarea id="chat-description" class="input min-h-20" formControlName="description" maxlength="1000"></textarea>
            </div>
            <div>
              <label class="label" for="chat-handle">Public handle <span class="font-normal text-muted">(optional)</span></label>
              <div class="relative">
                <span class="pointer-events-none absolute top-1/2 left-3 -translate-y-1/2 text-muted">&#64;</span>
                <input id="chat-handle" class="input pl-7" formControlName="publicHandle" autocapitalize="none" maxlength="32" [class.input-invalid]="handleError()" />
              </div>
              @if (handleError(); as m) {
                <p class="field-error">{{ m }}</p>
              } @else {
                <p class="mt-1 text-xs text-muted">With a handle the {{ tab() }} is public: anyone can find and join it.</p>
              }
            </div>
            <div>
              <span class="label">Add members</span>
              <app-user-search [multiple]="true" [(selected)]="members" />
            </div>
            <div class="flex flex-col-reverse gap-2 pt-2 sm:flex-row sm:justify-end">
              <button type="button" class="btn-secondary" (click)="ref.close()">Cancel</button>
              <button type="submit" class="btn-primary" [disabled]="busy()">
                @if (busy()) { <svg lucideLoaderCircle class="size-4 animate-spin"></svg> }
                Create {{ tab() }}
              </button>
            </div>
          </form>
        }
      </div>
    </div>
  `,
})
export class NewChatDialog {
  protected readonly ref = inject(DialogRef);
  private readonly api = inject(ChatsApi);
  private readonly store = inject(ChatsStore);
  private readonly router = inject(Router);

  protected readonly tabs: { id: Tab; label: string }[] = [
    { id: 'direct', label: 'Direct' },
    { id: 'group', label: 'Group' },
    { id: 'channel', label: 'Channel' },
  ];
  protected readonly tab = signal<Tab>('direct');
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  private readonly serverHandleError = signal<string | null>(null);
  protected readonly members = signal<UserSummaryDto[]>([]);

  protected readonly form = inject(NonNullableFormBuilder).group({
    title: ['', [Validators.required, Validators.maxLength(128)]],
    description: ['', Validators.maxLength(1000)],
    publicHandle: ['', Validators.pattern(HANDLE_PATTERN)],
  });

  constructor() {
    this.form.controls.publicHandle.valueChanges.subscribe(() => this.serverHandleError.set(null));
  }

  protected titleInvalid(): boolean {
    const c = this.form.controls.title;
    return c.invalid && c.touched;
  }

  protected handleError(): string | null {
    const c = this.form.controls.publicHandle;
    if (this.serverHandleError()) {
      return this.serverHandleError();
    }
    return c.invalid && c.touched ? '5–32 characters: letters, digits and _, starting with a letter.' : null;
  }

  protected async startDirect(user: UserSummaryDto): Promise<void> {
    try {
      const chat = await firstValueFrom(this.api.direct(user.id));
      this.store.setDetails(chat);
      this.ref.close();
      await this.router.navigate(['/chats', chat.id]);
    } catch (error) {
      this.error.set(problemMessage(error));
    }
  }

  protected async create(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    const value = this.form.getRawValue();
    try {
      const chat = await firstValueFrom(
        this.api.create({
          type: this.tab() === 'channel' ? 'channel' : 'group',
          title: value.title.trim(),
          description: value.description.trim() || null,
          publicHandle: value.publicHandle.trim() || null,
          memberIds: this.members().map((m) => m.id),
        }),
      );
      this.store.setDetails(chat);
      this.ref.close();
      await this.router.navigate(['/chats', chat.id]);
    } catch (error) {
      const problem = toProblem(error);
      if (problem.code === 'handle_taken') {
        this.serverHandleError.set(problemMessage(error));
      } else {
        this.serverHandleError.set(fieldErrors(error, 'publicHandle')[0] ?? null);
        this.error.set(problemMessage(error));
      }
    } finally {
      this.busy.set(false);
    }
  }
}
