import { Injectable, computed, effect, inject, signal } from '@angular/core';
import { Subject, firstValueFrom } from 'rxjs';
import { ChatsApi, avatarUrl } from '../core/api/chatex-api';
import { ChatDto, ChatListItemDto, LastMessageDto, MessageDto } from '../core/api/models';
import { AuthService } from '../core/auth/auth.service';
import { BlobUrlService } from '../core/media/blob-url.service';
import { HubEvent, RealtimeService } from '../core/realtime/realtime.service';

/** A chat list row; `lastMessage.deleted` is set locally when the last message gets deleted. */
export type ChatListItem = ChatListItemDto & {
  lastMessage?: (LastMessageDto & { deleted?: boolean; hasAttachments?: boolean }) | null;
};

const byActivity = (a: ChatListItem, b: ChatListItem) =>
  a.lastActivityAt < b.lastActivityAt ? 1 : a.lastActivityAt > b.lastActivityAt ? -1 : 0;

/** The user's chats (most recently active first), kept current by hub events. */
@Injectable({ providedIn: 'root' })
export class ChatsStore {
  private readonly api = inject(ChatsApi);
  private readonly auth = inject(AuthService);
  private readonly blobs = inject(BlobUrlService);
  private nextCursor: string | null = null;
  private reloadTimer: ReturnType<typeof setTimeout> | undefined;

  readonly items = signal<ChatListItem[]>([]);
  readonly loaded = signal(false);
  readonly loading = signal(false);
  readonly loadingMore = signal(false);
  readonly hasMore = signal(false);
  /** Full details of chats that were opened, by id. */
  readonly details = signal<Readonly<Record<string, ChatDto>>>({});
  /** The chat on screen; its new messages don't count as unread. */
  readonly activeChatId = signal<string | null>(null);
  /** Fires when the user loses access to a chat (deleted, removed, left). */
  readonly removed = new Subject<string>();

  readonly totalUnread = computed(() =>
    this.items().reduce((sum, chat) => sum + (chat.isMuted ? 0 : chat.unreadCount), 0),
  );

  constructor() {
    const realtime = inject(RealtimeService);
    realtime.events.subscribe((event) => this.apply(event));
    realtime.connected.subscribe(({ reconnected }) => {
      if (reconnected && this.loaded()) {
        void this.loadFirst();
      }
    });
    effect(() => {
      this.auth.sessionEpoch();
      this.reset();
    });
  }

  async loadFirst(): Promise<void> {
    this.loading.set(true);
    try {
      const page = await firstValueFrom(this.api.list());
      this.items.set([...page.items].sort(byActivity));
      this.nextCursor = page.nextCursor ?? null;
      this.hasMore.set(!!this.nextCursor);
      this.loaded.set(true);
    } finally {
      this.loading.set(false);
    }
  }

  async loadMore(): Promise<void> {
    if (!this.nextCursor || this.loadingMore()) {
      return;
    }
    this.loadingMore.set(true);
    try {
      const page = await firstValueFrom(this.api.list(this.nextCursor));
      const known = new Set(this.items().map((c) => c.id));
      this.items.update((list) => [...list, ...page.items.filter((c) => !known.has(c.id))]);
      this.nextCursor = page.nextCursor ?? null;
      this.hasMore.set(!!this.nextCursor);
    } finally {
      this.loadingMore.set(false);
    }
  }

  /** Loads (or reloads) a chat's details and syncs its list row. */
  async loadChat(chatId: string): Promise<ChatDto> {
    const chat = await firstValueFrom(this.api.get(chatId));
    this.setDetails(chat);
    return chat;
  }

  /** Stores fresh chat details (from any API response) and patches the list row. */
  setDetails(chat: ChatDto): void {
    const previous = this.details()[chat.id];
    if (previous && previous.hasAvatar !== chat.hasAvatar) {
      this.blobs.invalidate(avatarUrl.chat(chat.id));
    }
    this.details.update((all) => ({ ...all, [chat.id]: chat }));
    if (!chat.myRole) {
      this.items.update((list) => list.filter((c) => c.id !== chat.id));
      return;
    }
    const role = chat.myRole;
    const exists = this.items().some((c) => c.id === chat.id);
    if (exists) {
      this.patch(chat.id, (c) => ({
        ...c,
        title: chat.title,
        hasAvatar: chat.hasAvatar,
        isMuted: chat.isMuted,
        myRole: role,
        peer: chat.peer,
      }));
    } else {
      const item: ChatListItem = {
        id: chat.id,
        type: chat.type,
        title: chat.title,
        hasAvatar: chat.hasAvatar,
        lastActivityAt: new Date().toISOString(),
        myRole: role,
        isMuted: chat.isMuted,
        lastMessage: null,
        unreadCount: 0,
        peer: chat.peer,
      };
      this.items.update((list) => [item, ...list]);
    }
  }

  /**
   * Forget a chat the user can no longer see. `notify` announces it on `removed` (the open
   * conversation then closes with a notice); pass false when the user left or deleted it here.
   */
  remove(chatId: string, notify = true): void {
    this.items.update((list) => list.filter((c) => c.id !== chatId));
    this.details.update((all) => {
      const next = { ...all };
      delete next[chatId];
      return next;
    });
    if (notify) {
      this.removed.next(chatId);
    }
  }

  markReadLocally(chatId: string): void {
    this.patch(chatId, (c) => (c.unreadCount ? { ...c, unreadCount: 0 } : c));
  }

  setMuted(chatId: string, muted: boolean): void {
    this.patch(chatId, (c) => ({ ...c, isMuted: muted }));
    const chat = this.details()[chatId];
    if (chat) {
      this.details.update((all) => ({ ...all, [chatId]: { ...chat, isMuted: muted } }));
    }
  }

  /** Keeps the list in step with a message the user just sent. */
  noteOwnMessage(message: MessageDto): void {
    this.applyMessageCreated(message);
  }

  private apply(event: HubEvent): void {
    const me = this.auth.user()?.id;
    switch (event.type) {
      case 'messageCreated':
        this.applyMessageCreated(event.message);
        break;
      case 'messageUpdated':
        this.patch(event.message.chatId, (c) =>
          c.lastMessage?.id === event.message.id
            ? { ...c, lastMessage: { ...c.lastMessage, body: event.message.body ?? '' } }
            : c,
        );
        break;
      case 'messageDeleted':
        this.patch(event.chatId, (c) =>
          c.lastMessage?.id === event.messageId
            ? { ...c, lastMessage: { ...c.lastMessage, body: '', deleted: true } }
            : c,
        );
        break;
      case 'membersAdded':
        if (me && event.userIds.includes(me)) {
          this.scheduleReload();
        } else if (this.details()[event.chatId]) {
          void this.loadChat(event.chatId).catch(() => undefined);
        }
        break;
      case 'memberRemoved':
        if (event.userId === me) {
          this.remove(event.chatId);
        } else if (this.details()[event.chatId]) {
          void this.loadChat(event.chatId).catch(() => undefined);
        }
        break;
      case 'chatUpdated':
        this.blobs.invalidate(avatarUrl.chat(event.chatId));
        void this.loadChat(event.chatId).catch(() => undefined);
        break;
      case 'chatDeleted':
        this.remove(event.chatId);
        break;
    }
  }

  private applyMessageCreated(message: MessageDto): void {
    const chat = this.items().find((c) => c.id === message.chatId);
    if (!chat) {
      // A chat we don't have yet (e.g. someone started a direct chat with us).
      this.scheduleReload();
      return;
    }
    if (chat.lastMessage && chat.lastMessage.id >= message.id) {
      return;
    }
    const mine = message.sender.id === this.auth.user()?.id;
    const visible =
      this.activeChatId() === message.chatId && document.visibilityState === 'visible';
    this.items.update((list) =>
      list
        .map((c) =>
          c.id === message.chatId
            ? {
                ...c,
                lastActivityAt: message.createdAt,
                lastMessage: {
                  id: message.id,
                  senderId: message.sender.id,
                  body: (message.body ?? '').slice(0, 100),
                  createdAt: message.createdAt,
                  hasAttachments: message.attachments.length > 0,
                },
                unreadCount: mine || visible ? c.unreadCount : c.unreadCount + 1,
              }
            : c,
        )
        .sort(byActivity),
    );
  }

  private patch(chatId: string, change: (chat: ChatListItem) => ChatListItem): void {
    this.items.update((list) => list.map((c) => (c.id === chatId ? change(c) : c)));
  }

  private scheduleReload(): void {
    clearTimeout(this.reloadTimer);
    this.reloadTimer = setTimeout(() => void this.loadFirst().catch(() => undefined), 300);
  }

  private reset(): void {
    clearTimeout(this.reloadTimer);
    this.items.set([]);
    this.details.set({});
    this.loaded.set(false);
    this.hasMore.set(false);
    this.nextCursor = null;
    this.activeChatId.set(null);
  }
}
