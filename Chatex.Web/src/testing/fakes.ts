import { signal } from '@angular/core';
import { Subject } from 'rxjs';
import { MessageDto, MyProfileDto, UserSummaryDto } from '../app/core/api/models';
import { ConnectionState, HubEvent } from '../app/core/realtime/realtime.service';

export const ME: MyProfileDto = {
  id: '00000000-0000-7000-8000-00000000000a',
  userName: 'alice',
  email: 'alice@example.test',
  displayName: 'Alice',
  bio: null,
  hasAvatar: false,
  createdAt: '2026-11-01T10:00:00Z',
};

export const BOB: UserSummaryDto = {
  id: '00000000-0000-7000-8000-00000000000b',
  userName: 'bob',
  displayName: 'Bob',
  hasAvatar: false,
};

/** A stand-in for RealtimeService: tests push hub events through `events`. */
export function fakeRealtime() {
  return {
    state: signal<ConnectionState>('connected'),
    events: new Subject<HubEvent>(),
    connected: new Subject<{ reconnected: boolean }>(),
    sendTyping: vi.fn(),
    getOnlineUsers: vi.fn(async () => [] as string[]),
  };
}

export function fakeAuth(user: MyProfileDto | null = ME) {
  return {
    user: signal(user),
    sessionEpoch: signal(0),
    isAuthenticated: signal(!!user),
  };
}

/** Message ids sort like the server's UUIDv7 ids: by the number given. */
export function messageId(n: number): string {
  return `00000000-0000-7000-8000-${n.toString().padStart(12, '0')}`;
}

export function message(n: number, overrides: Partial<MessageDto> = {}): MessageDto {
  return {
    id: messageId(n),
    chatId: 'chat-1',
    sender: BOB,
    body: `message ${n}`,
    replyToMessageId: null,
    createdAt: new Date(Date.UTC(2026, 10, 10, 12, 0, n)).toISOString(),
    editedAt: null,
    isDeleted: false,
    attachments: [],
    ...overrides,
  };
}

/** Lets pending promise callbacks (firstValueFrom, async/await) run. */
export function settle(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
}
