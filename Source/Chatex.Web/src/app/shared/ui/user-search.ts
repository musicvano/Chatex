import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  input,
  model,
  output,
  signal,
} from '@angular/core';
import { Subject, debounceTime, distinctUntilChanged, switchMap, of, catchError, map } from 'rxjs';
import { UsersApi } from '../../core/api/chatex-api';
import { UserSummaryDto } from '../../core/api/models';
import { AuthService } from '../../core/auth/auth.service';
import { Avatar } from './avatar';
import { ICONS } from './icons';

/**
 * Searches users by name. In `multiple` mode it keeps a selection (shown as removable chips);
 * otherwise clicking a result emits `picked`.
 */
@Component({
  selector: 'app-user-search',
  imports: [Avatar, ICONS],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (multiple() && selected().length) {
      <ul class="mb-3 flex flex-wrap gap-2" aria-label="Selected users">
        @for (user of selected(); track user.id) {
          <li
            class="flex items-center gap-1.5 rounded-full bg-brand-50 py-1 pr-1 pl-1 text-sm text-brand-800 dark:bg-brand-950 dark:text-brand-200"
          >
            <app-avatar
              [id]="user.id"
              [name]="user.displayName"
              [hasAvatar]="user.hasAvatar"
              [size]="22"
            />
            <span class="max-w-32 truncate">{{ user.displayName }}</span>
            <button
              type="button"
              class="rounded-full p-0.5 hover:bg-brand-100 dark:hover:bg-brand-900"
              [attr.aria-label]="'Remove ' + user.displayName"
              (click)="toggle(user)"
            >
              <svg lucideX class="size-3.5"></svg>
            </button>
          </li>
        }
      </ul>
    }
    <label class="relative block">
      <span class="sr-only">Search people</span>
      <svg
        lucideSearch
        class="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted"
      ></svg>
      <input
        class="input pl-9"
        type="search"
        placeholder="Search by name or @username"
        autocomplete="off"
        [value]="query()"
        (input)="onQuery($any($event.target).value)"
      />
    </label>
    <ul
      class="mt-2 max-h-72 overflow-y-auto"
      role="listbox"
      [attr.aria-multiselectable]="multiple()"
    >
      @if (searching()) {
        <li class="flex justify-center py-4 text-muted">
          <svg lucideLoaderCircle class="size-5 animate-spin"></svg>
        </li>
      } @else if (query().trim().length >= 2 && !results().length) {
        <li class="py-4 text-center text-sm text-muted">No people found</li>
      }
      @for (user of results(); track user.id) {
        <li role="option" [attr.aria-selected]="isSelected(user.id)">
          <button
            type="button"
            class="flex w-full items-center gap-3 rounded-lg px-2 py-2 text-left hover:bg-subtle"
            (click)="multiple() ? toggle(user) : picked.emit(user)"
          >
            <app-avatar
              [id]="user.id"
              [name]="user.displayName"
              [hasAvatar]="user.hasAvatar"
              [size]="36"
            />
            <span class="min-w-0 flex-1">
              <span class="block truncate text-sm font-medium">{{ user.displayName }}</span>
              <span class="block truncate text-xs text-muted">&#64;{{ user.userName }}</span>
            </span>
            @if (multiple()) {
              <span
                class="flex size-5 items-center justify-center rounded-full border"
                [class]="
                  isSelected(user.id) ? 'border-brand-600 bg-brand-600 text-white' : 'border-line'
                "
              >
                @if (isSelected(user.id)) {
                  <svg lucideCheck class="size-3.5"></svg>
                }
              </span>
            }
          </button>
        </li>
      }
    </ul>
  `,
})
export class UserSearch {
  private readonly api = inject(UsersApi);
  private readonly me = inject(AuthService).user;
  private readonly queries = new Subject<string>();

  readonly multiple = input(false);
  /** Users to leave out of the results (e.g. existing members). */
  readonly exclude = input<readonly string[]>([]);
  readonly selected = model<UserSummaryDto[]>([]);
  readonly picked = output<UserSummaryDto>();

  protected readonly query = signal('');
  protected readonly searching = signal(false);
  private readonly found = signal<UserSummaryDto[]>([]);
  protected readonly results = computed(() => {
    const hidden = new Set([...this.exclude(), this.me()?.id ?? '']);
    return this.found().filter((u) => !hidden.has(u.id));
  });

  constructor() {
    const subscription = this.queries
      .pipe(
        map((q) => q.trim()),
        debounceTime(250),
        distinctUntilChanged(),
        switchMap((q) => {
          if (q.length < 2) {
            this.searching.set(false);
            return of([]);
          }
          this.searching.set(true);
          return this.api.search(q).pipe(catchError(() => of([])));
        }),
      )
      .subscribe((users) => {
        this.searching.set(false);
        this.found.set(users);
      });
    inject(DestroyRef).onDestroy(() => subscription.unsubscribe());
  }

  protected onQuery(value: string): void {
    this.query.set(value);
    this.queries.next(value);
  }

  protected isSelected(id: string): boolean {
    return this.selected().some((u) => u.id === id);
  }

  protected toggle(user: UserSummaryDto): void {
    this.selected.update((list) =>
      list.some((u) => u.id === user.id) ? list.filter((u) => u.id !== user.id) : [...list, user],
    );
  }
}
