import { Injectable, computed, inject, signal } from '@angular/core';
import { DestroyRef } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ChatsApi, MessagesApi } from '../core/api/chatex-api';
import { AttachmentDto, MessageDto, PageDto, compareIds } from '../core/api/models';
import { AuthService } from '../core/auth/auth.service';
import { HubEvent, RealtimeService } from '../core/realtime/realtime.service';
import { ChatsStore } from './chats.store';

/** A message on screen. Messages being sent have `localState` and a temporary id. */
export type ChatMessage = MessageDto & { localState?: 'pending' | 'failed' };

export const PAGE_SIZE = 50;
const CATCH_UP_LIMIT = 100;

let localIds = 0;

/**
 * The open chat's messages, oldest first. History is keyset paged (`before=` for older pages,
 * `after=` to fill the gap after a reconnect), and hub events are merged in by id so nothing shows
 * twice. Provided by the conversation page, so each open chat gets its own instance state.
 */
@Injectable()
export class MessagesStore {
  private readonly api = inject(MessagesApi);
  private readonly chatsApi = inject(ChatsApi);
  private readonly chats = inject(ChatsStore);
  private readonly auth = inject(AuthService);

  private olderCursor: string | null = null;
  /** Bumped on every chat switch so late responses for the previous chat are dropped. */
  private generation = 0;
  private lastMarkedRead: string | null = null;

  readonly chatId = signal<string | null>(null);
  readonly messages = signal<ChatMessage[]>([]);
  readonly loading = signal(false);
  readonly loadingOlder = signal(false);
  readonly hasOlder = signal(false);
  readonly error = signal<unknown>(null);
  /** Read positions of other members, from live MessagesRead events. */
  readonly readBy = signal<Readonly<Record<string, string>>>({});

  readonly byId = computed(() => new Map(this.messages().map((m) => [m.id, m])));
  /** The newest message the server knows about (not a pending one). */
  readonly lastConfirmed = computed(() => {
    const list = this.messages();
    for (let i = list.length - 1; i >= 0; i--) {
      if (!list[i].localState) return list[i];
    }
    return null;
  });
  /** The newest message that can be a read position (the server rejects deleted ones). */
  readonly lastReadable = computed(() => {
    const list = this.messages();
    for (let i = list.length - 1; i >= 0; i--) {
      if (!list[i].localState && !list[i].isDeleted) return list[i];
    }
    return null;
  });
  /** The newest message any other member has read. */
  readonly peerReadUpTo = computed(() => {
    const ids = Object.values(this.readBy());
    return ids.length ? ids.reduce((max, id) => (id > max ? id : max)) : null;
  });

  constructor() {
    const realtime = inject(RealtimeService);
    const events = realtime.events.subscribe((event) => this.apply(event));
    const connected = realtime.connected.subscribe(({ reconnected }) => {
      if (reconnected) {
        void this.catchUp();
      }
    });
    inject(DestroyRef).onDestroy(() => {
      events.unsubscribe();
      connected.unsubscribe();
    });
  }

  async open(chatId: string): Promise<void> {
    this.generation++;
    this.chatId.set(chatId);
    this.messages.set([]);
    this.readBy.set({});
    this.olderCursor = null;
    this.hasOlder.set(false);
    this.lastMarkedRead = null;
    this.error.set(null);
    await this.loadInitial();
  }

  async loadInitial(): Promise<void> {
    const chatId = this.chatId();
    if (!chatId) {
      return;
    }
    const generation = this.generation;
    this.loading.set(true);
    try {
      const page = await firstValueFrom(this.api.list(chatId, { limit: PAGE_SIZE }));
      if (generation !== this.generation) {
        return;
      }
      this.messages.update((current) => merge(current, [...page.items].reverse()));
      this.olderCursor = page.nextCursor ?? null;
      this.hasOlder.set(!!this.olderCursor);
    } catch (error) {
      if (generation === this.generation) {
        this.error.set(error);
      }
    } finally {
      if (generation === this.generation) {
        this.loading.set(false);
      }
    }
  }

  async loadOlder(): Promise<void> {
    const chatId = this.chatId();
    if (!chatId || !this.olderCursor || this.loadingOlder()) {
      return;
    }
    const generation = this.generation;
    this.loadingOlder.set(true);
    try {
      const page = await firstValueFrom(
        this.api.list(chatId, { before: this.olderCursor, limit: PAGE_SIZE }),
      );
      if (generation !== this.generation) {
        return;
      }
      this.messages.update((current) => merge(current, [...page.items].reverse()));
      this.olderCursor = page.nextCursor ?? null;
      this.hasOlder.set(!!this.olderCursor);
    } finally {
      if (generation === this.generation) {
        this.loadingOlder.set(false);
      }
    }
  }

  /** Fetches everything newer than the last message on screen (after a reconnect). */
  async catchUp(): Promise<void> {
    const chatId = this.chatId();
    if (!chatId) {
      return;
    }
    let after = this.lastConfirmed()?.id;
    if (!after) {
      await this.loadInitial();
      return;
    }
    const generation = this.generation;
    for (let i = 0; i < 20 && after; i++) {
      const page: PageDto<MessageDto> = await firstValueFrom(
        this.api.list(chatId, { after, limit: CATCH_UP_LIMIT }),
      );
      if (generation !== this.generation) {
        return;
      }
      this.messages.update((current) => merge(current, page.items));
      after = page.nextCursor ?? undefined;
    }
  }

  /** Sends a message, showing it at once as pending. */
  async send(body: string, attachments: AttachmentDto[], replyToMessageId?: string | null) {
    const chatId = this.chatId();
    const me = this.auth.user();
    if (!chatId || !me) {
      return;
    }
    const localId = `local-${++localIds}`;
    const pending: ChatMessage = {
      id: localId,
      chatId,
      sender: { id: me.id, userName: me.userName, displayName: me.displayName, hasAvatar: me.hasAvatar },
      body,
      replyToMessageId: replyToMessageId ?? null,
      createdAt: new Date().toISOString(),
      isDeleted: false,
      attachments,
      localState: 'pending',
    };
    this.messages.update((list) => [...list, pending]);
    await this.deliver(pending);
  }

  /** Sends a failed message again. */
  async retry(localId: string): Promise<void> {
    const message = this.byId().get(localId);
    if (message?.localState === 'failed') {
      this.replace(localId, { ...message, localState: 'pending' });
      await this.deliver(message);
    }
  }

  discard(localId: string): void {
    this.messages.update((list) => list.filter((m) => m.id !== localId));
  }

  async edit(messageId: string, body: string): Promise<void> {
    const updated = await firstValueFrom(this.api.edit(messageId, body));
    this.upsert(updated);
  }

  async delete(messageId: string): Promise<void> {
    await firstValueFrom(this.api.delete(messageId));
    this.markDeleted(messageId);
  }

  /** Moves the read position to the newest message (once per message). */
  async markRead(): Promise<void> {
    const chatId = this.chatId();
    const last = this.lastReadable();
    if (!chatId || !last || last.id === this.lastMarkedRead) {
      return;
    }
    this.lastMarkedRead = last.id;
    this.chats.markReadLocally(chatId);
    try {
      await firstValueFrom(this.chatsApi.markRead(chatId, last.id));
    } catch {
      this.lastMarkedRead = null;
    }
  }

  private async deliver(pending: ChatMessage): Promise<void> {
    const generation = this.generation;
    try {
      const sent = await firstValueFrom(
        this.api.send(pending.chatId, {
          body: pending.body || null,
          replyToMessageId: pending.replyToMessageId,
          attachmentIds: pending.attachments.map((a) => a.id),
        }),
      );
      if (generation !== this.generation) {
        return;
      }
      // The hub event may have arrived first; either way keep exactly one copy.
      this.messages.update((list) => merge(list.filter((m) => m.id !== pending.id), [sent]));
      this.chats.noteOwnMessage(sent);
    } catch (error) {
      if (generation === this.generation) {
        this.replace(pending.id, { ...pending, localState: 'failed' });
      }
      throw error;
    }
  }

  private apply(event: HubEvent): void {
    const chatId = this.chatId();
    switch (event.type) {
      case 'messageCreated':
      case 'messageUpdated':
        if (event.message.chatId === chatId) {
          this.upsert(event.message);
        }
        break;
      case 'messageDeleted':
        if (event.chatId === chatId) {
          this.markDeleted(event.messageId);
        }
        break;
      case 'messagesRead':
        if (event.chatId === chatId && event.userId !== this.auth.user()?.id) {
          this.readBy.update((all) =>
            (all[event.userId] ?? '') >= event.lastReadMessageId
              ? all
              : { ...all, [event.userId]: event.lastReadMessageId },
          );
        }
        break;
    }
  }

  private upsert(message: MessageDto): void {
    this.messages.update((list) => merge(list, [message]));
  }

  private replace(id: string, message: ChatMessage): void {
    this.messages.update((list) => list.map((m) => (m.id === id ? message : m)));
  }

  private markDeleted(messageId: string): void {
    this.messages.update((list) =>
      list.map((m) =>
        m.id === messageId ? { ...m, isDeleted: true, body: null, attachments: [] } : m,
      ),
    );
  }
}

/**
 * Merges server messages into the list: replaces ones with the same id, keeps confirmed messages
 * ordered by id, and keeps pending messages at the end.
 */
export function merge(current: ChatMessage[], incoming: MessageDto[]): ChatMessage[] {
  if (!incoming.length) {
    return current;
  }
  const confirmed = new Map(current.filter((m) => !m.localState).map((m) => [m.id, m]));
  for (const message of incoming) {
    confirmed.set(message.id, message);
  }
  const pending = current.filter((m) => m.localState);
  return [...[...confirmed.values()].sort((a, b) => compareIds(a.id, b.id)), ...pending];
}
