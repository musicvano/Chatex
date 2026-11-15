import { Injectable, effect, inject, signal } from '@angular/core';
import { AuthService } from '../auth/auth.service';
import { RealtimeService } from './realtime.service';

/** Who is online, from UserOnline/UserOffline events plus GetOnlineUsers for the users on screen. */
@Injectable({ providedIn: 'root' })
export class PresenceStore {
  private readonly realtime = inject(RealtimeService);
  private readonly online = signal<ReadonlySet<string>>(new Set());
  private readonly lastSeen = signal<Readonly<Record<string, string>>>({});
  private readonly watched = new Set<string>();

  constructor() {
    this.realtime.events.subscribe((event) => {
      if (event.type === 'userOnline') {
        this.setOnline([event.userId], true);
      } else if (event.type === 'userOffline') {
        this.setOnline([event.userId], false);
        this.lastSeen.update((map) => ({ ...map, [event.userId]: event.lastSeenAt }));
      }
    });
    // Presence may have changed while disconnected: ask again for everyone shown.
    this.realtime.connected.subscribe(() => void this.query([...this.watched]));

    const auth = inject(AuthService);
    effect(() => {
      auth.sessionEpoch();
      this.watched.clear();
      this.online.set(new Set());
      this.lastSeen.set({});
    });
  }

  isOnline(userId: string | null | undefined): boolean {
    return !!userId && this.online().has(userId);
  }

  /** A live "last seen" time, newer than what the API returned when the user went offline here. */
  lastSeenAt(userId: string): string | undefined {
    return this.lastSeen()[userId];
  }

  /** Start showing presence for these users. */
  watch(userIds: readonly string[]): void {
    const fresh = userIds.filter((id) => !this.watched.has(id));
    fresh.forEach((id) => this.watched.add(id));
    void this.query(fresh);
  }

  private async query(userIds: string[]): Promise<void> {
    for (let i = 0; i < userIds.length; i += 100) {
      const batch = userIds.slice(i, i + 100);
      const online = new Set(await this.realtime.getOnlineUsers(batch));
      this.online.update((current) => {
        const next = new Set(current);
        batch.forEach((id) => (online.has(id) ? next.add(id) : next.delete(id)));
        return next;
      });
    }
  }

  private setOnline(userIds: string[], online: boolean): void {
    this.online.update((current) => {
      const next = new Set(current);
      userIds.forEach((id) => (online ? next.add(id) : next.delete(id)));
      return next;
    });
  }
}
