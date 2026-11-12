import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { BOB, ME, fakeAuth, fakeRealtime, message, messageId } from '../../testing/fakes';
import { ChatsApi } from '../core/api/chatex-api';
import { ChatListItemDto } from '../core/api/models';
import { AuthService } from '../core/auth/auth.service';
import { BlobUrlService } from '../core/media/blob-url.service';
import { RealtimeService } from '../core/realtime/realtime.service';
import { ChatsStore } from './chats.store';

function item(
  id: string,
  lastActivityAt: string,
  overrides: Partial<ChatListItemDto> = {},
): ChatListItemDto {
  return {
    id,
    type: 'group',
    title: id,
    hasAvatar: false,
    lastActivityAt,
    myRole: 'member',
    isMuted: false,
    lastMessage: null,
    unreadCount: 0,
    peer: null,
    ...overrides,
  };
}

describe('ChatsStore', () => {
  let store: ChatsStore;
  let realtime: ReturnType<typeof fakeRealtime>;

  beforeEach(async () => {
    Object.defineProperty(document, 'visibilityState', { value: 'visible', configurable: true });
    realtime = fakeRealtime();
    const api = {
      list: vi.fn(() =>
        of({
          items: [
            item('chat-old', '2026-11-01T00:00:00Z'),
            item('chat-1', '2026-11-05T00:00:00Z'),
            item('chat-muted', '2026-11-03T00:00:00Z', { isMuted: true, unreadCount: 4 }),
          ],
          nextCursor: null,
        }),
      ),
    };
    TestBed.configureTestingModule({
      providers: [
        { provide: ChatsApi, useValue: api },
        { provide: AuthService, useValue: fakeAuth() },
        { provide: BlobUrlService, useValue: { invalidate: vi.fn() } },
        { provide: RealtimeService, useValue: realtime },
      ],
    });
    store = TestBed.inject(ChatsStore);
    TestBed.tick();
    await store.loadFirst();
  });

  const order = () => store.items().map((c) => c.id);
  const get = (id: string) => store.items().find((c) => c.id === id)!;

  it('lists chats by latest activity', () => {
    expect(order()).toEqual(['chat-1', 'chat-muted', 'chat-old']);
  });

  it('moves a chat with a new message to the top and counts it unread', () => {
    realtime.events.next({
      type: 'messageCreated',
      message: message(10, { chatId: 'chat-old', createdAt: '2026-11-10T00:00:00Z' }),
    });

    expect(order()[0]).toBe('chat-old');
    expect(get('chat-old').unreadCount).toBe(1);
    expect(get('chat-old').lastMessage?.id).toBe(messageId(10));
  });

  it("doesn't count the user's own messages or the open chat as unread", () => {
    store.activeChatId.set('chat-old');
    realtime.events.next({ type: 'messageCreated', message: message(11, { chatId: 'chat-old' }) });
    realtime.events.next({
      type: 'messageCreated',
      message: message(12, { chatId: 'chat-1', sender: { ...BOB, id: ME.id } }),
    });

    expect(get('chat-old').unreadCount).toBe(0);
    expect(get('chat-1').unreadCount).toBe(0);
  });

  it('leaves muted chats out of the total unread count', () => {
    realtime.events.next({ type: 'messageCreated', message: message(13, { chatId: 'chat-1' }) });
    expect(store.totalUnread()).toBe(1);
  });

  it('marks the last message deleted', () => {
    realtime.events.next({ type: 'messageCreated', message: message(14, { chatId: 'chat-1' }) });
    realtime.events.next({ type: 'messageDeleted', chatId: 'chat-1', messageId: messageId(14) });

    expect(get('chat-1').lastMessage).toMatchObject({ deleted: true, body: '' });
  });

  it('drops chats that were deleted or that the user was removed from', () => {
    const removed: string[] = [];
    store.removed.subscribe((id) => removed.push(id));

    realtime.events.next({ type: 'chatDeleted', chatId: 'chat-1' });
    realtime.events.next({ type: 'memberRemoved', chatId: 'chat-old', userId: ME.id });
    realtime.events.next({ type: 'memberRemoved', chatId: 'chat-muted', userId: BOB.id });

    expect(order()).toEqual(['chat-muted']);
    expect(removed).toEqual(['chat-1', 'chat-old']);
  });

  it('clears unread counts locally', () => {
    store.markReadLocally('chat-muted');
    expect(get('chat-muted').unreadCount).toBe(0);
  });
});
