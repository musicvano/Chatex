import { HttpEventType } from '@angular/common/http';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  effect,
  inject,
  input,
  model,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { Subscription } from 'rxjs';
import { AttachmentsApi } from '../../core/api/chatex-api';
import { AttachmentDto } from '../../core/api/models';
import { problemMessage } from '../../core/api/problem';
import { RealtimeService } from '../../core/realtime/realtime.service';
import { ToastService } from '../../core/ui/toast.service';
import { ChatMessage, MessagesStore } from '../../state/messages.store';
import { ICONS } from '../../shared/ui/icons';
import { FileSizePipe } from '../../shared/ui/pipes';

export const MAX_ATTACHMENTS = 10;
export const MAX_BODY = 4000;

/** What the server accepts; checked here only to fail fast. */
const ALLOWED_TYPES = new Set([
  'image/jpeg',
  'image/png',
  'image/gif',
  'image/webp',
  'application/pdf',
  'text/plain',
  'application/zip',
  'application/x-zip-compressed',
  'application/msword',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  'application/vnd.ms-excel',
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
  'application/vnd.ms-powerpoint',
  'application/vnd.openxmlformats-officedocument.presentationml.presentation',
  'audio/mpeg',
  'audio/ogg',
  'video/ogg',
  'video/mp4',
  'video/mpeg',
]);
const ALLOWED_EXTENSIONS =
  /\.(jpe?g|png|gif|webp|pdf|txt|zip|docx?|xlsx?|pptx?|mp3|mpeg|ogg|oga|ogv|mp4)$/i;

export function isAllowedFile(file: File): boolean {
  return ALLOWED_TYPES.has(file.type) || (!file.type && ALLOWED_EXTENSIONS.test(file.name));
}

interface Upload {
  key: number;
  file: File;
  progress: number;
  attachment?: AttachmentDto;
  error?: string;
}

let uploadKeys = 0;

/** The message box: text, reply/edit bar, and attachments uploaded as soon as they're picked. */
@Component({
  selector: 'app-composer',
  imports: [FileSizePipe, ICONS],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block border-t border-line bg-surface pb-safe' },
  template: `
    @if (editing(); as m) {
      <div class="flex items-center gap-3 border-b border-line px-4 py-2 text-sm">
        <svg lucidePencil class="size-4 shrink-0 text-brand-600"></svg>
        <div class="min-w-0 flex-1">
          <p class="font-medium text-brand-600 dark:text-brand-400">Editing message</p>
          <p class="truncate text-muted">{{ m.body }}</p>
        </div>
        <button
          type="button"
          class="icon-btn size-8"
          aria-label="Cancel editing"
          (click)="cancel()"
        >
          <svg lucideX class="size-4"></svg>
        </button>
      </div>
    } @else if (replyTo(); as m) {
      <div class="flex items-center gap-3 border-b border-line px-4 py-2 text-sm">
        <svg lucideReply class="size-4 shrink-0 text-brand-600"></svg>
        <div class="min-w-0 flex-1">
          <p class="font-medium text-brand-600 dark:text-brand-400">
            Reply to {{ m.sender.displayName }}
          </p>
          <p class="truncate text-muted">{{ m.body || '📎 Attachment' }}</p>
        </div>
        <button type="button" class="icon-btn size-8" aria-label="Cancel reply" (click)="cancel()">
          <svg lucideX class="size-4"></svg>
        </button>
      </div>
    }

    @if (uploads().length) {
      <ul class="flex gap-2 overflow-x-auto px-3 pt-3" aria-label="Attachments">
        @for (u of uploads(); track u.key) {
          <li
            class="relative flex w-44 shrink-0 items-center gap-2 rounded-xl border border-line bg-subtle p-2 pr-7"
            [class.border-red-400]="u.error"
          >
            <svg lucideFile class="size-5 shrink-0 text-muted"></svg>
            <div class="min-w-0 flex-1">
              <p class="truncate text-xs font-medium">{{ u.file.name }}</p>
              @if (u.error) {
                <p class="truncate text-[11px] text-red-600 dark:text-red-400" [title]="u.error">
                  {{ u.error }}
                </p>
              } @else if (!u.attachment) {
                <div class="mt-1 h-1 overflow-hidden rounded bg-line">
                  <div
                    class="h-full bg-brand-500 transition-[width]"
                    [style.width.%]="u.progress"
                  ></div>
                </div>
              } @else {
                <p class="text-[11px] text-muted">{{ u.file.size | fileSize }}</p>
              }
            </div>
            <button
              type="button"
              class="absolute top-1 right-1 rounded-full p-1 text-muted hover:bg-line hover:text-fg"
              [attr.aria-label]="'Remove ' + u.file.name"
              (click)="removeUpload(u.key)"
            >
              <svg lucideX class="size-3.5"></svg>
            </button>
          </li>
        }
      </ul>
    }

    <form
      class="flex items-end gap-1 px-2 py-2 sm:px-3"
      (submit)="$event.preventDefault(); submit()"
    >
      @if (!editing()) {
        <button
          type="button"
          class="icon-btn"
          aria-label="Attach files"
          title="Attach files"
          (click)="fileInput.click()"
        >
          <svg lucidePaperclip class="size-5"></svg>
        </button>
        <input #fileInput type="file" multiple class="hidden" (change)="onFilesPicked(fileInput)" />
      }
      <div class="min-w-0 flex-1">
        <textarea
          #textarea
          rows="1"
          class="input max-h-40 resize-none overflow-y-auto rounded-3xl border-transparent bg-subtle px-4 py-2.5 leading-5"
          [placeholder]="placeholder()"
          aria-label="Message"
          [maxLength]="maxBody"
          [value]="text()"
          (input)="onInput($any($event.target).value)"
          (keydown)="onKeydown($event)"
          (paste)="onPaste($event)"
        ></textarea>
        @if (text().length > maxBody - 300) {
          <p class="px-4 pt-1 text-right text-[11px] text-muted">
            {{ text().length }} / {{ maxBody }}
          </p>
        }
      </div>
      <button
        type="submit"
        class="flex size-10 shrink-0 items-center justify-center rounded-full bg-brand-600 text-white transition-colors hover:bg-brand-700 disabled:bg-zinc-300 disabled:text-zinc-500 dark:disabled:bg-zinc-700 dark:disabled:text-zinc-400"
        [disabled]="!canSubmit()"
        [attr.aria-label]="editing() ? 'Save' : 'Send'"
      >
        @if (editing()) {
          <svg lucideCheck class="size-5"></svg>
        } @else {
          <svg lucideSend class="size-5"></svg>
        }
      </button>
    </form>
  `,
})
export class Composer {
  private readonly store = inject(MessagesStore);
  private readonly attachmentsApi = inject(AttachmentsApi);
  private readonly realtime = inject(RealtimeService);
  private readonly toast = inject(ToastService);
  private readonly injector = inject(Injector);
  private readonly subscriptions = new Map<number, Subscription>();
  private readonly coarsePointer =
    typeof matchMedia === 'function' && matchMedia('(pointer: coarse)').matches;

  readonly chatId = input.required<string>();
  readonly placeholder = input('Message');
  readonly replyTo = model<ChatMessage | null>(null);
  readonly editing = model<ChatMessage | null>(null);

  protected readonly maxBody = MAX_BODY;
  protected readonly text = signal('');
  protected readonly uploads = signal<Upload[]>([]);
  private readonly textarea = viewChild.required<ElementRef<HTMLTextAreaElement>>('textarea');

  protected readonly canSubmit = computed(() => {
    const body = this.text().trim();
    if (this.editing()) {
      return body.length > 0 && body !== this.editing()?.body;
    }
    const uploads = this.uploads();
    const uploading = uploads.some((u) => !u.attachment && !u.error);
    const ready = uploads.some((u) => u.attachment);
    return !uploading && (body.length > 0 || ready);
  });

  constructor() {
    // Starting an edit puts the message text into the box; a reply just focuses it.
    effect(() => {
      const editing = this.editing();
      untracked(() => {
        this.text.set(editing?.body ?? '');
        if (editing) this.focus();
      });
    });
    effect(() => {
      if (this.replyTo()) untracked(() => this.focus());
    });
    // Grow with the text, up to max-h-40 (then it scrolls).
    effect(() => {
      this.text();
      afterNextRender(() => this.autoResize(), { injector: this.injector });
    });
    // Switching chats starts with an empty box.
    effect(() => {
      this.chatId();
      untracked(() => this.reset());
    });
    inject(DestroyRef).onDestroy(() => this.subscriptions.forEach((s) => s.unsubscribe()));
  }

  private autoResize(): void {
    const element = this.textarea().nativeElement;
    element.style.height = 'auto';
    const border = element.offsetHeight - element.clientHeight;
    element.style.height = `${element.scrollHeight + border}px`;
  }

  focus(): void {
    setTimeout(() => this.textarea().nativeElement.focus());
  }

  /** Adds files from the picker, a paste or a drop. */
  addFiles(files: File[]): void {
    if (this.editing() || !files.length) {
      return;
    }
    const room = MAX_ATTACHMENTS - this.uploads().length;
    if (files.length > room) {
      this.toast.error(`You can attach up to ${MAX_ATTACHMENTS} files per message.`);
      files = files.slice(0, Math.max(0, room));
    }
    for (const file of files) {
      if (!isAllowedFile(file)) {
        this.toast.error(`“${file.name}” has a file type that can't be sent.`);
        continue;
      }
      this.startUpload(file);
    }
  }

  protected onInput(value: string): void {
    this.text.set(value);
    if (value.trim() && !this.editing()) {
      this.realtime.sendTyping(this.chatId());
    }
  }

  protected onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape' && (this.editing() || this.replyTo())) {
      event.preventDefault();
      this.cancel();
      return;
    }
    // On touch keyboards Enter makes a new line; elsewhere Enter sends and Shift+Enter breaks the line.
    if (event.key === 'Enter' && !event.shiftKey && !event.isComposing && !this.coarsePointer) {
      event.preventDefault();
      this.submit();
    }
  }

  protected onPaste(event: ClipboardEvent): void {
    const files = Array.from(event.clipboardData?.files ?? []);
    if (files.length) {
      event.preventDefault();
      this.addFiles(files);
    }
  }

  protected onFilesPicked(input: HTMLInputElement): void {
    this.addFiles(Array.from(input.files ?? []));
    input.value = '';
  }

  protected cancel(): void {
    if (this.editing()) {
      this.editing.set(null);
      this.text.set('');
    }
    this.replyTo.set(null);
  }

  protected removeUpload(key: number): void {
    this.subscriptions.get(key)?.unsubscribe();
    this.subscriptions.delete(key);
    this.uploads.update((list) => list.filter((u) => u.key !== key));
  }

  submit(): void {
    if (!this.canSubmit()) {
      return;
    }
    const body = this.text().trim();
    const editing = this.editing();
    if (editing) {
      this.editing.set(null);
      this.text.set('');
      this.store.edit(editing.id, body).catch((error) => this.toast.error(error));
      return;
    }
    const attachments = this.uploads()
      .map((u) => u.attachment)
      .filter((a): a is AttachmentDto => !!a);
    const replyTo = this.replyTo()?.id ?? null;
    this.text.set('');
    this.uploads.set([]);
    this.replyTo.set(null);
    this.focus();
    this.store.send(body, attachments, replyTo).catch((error) => this.toast.error(error));
  }

  private startUpload(file: File): void {
    const key = ++uploadKeys;
    this.uploads.update((list) => [...list, { key, file, progress: 0 }]);
    const patch = (change: Partial<Upload>) =>
      this.uploads.update((list) => list.map((u) => (u.key === key ? { ...u, ...change } : u)));
    const subscription = this.attachmentsApi.upload(file).subscribe({
      next: (event) => {
        if (event.type === HttpEventType.UploadProgress && event.total) {
          patch({ progress: Math.round((event.loaded / event.total) * 100) });
        } else if (event.type === HttpEventType.Response && event.body) {
          patch({ progress: 100, attachment: event.body });
        }
      },
      error: (error) => patch({ error: problemMessage(error) }),
    });
    this.subscriptions.set(key, subscription);
  }

  private reset(): void {
    this.subscriptions.forEach((s) => s.unsubscribe());
    this.subscriptions.clear();
    this.text.set('');
    this.uploads.set([]);
  }
}
