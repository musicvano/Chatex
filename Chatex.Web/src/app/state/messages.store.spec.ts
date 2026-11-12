import { TestBed } from '@angular/core/testing';
import { Subject, of, throwError } from 'rxjs';
import { BOB, ME, fakeAuth, fakeRealtime, message, messageId, settle } from '../../testing/fakes';
import { ChatsApi, MessagesApi } from '../core/api/chatex-api';
import { MessageDto, PageDto } from '../core/api/models';
import { AuthService } from '../core/auth/auth.service';
import { RealtimeService } from '../core/realtime/realtime.service';
import { ChatsStore } from './chats.store';
import { MessagesStore, merge } from './messages.store';

describe('MessagesStore', () => {
  let store: MessagesStore;
  let realtime: ReturnType<typeof fakeRealtime>;
  let api: {
    list: ReturnType<typeof vi.fn>;
    send: ReturnType<typeof vi.fn>;
    edit: ReturnType<typeof vi.fn>;
    delete: ReturnType<typeof vi.fn>;
  };
  let chats: {
    markReadLocally: ReturnType<typeof vi.fn>;
    noteOwnMessage: ReturnType<typeof vi.fn>;
  };
  let chatsApi: { markRead: ReturnType<typeof vi.fn> };

  const page = (items: MessageDto[], nextCursor: string | null = null): PageDto<MessageDto> => ({
    items,
    nextCursor,
  });
  const ids = () => store.messages().map((m) => m.id);

  beforeEach(() => {
    realtime = fakeRealtime();
    api = { list: vi.fn(), send: vi.fn(), edit: vi.fn(), delete: vi.fn() };
    chats = { markReadLocally: vi.fn(), noteOwnMessage: vi.fn() };
    chatsApi = { markRead: vi.fn(() => of(undefined)) };
    TestBed.configureTestingModule({
      providers: [
        MessagesStore,
        { provide: MessagesApi, useValue: api },
        { provide: ChatsApi, useValue: chatsApi },
        { provide: ChatsStore, useValue: chats },
        { provide: AuthService, useValue: fakeAuth() },
        { provide: RealtimeService, useValue: realtime },
      ],
    });
    store = TestBed.inject(MessagesStore);
  });

  async function openWith(newestFirst: MessageDto[], cursor: string | null = null) {
    api.list.mockReturnValueOnce(of(page(newestFirst, cursor)));
    await store.open('chat-1');
  }

  it('shows the newest page oldest first', async () => {
    await openWith([message(3), message(2), message(1)], 'cursor-1');

    expect(api.list).toHaveBeenCalledWith('chat-1', { limit: 50 });
    expect(ids()).toEqual([messageId(1), messageId(2), messageId(3)]);
    expect(store.hasOlder()).toBe(true);
  });

  it('prepends older pages using the cursor', async () => {
    await openWith([message(4), message(3)], 'cursor-1');
    api.list.mockReturnValueOnce(of(page([message(2), message(1)])));

    await store.loadOlder();

    expect(api.list).toHaveBeenLastCalledWith('chat-1', { before: 'cursor-1', limit: 50 });
    expect(ids()).toEqual([1, 2, 3, 4].map(messageId));
    expect(store.hasOlder()).toBe(false);
  });

  it('adds live messages once and ignores other chats', async () => {
    await openWith([message(1)]);

    realtime.events.next({ type: 'messageCreated', message: message(2) });
    realtime.events.next({ type: 'messageCreated', message: message(2) });
    realtime.events.next({ type: 'messageCreated', message: message(3, { chatId: 'other' }) });

    expect(ids()).toEqual([messageId(1), messageId(2)]);
  });

  it('applies edits and deletions from the hub', async () => {
    await openWith([message(2), message(1)]);

    realtime.events.next({
      type: 'messageUpdated',
      message: message(1, { body: 'edited', editedAt: '2026-11-10T12:05:00Z' }),
    });
    realtime.events.next({ type: 'messageDeleted', chatId: 'chat-1', messageId: messageId(2) });

    const [first, second] = store.messages();
    expect(first.body).toBe('edited');
    expect(second.isDeleted).toBe(true);
    expect(second.body).toBeNull();
  });

  it('shows a sent message at once and swaps in the server copy', async () => {
    await openWith([message(1)]);
    const response = new Subject<MessageDto>();
    api.send.mockReturnValueOnce(response);

    const sending = store.send('hello', []);
    expect(store.messages().at(-1)).toMatchObject({ body: 'hello', localState: 'pending' });

    const sent = message(2, {
      body: 'hello',
      sender: { ...BOB, id: ME.id, displayName: ME.displayName },
    });
    response.next(sent);
    response.complete();
    await sending;

    expect(ids()).toEqual([messageId(1), messageId(2)]);
    expect(store.messages().some((m) => m.localState)).toBe(false);
    expect(chats.noteOwnMessage).toHaveBeenCalledWith(sent);
  });

  it('keeps one copy when the hub event beats the POST response', async () => {
    await openWith([message(1)]);
    const response = new Subject<MessageDto>();
    api.send.mockReturnValueOnce(response);
    const sent = message(2, { body: 'hi' });

    const sending = store.send('hi', []);
    realtime.events.next({ type: 'messageCreated', message: sent });
    response.next(sent);
    response.complete();
    await sending;

    expect(ids()).toEqual([messageId(1), messageId(2)]);
  });

  it('marks a message failed when sending fails, and can retry it', async () => {
    await openWith([]);
    api.send.mockReturnValueOnce(throwError(() => new Error('offline')));

    await expect(store.send('hi', [])).rejects.toThrow('offline');
    const failed = store.messages()[0];
    expect(failed.localState).toBe('failed');

    api.send.mockReturnValueOnce(of(message(5, { body: 'hi' })));
    await store.retry(failed.id);
    expect(ids()).toEqual([messageId(5)]);
  });

  it('catches up after a reconnect by following the after cursor', async () => {
    await openWith([message(1)]);
    api.list
      .mockReturnValueOnce(of(page([message(2), message(3)], 'next')))
      .mockReturnValueOnce(of(page([message(4)], null)));

    realtime.connected.next({ reconnected: true });
    await settle();

    expect(api.list).toHaveBeenCalledWith('chat-1', { after: messageId(1), limit: 100 });
    expect(api.list).toHaveBeenCalledWith('chat-1', { after: 'next', limit: 100 });
    expect(ids()).toEqual([1, 2, 3, 4].map(messageId));
  });

  it('marks the newest message read once', async () => {
    await openWith([message(2), message(1)]);

    await store.markRead();
    await store.markRead();

    expect(chatsApi.markRead).toHaveBeenCalledTimes(1);
    expect(chatsApi.markRead).toHaveBeenCalledWith('chat-1', messageId(2));
    expect(chats.markReadLocally).toHaveBeenCalledWith('chat-1');
  });

  it('never uses a deleted message as the read position', async () => {
    await openWith([message(2, { isDeleted: true, body: null }), message(1)]);

    await store.markRead();

    expect(chatsApi.markRead).toHaveBeenCalledWith('chat-1', messageId(1));
  });

  it('tracks how far other members have read', async () => {
    await openWith([message(2), message(1)]);

    realtime.events.next({
      type: 'messagesRead',
      chatId: 'chat-1',
      userId: BOB.id,
      lastReadMessageId: messageId(2),
    });
    realtime.events.next({
      type: 'messagesRead',
      chatId: 'chat-1',
      userId: BOB.id,
      lastReadMessageId: messageId(1),
    });
    realtime.events.next({
      type: 'messagesRead',
      chatId: 'chat-1',
      userId: ME.id,
      lastReadMessageId: messageId(9),
    });

    expect(store.peerReadUpTo()).toBe(messageId(2));
  });

  it('drops responses for a chat that is no longer open', async () => {
    const slow = new Subject<PageDto<MessageDto>>();
    api.list.mockReturnValueOnce(slow);
    const first = store.open('chat-1');
    api.list.mockReturnValueOnce(of(page([message(7, { chatId: 'chat-2' })])));
    await store.open('chat-2');

    slow.next(page([message(1)]));
    slow.complete();
    await first;

    expect(ids()).toEqual([messageId(7)]);
  });
});

describe('merge', () => {
  it('orders by id and keeps pending messages last', () => {
    const pending = { ...message(0), id: 'local-1', localState: 'pending' as const };
    const merged = merge([message(3), pending], [message(1), message(3, { body: 'new' })]);

    expect(merged.map((m) => m.id)).toEqual([messageId(1), messageId(3), 'local-1']);
    expect(merged[1].body).toBe('new');
  });
});
