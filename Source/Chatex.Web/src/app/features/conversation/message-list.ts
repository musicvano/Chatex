import { Clipboard } from '@angular/cdk/clipboard';
import { CdkMenu, CdkMenuItem, CdkMenuTrigger } from '@angular/cdk/menu';
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
  output,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { ChatDto, canManageChat, canPost } from '../../core/api/models';
import { ToastService } from '../../core/ui/toast.service';
import { ChatMessage } from '../../state/messages.store';
import { Avatar } from '../../shared/ui/avatar';
import { ICONS } from '../../shared/ui/icons';
import { ClockPipe, dayLabel } from '../../shared/ui/pipes';
import { MessageAttachments } from './attachments';

/** Messages from the same sender within this window are grouped into one block. */
const GROUP_WINDOW_MS = 5 * 60_000;
const NEAR_BOTTOM_PX = 80;
const LOAD_OLDER_PX = 400;

export type Row =
  | { kind: 'day'; key: string; label: string }
  | {
      kind: 'message';
      key: string;
      message: ChatMessage;
      mine: boolean;
      first: boolean;
      last: boolean;
      /** undefined: not a reply; null: the original isn't loaded. */
      reply: ChatMessage | null | undefined;
      seen: boolean;
    };

/** Builds the rows: day separators plus messages grouped by sender. Exported for tests. */
export function buildRows(
  messages: ChatMessage[],
  meId: string | undefined,
  peerReadUpTo: string | null,
): Row[] {
  const byId = new Map(messages.map((m) => [m.id, m]));
  const rows: Row[] = [];
  let lastDay = '';
  messages.forEach((message, i) => {
    const day = new Date(message.createdAt).toDateString();
    if (day !== lastDay) {
      rows.push({ kind: 'day', key: `day-${day}`, label: dayLabel(message.createdAt) });
      lastDay = day;
    }
    const prev = messages[i - 1];
    const next = messages[i + 1];
    const sameGroup = (a: ChatMessage | undefined, b: ChatMessage | undefined) =>
      !!a &&
      !!b &&
      a.sender.id === b.sender.id &&
      new Date(a.createdAt).toDateString() === new Date(b.createdAt).toDateString() &&
      Math.abs(Date.parse(b.createdAt) - Date.parse(a.createdAt)) < GROUP_WINDOW_MS;
    const mine = message.sender.id === meId;
    rows.push({
      kind: 'message',
      key: message.id,
      message,
      mine,
      first: !sameGroup(prev, message),
      last: !sameGroup(message, next),
      reply: message.replyToMessageId ? (byId.get(message.replyToMessageId) ?? null) : undefined,
      seen: mine && !message.localState && !!peerReadUpTo && message.id <= peerReadUpTo,
    });
  });
  return rows;
}

/**
 * The scrolling message history. New pages load as the user nears the top, keeping the view
 * anchored; new messages scroll into view only when the user is already at the bottom.
 */
@Component({
  selector: 'app-message-list',
  imports: [Avatar, ClockPipe, MessageAttachments, CdkMenu, CdkMenuItem, CdkMenuTrigger, ICONS],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'relative flex min-h-0 flex-1 flex-col' },
  template: `
    <div
      #scroller
      class="min-h-0 flex-1 overflow-y-auto overscroll-contain py-2 [overflow-anchor:none]"
      (scroll)="onScroll()"
      role="log"
      aria-live="polite"
      aria-label="Messages"
    >
      @if (loadingOlder()) {
        <div class="flex justify-center py-3 text-muted">
          <svg lucideLoaderCircle class="size-5 animate-spin"></svg>
        </div>
      } @else if (!hasOlder() && messages().length && !loading()) {
        <p class="py-4 text-center text-xs text-muted">
          This is the beginning of the conversation.
        </p>
      }

      @for (row of rows(); track row.key) {
        @if (row.kind === 'day') {
          <div class="sticky top-1 z-10 flex justify-center py-2">
            <span
              class="rounded-full border border-line bg-surface/90 px-3 py-1 text-xs font-medium text-muted shadow-sm backdrop-blur"
              >{{ row.label }}</span
            >
          </div>
        } @else {
          @let m = row.message;
          <div
            class="flex gap-2 px-2 sm:px-4 lg:px-6"
            [class.justify-end]="row.mine"
            [class.mt-3]="row.first"
            [class.mt-0.5]="!row.first"
            [id]="'m-' + m.id"
          >
            @if (!row.mine && showAvatars()) {
              <div class="w-8 shrink-0 self-end">
                @if (row.last) {
                  <button
                    type="button"
                    class="rounded-full"
                    [attr.aria-label]="'Profile of ' + m.sender.displayName"
                    (click)="openUser.emit(m.sender.id)"
                  >
                    <app-avatar
                      [id]="m.sender.id"
                      [name]="m.sender.displayName"
                      [hasAvatar]="m.sender.hasAvatar"
                      [size]="32"
                    />
                  </button>
                }
              </div>
            }
            <div
              class="group flex max-w-[85%] min-w-0 items-center gap-1 sm:max-w-[75%] xl:max-w-[65%]"
              [class.flex-row-reverse]="row.mine"
            >
              <div
                class="min-w-0 rounded-2xl px-3 py-1.5 shadow-sm transition-shadow"
                [class]="
                  (row.mine ? 'bg-brand-600 text-white' : 'border border-line bg-bubble text-fg') +
                  (row.mine && row.last ? ' rounded-br-md' : '') +
                  (!row.mine && row.last ? ' rounded-bl-md' : '') +
                  (highlighted() === m.id ? ' ring-4 ring-amber-400/70' : '') +
                  (m.localState === 'failed' ? ' opacity-70' : '')
                "
              >
                @if (row.first && !row.mine && showNames()) {
                  <button
                    type="button"
                    class="block text-xs font-semibold text-brand-600 hover:underline dark:text-brand-400"
                    (click)="openUser.emit(m.sender.id)"
                  >
                    {{ m.sender.displayName }}
                  </button>
                }
                @if (m.isDeleted) {
                  <p class="py-0.5 text-sm italic opacity-70">Message deleted</p>
                } @else {
                  @if (row.reply !== undefined) {
                    <button
                      type="button"
                      class="my-1 block w-full min-w-0 rounded-lg border-l-[3px] px-2 py-1 text-left text-xs"
                      [class]="
                        row.mine
                          ? 'border-white/70 bg-white/15'
                          : 'border-brand-500 bg-brand-50 dark:bg-brand-950/60'
                      "
                      (click)="row.reply && jumpTo(row.reply.id)"
                    >
                      <span class="block font-semibold">{{
                        row.reply?.sender?.displayName ?? 'Reply'
                      }}</span>
                      <span class="block truncate opacity-80">{{
                        row.reply ? snippet(row.reply) : 'Original message'
                      }}</span>
                    </button>
                  }
                  @if (m.attachments.length) {
                    <div class="mt-1 w-64 max-w-full sm:w-72">
                      <app-message-attachments [attachments]="m.attachments" [mine]="row.mine" />
                    </div>
                  }
                  @if (m.body) {
                    <p class="text-[15px] leading-snug break-words whitespace-pre-wrap">
                      {{ m.body }}
                    </p>
                  }
                }
                <span
                  class="-mb-0.5 flex items-center justify-end gap-1 text-[11px] leading-4"
                  [class]="row.mine ? 'text-white/75' : 'text-muted'"
                >
                  @if (m.editedAt && !m.isDeleted) {
                    <span>edited</span>
                  }
                  <time [attr.datetime]="m.createdAt">{{ m.createdAt | clock }}</time>
                  @if (row.mine) {
                    @switch (m.localState) {
                      @case ('pending') {
                        <svg
                          lucideLoaderCircle
                          class="size-3.5 animate-spin"
                          aria-label="Sending"
                        ></svg>
                      }
                      @case ('failed') {
                        <svg
                          lucideCircleExclamationPoint
                          class="size-3.5 text-red-200"
                          aria-label="Not sent"
                        ></svg>
                      }
                      @default {
                        @if (row.seen) {
                          <svg lucideCheckCheck class="size-3.5" aria-label="Seen"></svg>
                        } @else {
                          <svg lucideCheck class="size-3.5" aria-label="Sent"></svg>
                        }
                      }
                    }
                  }
                </span>
              </div>

              @if (!m.isDeleted && !m.localState) {
                <button
                  type="button"
                  class="flex size-8 shrink-0 items-center justify-center rounded-full text-muted transition-opacity hover:bg-subtle focus:opacity-100 pointer-fine:opacity-0 pointer-fine:group-hover:opacity-100"
                  [cdkMenuTriggerFor]="menu"
                  aria-label="Message actions"
                >
                  <svg lucideEllipsisVertical class="size-4"></svg>
                </button>
                <ng-template #menu>
                  <div cdkMenu class="menu">
                    @if (allowReply()) {
                      <button cdkMenuItem class="menu-item" (cdkMenuItemTriggered)="reply.emit(m)">
                        <svg lucideReply class="size-4"></svg>Reply
                      </button>
                    }
                    @if (m.body) {
                      <button cdkMenuItem class="menu-item" (cdkMenuItemTriggered)="copy(m)">
                        <svg lucideCopy class="size-4"></svg>Copy text
                      </button>
                    }
                    @if (row.mine && m.body) {
                      <button cdkMenuItem class="menu-item" (cdkMenuItemTriggered)="edit.emit(m)">
                        <svg lucidePencil class="size-4"></svg>Edit
                      </button>
                    }
                    @if (row.mine || canModerate()) {
                      <button
                        cdkMenuItem
                        class="menu-item text-red-600 dark:text-red-400"
                        (cdkMenuItemTriggered)="remove.emit(m)"
                      >
                        <svg lucideTrash class="size-4"></svg>Delete
                      </button>
                    }
                  </div>
                </ng-template>
              }
            </div>
          </div>
          @if (m.localState === 'failed') {
            <div class="mt-1 flex justify-end gap-3 px-4 text-xs lg:px-6">
              <span class="text-red-600 dark:text-red-400">Not sent.</span>
              <button
                type="button"
                class="font-medium text-brand-600 hover:underline dark:text-brand-400"
                (click)="retry.emit(m)"
              >
                Retry
              </button>
              <button type="button" class="text-muted hover:underline" (click)="discard.emit(m)">
                Delete
              </button>
            </div>
          }
        }
      }

      @if (loading() && !messages().length) {
        <div class="flex h-full items-center justify-center text-muted">
          <svg lucideLoaderCircle class="size-6 animate-spin"></svg>
        </div>
      } @else if (!loading() && !messages().length) {
        <div
          class="flex h-full flex-col items-center justify-center gap-2 px-6 text-center text-muted"
        >
          <svg lucideMessagesSquare class="size-10 opacity-50"></svg>
          <p class="text-sm">{{ emptyText() }}</p>
        </div>
      }
    </div>

    @if (showJump()) {
      <button
        type="button"
        class="absolute right-4 bottom-4 flex items-center gap-1.5 rounded-full bg-brand-600 py-2 pr-4 pl-3 text-sm font-medium text-white shadow-lg hover:bg-brand-700"
        (click)="scrollToBottom(true)"
      >
        <svg lucideArrowDown class="size-4"></svg> New messages
      </button>
    }
  `,
})
export class MessageList {
  private readonly injector = inject(Injector);
  private readonly clipboard = inject(Clipboard);
  private readonly toast = inject(ToastService);

  readonly messages = input.required<ChatMessage[]>();
  readonly chat = input<ChatDto | undefined>();
  readonly meId = input<string | undefined>();
  readonly peerReadUpTo = input<string | null>(null);
  readonly loading = input(false);
  readonly loadingOlder = input(false);
  readonly hasOlder = input(false);

  readonly loadOlder = output<void>();
  readonly reply = output<ChatMessage>();
  readonly edit = output<ChatMessage>();
  readonly remove = output<ChatMessage>();
  readonly retry = output<ChatMessage>();
  readonly discard = output<ChatMessage>();
  readonly openUser = output<string>();
  /** True while the newest message is in view. */
  readonly atBottom = output<boolean>();

  protected readonly rows = computed(() =>
    buildRows(this.messages(), this.meId(), this.peerReadUpTo()),
  );
  protected readonly showNames = computed(() => this.chat()?.type === 'group');
  protected readonly showAvatars = computed(() => this.chat()?.type === 'group');
  protected readonly allowReply = computed(() => {
    const chat = this.chat();
    return !!chat && canPost(chat.type, chat.myRole);
  });
  protected readonly canModerate = computed(() => canManageChat(this.chat()?.myRole));
  protected readonly emptyText = computed(() =>
    this.chat()?.type === 'channel' ? 'No posts yet.' : 'No messages yet. Say hello!',
  );
  protected readonly showJump = signal(false);
  protected readonly highlighted = signal<string | null>(null);

  private readonly scroller = viewChild.required<ElementRef<HTMLElement>>('scroller');
  private ready = false;
  private prevFirst: string | undefined;
  private prevLast: string | undefined;
  private measuredHeight = 0;
  private measuredTop = 0;
  private nearBottom = true;

  constructor() {
    effect(() => {
      const messages = this.messages();
      untracked(() => this.onMessagesChanged(messages));
    });

    // Keep the newest message in view when the pane shrinks (composer grows, keyboard opens).
    afterNextRender(() => {
      const element = this.scroller().nativeElement;
      const observer = new ResizeObserver(() => {
        if (this.nearBottom && this.ready) {
          element.scrollTop = element.scrollHeight;
        }
        this.measure();
      });
      observer.observe(element);
      this.injector.get(DestroyRef).onDestroy(() => observer.disconnect());
    });
  }

  scrollToBottom(smooth = false): void {
    const element = this.scroller().nativeElement;
    element.scrollTo({ top: element.scrollHeight, behavior: smooth ? 'smooth' : 'auto' });
    this.showJump.set(false);
  }

  protected onScroll(): void {
    this.measure();
    if (this.nearBottom) {
      this.showJump.set(false);
    }
    this.maybeLoadOlder();
  }

  protected snippet(message: ChatMessage): string {
    if (message.isDeleted) return 'Message deleted';
    return message.body || (message.attachments.length ? '📎 Attachment' : '');
  }

  protected copy(message: ChatMessage): void {
    if (message.body && this.clipboard.copy(message.body)) {
      this.toast.info('Copied to clipboard');
    }
  }

  protected jumpTo(messageId: string): void {
    const element = document.getElementById(`m-${messageId}`);
    element?.scrollIntoView({ block: 'center', behavior: 'smooth' });
    this.highlighted.set(messageId);
    setTimeout(() => this.highlighted.set(null), 1600);
  }

  private onMessagesChanged(messages: ChatMessage[]): void {
    if (!messages.length) {
      // A new chat was opened (or it is empty): start over at the bottom.
      this.ready = false;
      this.prevFirst = this.prevLast = undefined;
      this.showJump.set(false);
      return;
    }
    const before = {
      first: this.prevFirst,
      last: this.prevLast,
      height: this.measuredHeight,
      top: this.measuredTop,
      nearBottom: this.nearBottom,
    };
    const first = messages[0].id;
    const lastMessage = messages[messages.length - 1];
    this.prevFirst = first;
    this.prevLast = lastMessage.id;

    afterNextRender(
      {
        write: () => {
          const element = this.scroller().nativeElement;
          if (!this.ready) {
            element.scrollTop = element.scrollHeight;
            this.ready = true;
          } else if (first !== before.first && lastMessage.id === before.last) {
            // Older messages were added on top: keep what the user was looking at in place.
            element.scrollTop = before.top + (element.scrollHeight - before.height);
          } else if (lastMessage.id !== before.last) {
            if (before.nearBottom || lastMessage.sender.id === this.meId()) {
              this.scrollToBottom(true);
            } else {
              this.showJump.set(true);
            }
          }
          this.measure();
          this.maybeLoadOlder();
        },
      },
      { injector: this.injector },
    );
  }

  private measure(): void {
    const element = this.scroller().nativeElement;
    this.measuredHeight = element.scrollHeight;
    this.measuredTop = element.scrollTop;
    const near = element.scrollHeight - element.scrollTop - element.clientHeight < NEAR_BOTTOM_PX;
    if (near !== this.nearBottom || !this.ready) {
      this.nearBottom = near;
      this.atBottom.emit(near);
    }
  }

  private maybeLoadOlder(): void {
    if (
      this.ready &&
      this.hasOlder() &&
      !this.loadingOlder() &&
      this.scroller().nativeElement.scrollTop < LOAD_OLDER_PX
    ) {
      this.loadOlder.emit();
    }
  }
}
