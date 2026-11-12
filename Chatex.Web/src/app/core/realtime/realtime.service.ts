import { Injectable, effect, inject, signal, untracked } from '@angular/core';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { MessageDto } from '../api/models';

export type ConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

/** Server → client events of the /hubs/chat hub. */
export type HubEvent =
  | { type: 'messageCreated'; message: MessageDto }
  | { type: 'messageUpdated'; message: MessageDto }
  | { type: 'messageDeleted'; chatId: string; messageId: string }
  | { type: 'messagesRead'; chatId: string; userId: string; lastReadMessageId: string }
  | { type: 'membersAdded'; chatId: string; userIds: string[] }
  | { type: 'memberRemoved'; chatId: string; userId: string }
  | { type: 'chatUpdated'; chatId: string }
  | { type: 'chatDeleted'; chatId: string }
  | { type: 'typing'; chatId: string; userId: string }
  | { type: 'userOnline'; userId: string }
  | { type: 'userOffline'; userId: string; lastSeenAt: string };

const TYPING_THROTTLE_MS = 3_000;
const RETRY_DELAYS_MS = [0, 2_000, 5_000, 10_000, 30_000];

/**
 * The SignalR connection. It runs while the user is signed in, reconnects on its own, and after a
 * reconnect emits `connected` with `reconnected: true` so stores can fetch what they missed.
 */
@Injectable({ providedIn: 'root' })
export class RealtimeService {
  private readonly auth = inject(AuthService);
  private connection: HubConnection | null = null;
  private retryTimer: ReturnType<typeof setTimeout> | undefined;
  private attempt = 0;
  private everConnected = false;
  private readonly lastTypingSent = new Map<string, number>();

  readonly state = signal<ConnectionState>('disconnected');
  readonly events = new Subject<HubEvent>();
  /** Fires on every (re)connect; `reconnected` is true when there may be a gap to fill. */
  readonly connected = new Subject<{ reconnected: boolean }>();

  constructor() {
    effect(() => {
      const signedIn = this.auth.isAuthenticated();
      untracked(() => (signedIn ? this.start() : void this.stop()));
    });
  }

  /** Tells the chat's other members that the user is typing, at most once every 3 seconds. */
  sendTyping(chatId: string): void {
    const now = Date.now();
    if (now - (this.lastTypingSent.get(chatId) ?? 0) < TYPING_THROTTLE_MS || !this.isConnected()) {
      return;
    }
    this.lastTypingSent.set(chatId, now);
    this.connection!.invoke('Typing', chatId).catch(() => undefined);
  }

  /** Which of these users are online right now (empty while disconnected). */
  async getOnlineUsers(userIds: string[]): Promise<string[]> {
    if (!userIds.length || !this.isConnected()) {
      return [];
    }
    try {
      return await this.connection!.invoke<string[]>('GetOnlineUsers', userIds);
    } catch {
      return [];
    }
  }

  private isConnected(): boolean {
    return this.connection?.state === HubConnectionState.Connected;
  }

  private start(): void {
    if (this.connection) {
      return;
    }
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/chat', { accessTokenFactory: async () => (await this.auth.getValidToken()) ?? '' })
      .withAutomaticReconnect([0, 2_000, 5_000, 10_000, 20_000, 30_000])
      .configureLogging(LogLevel.Warning)
      .build();

    const emit = (event: HubEvent) => this.events.next(event);
    connection.on('MessageCreated', (message: MessageDto) => emit({ type: 'messageCreated', message }));
    connection.on('MessageUpdated', (message: MessageDto) => emit({ type: 'messageUpdated', message }));
    connection.on('MessageDeleted', (chatId: string, messageId: string) =>
      emit({ type: 'messageDeleted', chatId, messageId }),
    );
    connection.on('MessagesRead', (chatId: string, userId: string, lastReadMessageId: string) =>
      emit({ type: 'messagesRead', chatId, userId, lastReadMessageId }),
    );
    connection.on('MembersAdded', (chatId: string, userIds: string[]) =>
      emit({ type: 'membersAdded', chatId, userIds }),
    );
    connection.on('MemberRemoved', (chatId: string, userId: string) =>
      emit({ type: 'memberRemoved', chatId, userId }),
    );
    connection.on('ChatUpdated', (chatId: string) => emit({ type: 'chatUpdated', chatId }));
    connection.on('ChatDeleted', (chatId: string) => emit({ type: 'chatDeleted', chatId }));
    connection.on('Typing', (chatId: string, userId: string) => emit({ type: 'typing', chatId, userId }));
    connection.on('UserOnline', (userId: string) => emit({ type: 'userOnline', userId }));
    connection.on('UserOffline', (userId: string, lastSeenAt: string) =>
      emit({ type: 'userOffline', userId, lastSeenAt }),
    );

    connection.onreconnecting(() => this.state.set('reconnecting'));
    connection.onreconnected(() => {
      this.state.set('connected');
      this.connected.next({ reconnected: true });
    });
    connection.onclose(() => {
      // Automatic reconnect gave up (or the server is down): keep trying while signed in.
      if (this.connection === connection) {
        this.state.set('disconnected');
        this.scheduleRetry(connection);
      }
    });

    this.connection = connection;
    this.attempt = 0;
    void this.connect(connection);
  }

  private async connect(connection: HubConnection): Promise<void> {
    if (this.connection !== connection) {
      return;
    }
    this.state.set(this.everConnected ? 'reconnecting' : 'connecting');
    try {
      await connection.start();
      if (this.connection !== connection) {
        return;
      }
      const reconnected = this.everConnected;
      this.everConnected = true;
      this.attempt = 0;
      this.state.set('connected');
      this.connected.next({ reconnected });
    } catch {
      if (this.connection === connection) {
        this.state.set('disconnected');
        this.scheduleRetry(connection);
      }
    }
  }

  private scheduleRetry(connection: HubConnection): void {
    clearTimeout(this.retryTimer);
    const delay = RETRY_DELAYS_MS[Math.min(++this.attempt, RETRY_DELAYS_MS.length - 1)];
    this.retryTimer = setTimeout(() => void this.connect(connection), delay);
  }

  private async stop(): Promise<void> {
    clearTimeout(this.retryTimer);
    const connection = this.connection;
    this.connection = null;
    this.everConnected = false;
    this.lastTypingSent.clear();
    this.state.set('disconnected');
    await connection?.stop().catch(() => undefined);
  }
}
