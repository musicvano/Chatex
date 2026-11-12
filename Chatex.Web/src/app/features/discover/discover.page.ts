import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import {
  Subject,
  catchError,
  debounceTime,
  distinctUntilChanged,
  firstValueFrom,
  map,
  of,
  switchMap,
} from 'rxjs';
import { ChatsApi } from '../../core/api/chatex-api';
import { ChatDto, ChatSummaryDto } from '../../core/api/models';
import { ToastService } from '../../core/ui/toast.service';
import { ChatsStore } from '../../state/chats.store';
import { Avatar } from '../../shared/ui/avatar';
import { ICONS } from '../../shared/ui/icons';

/** Search public groups and channels, preview one and join it. */
@Component({
  selector: 'app-discover-page',
  imports: [Avatar, ICONS],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex min-h-0 flex-1 flex-col overflow-y-auto' },
  template: `
    <div class="pt-safe mx-auto w-full max-w-3xl px-4 pb-24 md:px-8 md:pb-10">
      <header class="py-6">
        <h1 class="text-2xl font-bold tracking-tight">Discover</h1>
        <p class="mt-1 text-sm text-muted">Find public groups and channels by name or handle.</p>
      </header>
      <label class="relative block">
        <span class="sr-only">Search public chats</span>
        <svg
          lucideSearch
          class="pointer-events-none absolute top-1/2 left-3 size-5 -translate-y-1/2 text-muted"
        ></svg>
        <input
          class="input py-3 pl-11"
          type="search"
          placeholder="Search public chats"
          (input)="search($any($event.target).value)"
        />
      </label>

      <div class="mt-6 grid gap-6 lg:grid-cols-[1fr_20rem]">
        <ul class="space-y-2">
          @if (searching()) {
            <li class="flex justify-center py-8 text-muted">
              <svg lucideLoaderCircle class="size-6 animate-spin"></svg>
            </li>
          } @else if (searched() && !results().length) {
            <li class="card px-4 py-10 text-center text-sm text-muted">
              Nothing found. Try another name.
            </li>
          } @else if (!searched()) {
            <li class="flex flex-col items-center gap-3 card px-4 py-12 text-center">
              <svg lucideCompass class="size-10 text-brand-500"></svg>
              <p class="text-sm text-muted">Type at least 2 characters to search.</p>
            </li>
          }
          @for (chat of results(); track chat.id) {
            <li>
              <button
                type="button"
                class="flex w-full items-center gap-3 card p-3 text-left transition-colors hover:border-brand-300 dark:hover:border-brand-700"
                [class.!border-brand-500]="preview()?.id === chat.id"
                (click)="select(chat)"
              >
                <app-avatar
                  kind="chat"
                  [id]="chat.id"
                  [name]="chat.title ?? ''"
                  [hasAvatar]="chat.hasAvatar"
                  [size]="48"
                />
                <span class="min-w-0 flex-1">
                  <span class="flex items-center gap-1.5 font-medium">
                    @if (chat.type === 'channel') {
                      <svg lucideMegaphone class="size-4 text-muted"></svg>
                    } @else {
                      <svg lucideUsers class="size-4 text-muted"></svg>
                    }
                    <span class="truncate">{{ chat.title }}</span>
                  </span>
                  <span class="block truncate text-sm text-muted"
                    >&#64;{{ chat.publicHandle }}
                    @if (chat.description) {
                      · {{ chat.description }}
                    }
                  </span>
                </span>
              </button>
            </li>
          }
        </ul>

        @if (preview(); as c) {
          <aside class="h-fit card p-5 text-center lg:sticky lg:top-6">
            <app-avatar
              class="mx-auto"
              kind="chat"
              [id]="c.id"
              [name]="c.title ?? ''"
              [hasAvatar]="c.hasAvatar"
              [size]="80"
            />
            <h2 class="mt-3 text-lg font-semibold">{{ c.title }}</h2>
            <p class="text-sm text-muted">
              &#64;{{ c.publicHandle }} · {{ c.memberCount }}
              {{ c.type === 'channel' ? 'subscribers' : 'members' }}
            </p>
            @if (c.description) {
              <p class="mt-3 text-sm whitespace-pre-wrap">{{ c.description }}</p>
            }
            <button
              type="button"
              class="mt-5 btn-primary w-full"
              [disabled]="busy()"
              (click)="joinOrOpen(c)"
            >
              @if (busy()) {
                <svg lucideLoaderCircle class="size-4 animate-spin"></svg>
              }
              {{ c.myRole ? 'Open' : 'Join ' + c.type }}
            </button>
          </aside>
        }
      </div>
    </div>
  `,
})
export class DiscoverPage {
  private readonly api = inject(ChatsApi);
  private readonly chats = inject(ChatsStore);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  private readonly queries = new Subject<string>();

  protected readonly results = signal<ChatSummaryDto[]>([]);
  protected readonly searching = signal(false);
  protected readonly searched = signal(false);
  protected readonly preview = signal<ChatDto | null>(null);
  protected readonly busy = signal(false);

  constructor() {
    const subscription = this.queries
      .pipe(
        map((q) => q.trim()),
        debounceTime(300),
        distinctUntilChanged(),
        switchMap((q) => {
          if (q.length < 2) {
            this.searched.set(false);
            this.searching.set(false);
            return of([]);
          }
          this.searching.set(true);
          return this.api.search(q).pipe(
            map((list) => {
              this.searched.set(true);
              return list;
            }),
            catchError((error) => {
              this.toast.error(error);
              return of([]);
            }),
          );
        }),
      )
      .subscribe((list) => {
        this.searching.set(false);
        this.results.set(list);
      });
    inject(DestroyRef).onDestroy(() => subscription.unsubscribe());
  }

  protected search(value: string): void {
    this.queries.next(value);
  }

  protected async select(chat: ChatSummaryDto): Promise<void> {
    try {
      const details = await firstValueFrom(this.api.get(chat.id));
      this.preview.set(details);
      // On narrow screens the preview sits below the list; bring it into view.
      if (window.innerWidth < 1024) {
        setTimeout(() =>
          document.querySelector('aside.card')?.scrollIntoView({ behavior: 'smooth' }),
        );
      }
    } catch (error) {
      this.toast.error(error);
    }
  }

  protected async joinOrOpen(chat: ChatDto): Promise<void> {
    if (chat.myRole) {
      this.chats.setDetails(chat);
      await this.router.navigate(['/chats', chat.id]);
      return;
    }
    this.busy.set(true);
    try {
      const joined = await firstValueFrom(this.api.join(chat.id));
      this.chats.setDetails(joined);
      this.toast.success(`Joined ${joined.title ?? 'chat'}`);
      await this.router.navigate(['/chats', joined.id]);
    } catch (error) {
      this.toast.error(error);
    } finally {
      this.busy.set(false);
    }
  }
}
