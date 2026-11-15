import { Injectable, inject, signal } from '@angular/core';
import { AuthService } from '../auth/auth.service';
import { RealtimeService } from './realtime.service';

/** Typing indicators expire this long after the last Typing event. */
export const TYPING_TTL_MS = 5_000;

/** Who is typing in which chat. Typing is never stored on the server. */
@Injectable({ providedIn: 'root' })
export class TypingStore {
  private readonly auth = inject(AuthService);
  /** chatId → userId → expiry timestamp. */
  private readonly typing = signal<Readonly<Record<string, Readonly<Record<string, number>>>>>({});
  private readonly timers = new Map<string, ReturnType<typeof setTimeout>>();

  constructor() {
    inject(RealtimeService).events.subscribe((event) => {
      if (event.type === 'typing' && event.userId !== this.auth.user()?.id) {
        this.set(event.chatId, event.userId);
      } else if (event.type === 'messageCreated') {
        this.clear(event.message.chatId, event.message.sender.id);
      }
    });
  }

  /** Ids of the users typing in the chat. Reads a signal, so it works inside computed(). */
  usersTyping(chatId: string): string[] {
    return Object.keys(this.typing()[chatId] ?? {});
  }

  private set(chatId: string, userId: string): void {
    this.typing.update((all) => ({
      ...all,
      [chatId]: { ...all[chatId], [userId]: Date.now() + TYPING_TTL_MS },
    }));
    const key = `${chatId}:${userId}`;
    clearTimeout(this.timers.get(key));
    this.timers.set(
      key,
      setTimeout(() => this.clear(chatId, userId), TYPING_TTL_MS),
    );
  }

  private clear(chatId: string, userId: string): void {
    const key = `${chatId}:${userId}`;
    clearTimeout(this.timers.get(key));
    this.timers.delete(key);
    if (!this.typing()[chatId]?.[userId]) {
      return;
    }
    this.typing.update((all) => {
      const chat = { ...all[chatId] };
      delete chat[userId];
      return { ...all, [chatId]: chat };
    });
  }
}
