import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  input,
  resource,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router, RouterLink, RouterOutlet } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ChatsApi, UsersApi } from '../../core/api/chatex-api';
import { canPost, chatTitle } from '../../core/api/models';
import { toProblem } from '../../core/api/problem';
import { AuthService } from '../../core/auth/auth.service';
import { PresenceStore } from '../../core/realtime/presence.store';
import { TypingStore } from '../../core/realtime/typing.store';
import { ToastService } from '../../core/ui/toast.service';
import { ChatsStore } from '../../state/chats.store';
import { ChatMessage, MessagesStore } from '../../state/messages.store';
import { Avatar } from '../../shared/ui/avatar';
import { Dialogs } from '../../shared/ui/dialogs';
import { ICONS } from '../../shared/ui/icons';
import { timeAgo } from '../../shared/ui/pipes';
import { UserProfileDialog } from '../user-profile/user-profile-dialog';
import { Composer } from './composer';
import { MessageList } from './message-list';

@Component({
  selector: 'app-conversation-page',
  imports: [RouterLink, RouterOutlet, Avatar, MessageList, Composer, ICONS],
  providers: [MessagesStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex min-h-0 flex-1' },
  template: `
    <section
      class="relative flex min-w-0 flex-1 flex-col bg-canvas"
      (dragover)="onDragOver($event)"
      (dragleave)="dragging.set(false)"
      (drop)="onDrop($event)"
    >
      <header
        class="pt-safe flex items-center gap-1 border-b border-line bg-surface/95 px-2 backdrop-blur sm:px-3"
      >
        <a routerLink="/chats" class="icon-btn md:hidden" aria-label="Back to chats">
          <svg lucideArrowLeft class="size-5"></svg>
        </a>
        <a
          [routerLink]="['/chats', chatId(), 'info']"
          class="flex min-w-0 flex-1 items-center gap-3 rounded-lg py-2 pr-2 pl-1 hover:bg-subtle/60"
        >
          <app-avatar
            [kind]="isDirect() ? 'user' : 'chat'"
            [id]="isDirect() ? chat()?.peer?.id : chatId()"
            [name]="title()"
            [hasAvatar]="isDirect() ? !!chat()?.peer?.hasAvatar : !!chat()?.hasAvatar"
            [online]="isDirect() && presence.isOnline(chat()?.peer?.id)"
            [size]="40"
          />
          <span class="min-w-0">
            <span class="block truncate font-semibold">{{ title() }}</span>
            <span
              class="block truncate text-xs"
              [class]="typingText() ? 'text-brand-600 dark:text-brand-400' : 'text-muted'"
            >
              {{ typingText() ?? subtitle() }}
            </span>
          </span>
        </a>
        <a
          [routerLink]="['/chats', chatId(), 'info']"
          class="icon-btn"
          aria-label="Chat info"
          title="Chat info"
        >
          <svg lucideInfo class="size-5"></svg>
        </a>
      </header>

      @if (store.error() && !store.messages().length) {
        <div class="flex flex-1 flex-col items-center justify-center gap-3 px-6 text-center">
          <p class="text-sm text-muted">
            {{
              chat() && !chat()!.myRole
                ? 'Join this chat to read its messages.'
                : "Messages couldn't be loaded."
            }}
          </p>
          @if (!chat() || chat()!.myRole) {
            <button type="button" class="btn-secondary" (click)="store.loadInitial()">
              <svg lucideRotateCw class="size-4"></svg>Try again
            </button>
          }
        </div>
      } @else {
        <app-message-list
          [messages]="store.messages()"
          [chat]="chat()"
          [meId]="me()?.id"
          [peerReadUpTo]="store.peerReadUpTo()"
          [loading]="store.loading()"
          [loadingOlder]="store.loadingOlder()"
          [hasOlder]="store.hasOlder()"
          (loadOlder)="store.loadOlder()"
          (reply)="replyTo.set($event); editing.set(null)"
          (edit)="editing.set($event); replyTo.set(null)"
          (remove)="deleteMessage($event)"
          (retry)="retry($event)"
          (discard)="store.discard($event.id)"
          (openUser)="openUser($event)"
          (atBottom)="atBottom.set($event)"
        />
      }

      @if (chat(); as c) {
        @if (!c.myRole) {
          <div class="pb-safe border-t border-line bg-surface p-3 text-center">
            <button
              type="button"
              class="btn-primary w-full sm:w-auto"
              [disabled]="joining()"
              (click)="join()"
            >
              <svg lucideUserPlus class="size-4"></svg> Join {{ c.type }}
            </button>
          </div>
        } @else if (allowPost()) {
          <app-composer
            [chatId]="chatId()"
            [placeholder]="c.type === 'channel' ? 'Broadcast a post' : 'Message'"
            [(replyTo)]="replyTo"
            [(editing)]="editing"
          />
        } @else {
          <div
            class="pb-safe flex items-center justify-center gap-2 border-t border-line bg-surface p-3 text-sm text-muted"
          >
            <svg lucideMegaphone class="size-4"></svg> Only admins can post in this channel
            <button
              type="button"
              class="ml-2 font-medium text-brand-600 hover:underline dark:text-brand-400"
              (click)="toggleMute()"
            >
              {{ c.isMuted ? 'Unmute' : 'Mute' }}
            </button>
          </div>
        }
      }

      @if (dragging()) {
        <div
          class="pointer-events-none absolute inset-2 z-20 flex items-center justify-center rounded-2xl border-2 border-dashed border-brand-500 bg-brand-50/90 text-brand-700 dark:bg-brand-950/90 dark:text-brand-200"
        >
          <span class="flex items-center gap-2 font-medium"
            ><svg lucidePaperclip class="size-5"></svg> Drop files to attach</span
          >
        </div>
      }
    </section>

    <router-outlet />
  `,
})
export class ConversationPage {
  protected readonly store = inject(MessagesStore);
  private readonly chats = inject(ChatsStore);
  private readonly chatsApi = inject(ChatsApi);
  private readonly usersApi = inject(UsersApi);
  protected readonly presence = inject(PresenceStore);
  private readonly typing = inject(TypingStore);
  private readonly toast = inject(ToastService);
  private readonly dialogs = inject(Dialogs);
  private readonly router = inject(Router);
  protected readonly me = inject(AuthService).user;

  /** Route parameter (bound with withComponentInputBinding). */
  readonly chatId = input.required<string>();

  protected readonly chat = computed(() => this.chats.details()[this.chatId()]);
  protected readonly isDirect = computed(() => this.chat()?.type === 'direct');
  protected readonly title = computed(() => {
    const chat = this.chat() ?? this.chats.items().find((c) => c.id === this.chatId());
    return chat ? chatTitle(chat) : '';
  });
  protected readonly allowPost = computed(() => {
    const chat = this.chat();
    return !!chat && canPost(chat.type, chat.myRole);
  });

  protected readonly replyTo = signal<ChatMessage | null>(null);
  protected readonly editing = signal<ChatMessage | null>(null);
  protected readonly atBottom = signal(true);
  protected readonly dragging = signal(false);
  protected readonly joining = signal(false);
  private readonly composer = viewChild(Composer);

  /** The direct-chat partner's profile, for "last seen". */
  private readonly peer = resource({
    params: () => (this.isDirect() ? this.chat()?.peer?.id : undefined),
    loader: ({ params }) => firstValueFrom(this.usersApi.get(params)),
  });

  protected readonly typingText = computed(() => {
    const ids = this.typing.usersTyping(this.chatId());
    if (!ids.length) return null;
    if (this.isDirect()) return 'typing…';
    return ids.length === 1 ? 'Someone is typing…' : `${ids.length} people are typing…`;
  });

  protected readonly subtitle = computed(() => {
    const chat = this.chat();
    if (!chat) return '';
    if (chat.type === 'direct') {
      const peerId = chat.peer?.id;
      if (this.presence.isOnline(peerId)) return 'online';
      const seen = (peerId && this.presence.lastSeenAt(peerId)) || this.peer.value()?.lastSeenAt;
      return seen ? `last seen ${timeAgo(seen)}` : `@${chat.peer?.userName ?? ''}`;
    }
    const noun = chat.type === 'channel' ? 'subscriber' : 'member';
    const handle = chat.publicHandle ? ` · @${chat.publicHandle}` : '';
    return `${chat.memberCount} ${noun}${chat.memberCount === 1 ? '' : 's'}${handle}`;
  });

  constructor() {
    effect(() => {
      const chatId = this.chatId();
      untracked(() => {
        this.chats.activeChatId.set(chatId);
        this.replyTo.set(null);
        this.editing.set(null);
        void this.store.open(chatId);
        this.chats.loadChat(chatId).catch((error) => {
          this.toast.error(toProblem(error).status === 404 ? 'This chat no longer exists.' : error);
          void this.router.navigate(['/chats']);
        });
      });
    });

    effect(() => {
      const peerId = this.chat()?.peer?.id;
      if (peerId) untracked(() => this.presence.watch([peerId]));
    });

    // Mark messages read while the newest one is on screen and the tab is visible.
    let readTimer: ReturnType<typeof setTimeout> | undefined;
    const scheduleRead = () => {
      clearTimeout(readTimer);
      readTimer = setTimeout(() => {
        if (this.atBottom() && document.visibilityState === 'visible' && this.chat()?.myRole) {
          void this.store.markRead();
        }
      }, 400);
    };
    effect(() => {
      this.store.lastConfirmed();
      this.atBottom();
      this.chat();
      untracked(scheduleRead);
    });
    document.addEventListener('visibilitychange', scheduleRead);

    this.chats.removed.pipe(takeUntilDestroyed()).subscribe((chatId) => {
      if (chatId === this.chatId()) {
        this.toast.info('You no longer have access to this chat.');
        void this.router.navigate(['/chats']);
      }
    });

    inject(DestroyRef).onDestroy(() => {
      clearTimeout(readTimer);
      document.removeEventListener('visibilitychange', scheduleRead);
      if (this.chats.activeChatId() === this.chatId()) {
        this.chats.activeChatId.set(null);
      }
    });
  }

  protected async deleteMessage(message: ChatMessage): Promise<void> {
    const ok = await this.dialogs.confirm({
      title: 'Delete message?',
      message: 'It will be removed for everyone in this chat.',
      confirmText: 'Delete',
      danger: true,
    });
    if (ok) {
      this.store.delete(message.id).catch((error) => this.toast.error(error));
    }
  }

  protected retry(message: ChatMessage): void {
    this.store.retry(message.id).catch((error) => this.toast.error(error));
  }

  protected openUser(userId: string): void {
    this.dialogs.open(UserProfileDialog, { userId });
  }

  protected async join(): Promise<void> {
    this.joining.set(true);
    try {
      const chat = await firstValueFrom(this.chatsApi.join(this.chatId()));
      this.chats.setDetails(chat);
      await this.store.open(chat.id);
    } catch (error) {
      this.toast.error(error);
    } finally {
      this.joining.set(false);
    }
  }

  protected async toggleMute(): Promise<void> {
    const chat = this.chat();
    if (!chat) return;
    try {
      await firstValueFrom(this.chatsApi.mute(chat.id, !chat.isMuted));
      this.chats.setMuted(chat.id, !chat.isMuted);
    } catch (error) {
      this.toast.error(error);
    }
  }

  protected onDragOver(event: DragEvent): void {
    if (this.allowPost() && event.dataTransfer?.types.includes('Files')) {
      event.preventDefault();
      this.dragging.set(true);
    }
  }

  protected onDrop(event: DragEvent): void {
    this.dragging.set(false);
    const files = Array.from(event.dataTransfer?.files ?? []);
    if (files.length && this.allowPost()) {
      event.preventDefault();
      this.composer()?.addFiles(files);
    }
  }
}
