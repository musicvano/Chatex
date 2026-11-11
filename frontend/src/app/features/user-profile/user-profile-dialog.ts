import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { ChangeDetectionStrategy, Component, computed, inject, resource, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ChatsApi, UsersApi } from '../../core/api/chatex-api';
import { AuthService } from '../../core/auth/auth.service';
import { PresenceStore } from '../../core/realtime/presence.store';
import { ToastService } from '../../core/ui/toast.service';
import { ChatsStore } from '../../state/chats.store';
import { Avatar } from '../../shared/ui/avatar';
import { ICONS } from '../../shared/ui/icons';
import { timeAgo } from '../../shared/ui/pipes';

@Component({
  selector: 'app-user-profile-dialog',
  imports: [Avatar, ICONS],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dialog-card relative p-6 text-center" role="dialog" aria-label="User profile">
      <button type="button" class="icon-btn absolute top-2 right-2" aria-label="Close" (click)="ref.close()">
        <svg lucideX class="size-5"></svg>
      </button>
      @if (user.value(); as u) {
        <div class="flex flex-col items-center gap-3">
          <app-avatar [id]="u.id" [name]="u.displayName" [hasAvatar]="u.hasAvatar" [size]="96" [online]="presence.isOnline(u.id)" />
          <div>
            <h2 class="text-xl font-semibold">{{ u.displayName }}</h2>
            <p class="text-sm text-muted">&#64;{{ u.userName }}</p>
            <p class="mt-1 text-xs" [class]="presence.isOnline(u.id) ? 'text-emerald-600 dark:text-emerald-400' : 'text-muted'">{{ status() }}</p>
          </div>
          @if (u.bio) {
            <p class="max-w-sm text-sm whitespace-pre-wrap">{{ u.bio }}</p>
          }
          @if (u.id !== me()?.id) {
            <button type="button" class="btn-primary mt-2" [disabled]="busy()" (click)="message(u.id)">
              <svg lucideMessagesSquare class="size-4"></svg> Send message
            </button>
          }
        </div>
      } @else if (user.error()) {
        <p class="py-8 text-sm text-muted">This user could not be loaded.</p>
      } @else {
        <div class="flex justify-center py-12 text-muted"><svg lucideLoaderCircle class="size-6 animate-spin"></svg></div>
      }
    </div>
  `,
})
export class UserProfileDialog {
  protected readonly ref = inject(DialogRef);
  private readonly data = inject<{ userId: string }>(DIALOG_DATA);
  private readonly usersApi = inject(UsersApi);
  private readonly chatsApi = inject(ChatsApi);
  private readonly chats = inject(ChatsStore);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  protected readonly presence = inject(PresenceStore);
  protected readonly me = inject(AuthService).user;
  protected readonly busy = signal(false);

  protected readonly user = resource({
    params: () => this.data.userId,
    loader: ({ params }) => firstValueFrom(this.usersApi.get(params)),
  });

  protected readonly status = computed(() => {
    const u = this.user.value();
    if (!u) return '';
    if (this.presence.isOnline(u.id)) return 'online';
    const seen = this.presence.lastSeenAt(u.id) ?? u.lastSeenAt;
    return seen ? `last seen ${timeAgo(seen)}` : 'offline';
  });

  constructor() {
    this.presence.watch([this.data.userId]);
  }

  protected async message(userId: string): Promise<void> {
    this.busy.set(true);
    try {
      const chat = await firstValueFrom(this.chatsApi.direct(userId));
      this.chats.setDetails(chat);
      this.ref.close();
      await this.router.navigate(['/chats', chat.id]);
    } catch (error) {
      this.toast.error(error);
    } finally {
      this.busy.set(false);
    }
  }
}
