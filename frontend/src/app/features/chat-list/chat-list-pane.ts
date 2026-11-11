import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  computed,
  effect,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { chatTitle } from '../../core/api/models';
import { AuthService } from '../../core/auth/auth.service';
import { PresenceStore } from '../../core/realtime/presence.store';
import { TypingStore } from '../../core/realtime/typing.store';
import { ChatListItem, ChatsStore } from '../../state/chats.store';
import { Avatar } from '../../shared/ui/avatar';
import { Dialogs } from '../../shared/ui/dialogs';
import { ICONS } from '../../shared/ui/icons';
import { ShortWhenPipe } from '../../shared/ui/pipes';
import { NewChatDialog } from '../new-chat/new-chat-dialog';

@Component({
  selector: 'app-chat-list-pane',
  imports: [RouterLink, RouterLinkActive, Avatar, ShortWhenPipe, ICONS],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="flex items-center gap-2 px-4 pt-safe">
      <h1 class="flex-1 py-4 text-xl font-bold tracking-tight">Chats</h1>
      <button type="button" class="icon-btn" aria-label="New chat" title="New chat" (click)="newChat()">
        <svg lucideSquarePen class="size-5"></svg>
      </button>
    </header>
    <div class="px-4 pb-3">
      <label class="relative block">
        <span class="sr-only">Filter chats</span>
        <svg lucideSearch class="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted"></svg>
        <input
          class="input border-transparent bg-subtle pl-9"
          type="search"
          placeholder="Search chats"
          [value]="filter()"
          (input)="filter.set($any($event.target).value)"
        />
      </label>
    </div>

    <div class="min-h-0 flex-1 overflow-y-auto pb-20 md:pb-2">
      @if (!store.loaded() && store.loading()) {
        <ul aria-hidden="true">
          @for (i of skeleton; track i) {
            <li class="flex animate-pulse items-center gap-3 px-4 py-3">
              <span class="size-12 rounded-full bg-subtle"></span>
              <span class="flex-1 space-y-2">
                <span class="block h-3 w-1/2 rounded bg-subtle"></span>
                <span class="block h-3 w-3/4 rounded bg-subtle"></span>
              </span>
            </li>
          }
        </ul>
      } @else if (store.loaded() && !store.items().length) {
        <div class="flex flex-col items-center gap-3 px-6 py-16 text-center">
          <span class="flex size-14 items-center justify-center rounded-full bg-brand-50 text-brand-600 dark:bg-brand-950 dark:text-brand-300">
            <svg lucideMessagesSquare class="size-7"></svg>
          </span>
          <p class="font-medium">No chats yet</p>
          <p class="text-sm text-muted">Start a conversation or find a public group.</p>
          <div class="mt-2 flex gap-2">
            <button type="button" class="btn-primary" (click)="newChat()">New chat</button>
            <a routerLink="/discover" class="btn-secondary">Discover</a>
          </div>
        </div>
      } @else if (!visible().length) {
        <p class="px-4 py-10 text-center text-sm text-muted">No chats match “{{ filter() }}”.</p>
      }

      <ul>
        @for (chat of visible(); track chat.id) {
          <li>
            <a
              [routerLink]="['/chats', chat.id]"
              routerLinkActive="!bg-brand-50 dark:!bg-brand-950/60"
              class="flex items-center gap-3 px-4 py-2.5 transition-colors hover:bg-subtle"
            >
              <app-avatar
                [kind]="chat.type === 'direct' ? 'user' : 'chat'"
                [id]="chat.type === 'direct' ? chat.peer?.id : chat.id"
                [name]="title(chat)"
                [hasAvatar]="chat.type === 'direct' ? !!chat.peer?.hasAvatar : chat.hasAvatar"
                [online]="chat.type === 'direct' && presence.isOnline(chat.peer?.id)"
                [size]="48"
              />
              <span class="min-w-0 flex-1">
                <span class="flex items-center gap-1.5">
                  @if (chat.type === 'channel') {
                    <svg lucideMegaphone class="size-3.5 shrink-0 text-muted"></svg>
                  } @else if (chat.type === 'group') {
                    <svg lucideUsers class="size-3.5 shrink-0 text-muted"></svg>
                  }
                  <span class="truncate font-medium">{{ title(chat) }}</span>
                  @if (chat.isMuted) {
                    <svg lucideBellOff class="size-3.5 shrink-0 text-muted" aria-label="Muted"></svg>
                  }
                  <span class="ml-auto shrink-0 pl-2 text-xs text-muted">{{ chat.lastMessage?.createdAt ?? chat.lastActivityAt | shortWhen }}</span>
                </span>
                <span class="mt-0.5 flex items-center gap-2">
                  <span class="min-w-0 flex-1 truncate text-sm text-muted">
                    @if (typingIn(chat.id); as typing) {
                      <span class="text-brand-600 dark:text-brand-400">{{ typing }}</span>
                    } @else {
                      {{ preview(chat) }}
                    }
                  </span>
                  @if (chat.unreadCount) {
                    <span class="badge" [class]="chat.isMuted ? 'bg-zinc-400 text-white dark:bg-zinc-600' : 'bg-brand-600 text-white'">
                      {{ chat.unreadCount > 99 ? '99+' : chat.unreadCount }}
                    </span>
                  }
                </span>
              </span>
            </a>
          </li>
        }
      </ul>
      @if (store.hasMore() && !filter()) {
        <div #sentinel class="flex justify-center py-4 text-muted">
          @if (store.loadingMore()) {
            <svg lucideLoaderCircle class="size-5 animate-spin"></svg>
          }
        </div>
      }
    </div>
  `,
})
export class ChatListPane {
  protected readonly store = inject(ChatsStore);
  protected readonly presence = inject(PresenceStore);
  private readonly typing = inject(TypingStore);
  private readonly me = inject(AuthService).user;
  private readonly dialogs = inject(Dialogs);

  protected readonly skeleton = [1, 2, 3, 4, 5, 6];
  protected readonly filter = signal('');
  protected readonly visible = computed(() => {
    const q = this.filter().trim().toLowerCase();
    const items = this.store.items();
    return q ? items.filter((c) => this.title(c).toLowerCase().includes(q)) : items;
  });

  private readonly sentinel = viewChild<ElementRef<HTMLElement>>('sentinel');

  constructor() {
    // Infinite scroll: load the next page when the bottom sentinel comes into view.
    effect((onCleanup) => {
      const element = this.sentinel()?.nativeElement;
      if (!element) {
        return;
      }
      const observer = new IntersectionObserver((entries) => {
        if (entries.some((e) => e.isIntersecting)) {
          void this.store.loadMore();
        }
      });
      observer.observe(element);
      onCleanup(() => observer.disconnect());
    });

    // Show presence dots for direct-chat partners.
    effect(() => {
      const peers = this.store
        .items()
        .map((c) => c.peer?.id)
        .filter((id): id is string => !!id);
      this.presence.watch(peers);
    });
  }

  protected title(chat: ChatListItem): string {
    return chatTitle(chat);
  }

  protected preview(chat: ChatListItem): string {
    const last = chat.lastMessage;
    if (!last) {
      return chat.type === 'channel' ? 'Channel created' : 'No messages yet';
    }
    const prefix = last.senderId === this.me()?.id && chat.type !== 'channel' ? 'You: ' : '';
    if (last.deleted) {
      return `${prefix}Message deleted`;
    }
    return prefix + (last.body || (last.hasAttachments !== false ? '📎 Attachment' : ''));
  }

  protected typingIn(chatId: string): string | null {
    const count = this.typing.usersTyping(chatId).length;
    return count ? (count === 1 ? 'typing…' : `${count} people typing…`) : null;
  }

  protected newChat(): void {
    this.dialogs.open(NewChatDialog);
  }
}
