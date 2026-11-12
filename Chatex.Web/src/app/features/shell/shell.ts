import { ChangeDetectionStrategy, Component, computed, effect, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';
import { AuthService } from '../../core/auth/auth.service';
import { RealtimeService } from '../../core/realtime/realtime.service';
import { ThemeService } from '../../core/ui/theme.service';
import { ChatsStore } from '../../state/chats.store';
import { Avatar } from '../../shared/ui/avatar';
import { ICONS } from '../../shared/ui/icons';
import { ChatListPane } from '../chat-list/chat-list-pane';

/**
 * The signed-in layout.
 * - Phone (< md): one pane at a time. /chats shows the list, a chat fills the screen, and a bottom
 *   tab bar switches between Chats, Discover and Settings (hidden inside a conversation).
 * - Tablet (md–xl): nav rail + chat list + main pane.
 * - Desktop (≥ xl): the same with a wider list; chat info docks on the right of the conversation.
 */
@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, ChatListPane, Avatar, ICONS],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="flex h-dvh overflow-hidden">
      <!-- Nav rail (tablet and desktop) -->
      <nav
        class="hidden w-18 shrink-0 flex-col items-center gap-2 border-r border-line bg-surface py-4 md:flex"
        aria-label="Main"
      >
        <a routerLink="/chats" class="mb-4 flex size-11 items-center justify-center rounded-xl bg-brand-600 text-white" aria-label="Chatex home">
          <svg lucideMessagesSquare class="size-6"></svg>
        </a>
        @for (item of nav; track item.path) {
          <a
            [routerLink]="item.path"
            routerLinkActive="!bg-brand-50 !text-brand-700 dark:!bg-brand-950 dark:!text-brand-300"
            class="relative flex size-11 items-center justify-center rounded-xl text-muted transition-colors hover:bg-subtle hover:text-fg"
            [attr.aria-label]="item.label"
            [title]="item.label"
          >
            @switch (item.icon) {
              @case ('chats') { <svg lucideMessagesSquare class="size-5"></svg> }
              @case ('discover') { <svg lucideCompass class="size-5"></svg> }
              @case ('settings') { <svg lucideSettings class="size-5"></svg> }
            }
            @if (item.icon === 'chats' && unread()) {
              <span class="badge absolute -top-1 -right-1 bg-brand-600 text-white">{{ unreadLabel() }}</span>
            }
          </a>
        }
        <div class="mt-auto flex flex-col items-center gap-2">
          <button type="button" class="icon-btn" (click)="toggleTheme()" [attr.aria-label]="theme.isDark() ? 'Switch to light theme' : 'Switch to dark theme'" [title]="theme.isDark() ? 'Light theme' : 'Dark theme'">
            @if (theme.isDark()) { <svg lucideSun class="size-5"></svg> } @else { <svg lucideMoon class="size-5"></svg> }
          </button>
          @if (auth.user(); as me) {
            <a routerLink="/settings" class="rounded-full" [title]="me.displayName" aria-label="Your profile">
              <app-avatar [id]="me.id" [name]="me.displayName" [hasAvatar]="me.hasAvatar" [size]="36" />
            </a>
          }
        </div>
      </nav>

      <!-- Chat list: always visible from md up; on phones only on /chats -->
      <app-chat-list-pane
        class="w-full shrink-0 flex-col border-r border-line bg-surface md:flex md:w-80 xl:w-96"
        [class.hidden]="!onChatsRoot()"
        [class.flex]="onChatsRoot()"
      />

      <!-- Main pane -->
      <main class="min-w-0 flex-1 flex-col md:flex" [class.hidden]="onChatsRoot()" [class.flex]="!onChatsRoot()">
        @if (realtime.state() === 'reconnecting' || (realtime.state() === 'disconnected' && auth.isAuthenticated())) {
          <div class="flex items-center justify-center gap-2 bg-amber-100 px-3 py-1.5 text-xs text-amber-900 dark:bg-amber-900/40 dark:text-amber-200" role="status">
            <svg lucideWifiOff class="size-3.5"></svg>
            Connecting… live updates are paused
          </div>
        }
        <div class="flex min-h-0 flex-1 flex-col">
          <router-outlet />
        </div>
      </main>
    </div>

    <!-- Bottom tab bar (phones), hidden inside a conversation -->
    @if (!inConversation()) {
      <nav
        class="fixed inset-x-0 bottom-0 z-30 flex border-t border-line bg-surface/95 pb-safe backdrop-blur md:hidden"
        aria-label="Main"
      >
        @for (item of nav; track item.path) {
          <a
            [routerLink]="item.path"
            routerLinkActive="!text-brand-600 dark:!text-brand-400"
            [routerLinkActiveOptions]="{ exact: item.path !== '/settings' && item.path !== '/discover' }"
            class="relative flex min-h-14 flex-1 flex-col items-center justify-center gap-0.5 text-[11px] text-muted"
          >
            @switch (item.icon) {
              @case ('chats') { <svg lucideMessagesSquare class="size-5"></svg> }
              @case ('discover') { <svg lucideCompass class="size-5"></svg> }
              @case ('settings') { <svg lucideSettings class="size-5"></svg> }
            }
            {{ item.label }}
            @if (item.icon === 'chats' && unread()) {
              <span class="badge absolute top-1.5 left-1/2 ml-1.5 bg-brand-600 text-white">{{ unreadLabel() }}</span>
            }
          </a>
        }
      </nav>
    }
  `,
})
export class Shell {
  protected readonly auth = inject(AuthService);
  protected readonly realtime = inject(RealtimeService);
  protected readonly theme = inject(ThemeService);
  private readonly chats = inject(ChatsStore);
  private readonly router = inject(Router);

  protected readonly nav = [
    { path: '/chats', label: 'Chats', icon: 'chats' },
    { path: '/discover', label: 'Discover', icon: 'discover' },
    { path: '/settings', label: 'Settings', icon: 'settings' },
  ] as const;

  private readonly url = toSignal(
    this.router.events.pipe(
      filter((e) => e instanceof NavigationEnd),
      map(() => this.router.url),
    ),
    { initialValue: this.router.url },
  );
  private readonly path = computed(() => this.url().split(/[?#]/)[0]);
  protected readonly onChatsRoot = computed(() => this.path() === '/chats');
  protected readonly inConversation = computed(() => /^\/chats\/[^/]+/.test(this.path()));

  protected readonly unread = this.chats.totalUnread;
  protected readonly unreadLabel = computed(() => (this.unread() > 99 ? '99+' : String(this.unread())));

  constructor() {
    void this.chats.loadFirst().catch(() => undefined);
    effect(() => {
      const count = this.unread();
      document.title = count ? `(${count}) Chatex` : 'Chatex';
    });
  }

  protected toggleTheme(): void {
    this.theme.set(this.theme.isDark() ? 'light' : 'dark');
  }
}
