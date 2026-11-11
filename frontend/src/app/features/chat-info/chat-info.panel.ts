import { CdkMenu, CdkMenuItem, CdkMenuTrigger } from '@angular/cdk/menu';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  resource,
  signal,
  untracked,
} from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ChatsApi, UsersApi } from '../../core/api/chatex-api';
import { ChatMemberDto, ChatMemberRole, canManageChat, chatTitle } from '../../core/api/models';
import { problemMessage, toProblem } from '../../core/api/problem';
import { AuthService } from '../../core/auth/auth.service';
import { PresenceStore } from '../../core/realtime/presence.store';
import { ToastService } from '../../core/ui/toast.service';
import { ChatsStore } from '../../state/chats.store';
import { Avatar } from '../../shared/ui/avatar';
import { Dialogs } from '../../shared/ui/dialogs';
import { ICONS } from '../../shared/ui/icons';
import { HANDLE_PATTERN } from '../new-chat/new-chat-dialog';
import { UserProfileDialog } from '../user-profile/user-profile-dialog';
import { AddMembersDialog, AddMembersData } from './add-members-dialog';

const ROLE_LABEL: Record<ChatMemberRole, string> = {
  owner: 'Owner',
  admin: 'Admin',
  member: 'Member',
  subscriber: 'Subscriber',
};

/**
 * Chat details, settings and members (route chats/:chatId/info).
 * Full screen on phones, a slide-over drawer on tablets, docked beside the conversation on desktops.
 */
@Component({
  selector: 'app-chat-info-panel',
  imports: [ReactiveFormsModule, RouterLink, Avatar, CdkMenu, CdkMenuItem, CdkMenuTrigger, ICONS],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'fixed inset-0 z-40 flex justify-end xl:static xl:z-auto' },
  template: `
    <a [routerLink]="['/chats', chatId()]" class="absolute inset-0 hidden bg-black/30 md:block xl:hidden" aria-label="Close chat info" tabindex="-1"></a>
    <aside class="relative flex h-full w-full flex-col bg-surface md:w-96 md:border-l md:border-line md:shadow-2xl xl:w-80 xl:shadow-none 2xl:w-96" aria-label="Chat info">
      <header class="flex items-center gap-2 border-b border-line px-2 pt-safe">
        <a [routerLink]="['/chats', chatId()]" class="icon-btn" aria-label="Close chat info">
          <svg lucideArrowLeft class="size-5 md:hidden"></svg>
          <svg lucideX class="hidden size-5 md:block"></svg>
        </a>
        <h2 class="flex-1 py-4 font-semibold">{{ chat()?.type === 'direct' ? 'Profile' : 'Chat info' }}</h2>
        @if (canEdit() && !editing()) {
          <button type="button" class="icon-btn" aria-label="Edit chat" title="Edit" (click)="startEdit()"><svg lucidePencil class="size-5"></svg></button>
        }
      </header>

      @if (chat(); as c) {
        <div class="min-h-0 flex-1 overflow-y-auto pb-safe">
          <!-- Identity -->
          <section class="flex flex-col items-center gap-3 px-6 pt-6 pb-4 text-center">
            <div class="relative">
              <app-avatar
                [kind]="c.type === 'direct' ? 'user' : 'chat'"
                [id]="c.type === 'direct' ? c.peer?.id : c.id"
                [name]="title()"
                [hasAvatar]="c.type === 'direct' ? !!c.peer?.hasAvatar : c.hasAvatar"
                [online]="c.type === 'direct' && presence.isOnline(c.peer?.id)"
                [size]="96"
              />
              @if (canEdit()) {
                <button type="button" class="absolute -right-1 -bottom-1 flex size-9 items-center justify-center rounded-full border-2 border-surface bg-brand-600 text-white shadow" aria-label="Change chat photo" [cdkMenuTriggerFor]="avatarMenu">
                  <svg lucideCamera class="size-4"></svg>
                </button>
                <ng-template #avatarMenu>
                  <div cdkMenu class="menu">
                    <button cdkMenuItem class="menu-item" (cdkMenuItemTriggered)="avatarInput.click()"><svg lucideCamera class="size-4"></svg>Upload photo</button>
                    @if (c.hasAvatar) {
                      <button cdkMenuItem class="menu-item text-red-600 dark:text-red-400" (cdkMenuItemTriggered)="removeAvatar()"><svg lucideTrash class="size-4"></svg>Remove photo</button>
                    }
                  </div>
                </ng-template>
              }
              <input #avatarInput type="file" accept="image/jpeg,image/png,image/webp" class="hidden" (change)="uploadAvatar(avatarInput)" />
            </div>
            @if (!editing()) {
              <div>
                <h3 class="text-xl font-semibold break-words">{{ title() }}</h3>
                <p class="mt-0.5 flex items-center justify-center gap-1 text-sm text-muted">
                  @if (c.type === 'direct') {
                    &#64;{{ c.peer?.userName }}
                  } @else {
                    @if (c.publicHandle) {
                      <svg lucideGlobe class="size-3.5"></svg> &#64;{{ c.publicHandle }}
                    } @else {
                      <svg lucideLock class="size-3.5"></svg> Private {{ c.type }}
                    }
                    · {{ c.memberCount }} {{ c.type === 'channel' ? 'subscribers' : 'members' }}
                  }
                </p>
              </div>
            }
          </section>

          @if (editing()) {
            <form [formGroup]="form" (ngSubmit)="save()" class="space-y-4 px-5 pb-5" novalidate>
              <div>
                <label class="label" for="info-title">Name</label>
                <input id="info-title" class="input" formControlName="title" maxlength="128" />
              </div>
              <div>
                <label class="label" for="info-description">Description</label>
                <textarea id="info-description" class="input min-h-20" formControlName="description" maxlength="1000"></textarea>
              </div>
              <div>
                <label class="label" for="info-handle">Public handle</label>
                <div class="relative">
                  <span class="pointer-events-none absolute top-1/2 left-3 -translate-y-1/2 text-muted">&#64;</span>
                  <input id="info-handle" class="input pl-7" formControlName="publicHandle" autocapitalize="none" maxlength="32" />
                </div>
                <p class="mt-1 text-xs" [class]="handleInvalid() ? 'text-red-600 dark:text-red-400' : 'text-muted'">
                  {{ handleInvalid() ? '5–32 characters: letters, digits and _, starting with a letter.' : 'Leave empty to make the chat private.' }}
                </p>
              </div>
              @if (formError()) {
                <p class="field-error">{{ formError() }}</p>
              }
              <div class="flex gap-2">
                <button type="button" class="btn-secondary flex-1" (click)="editing.set(false)">Cancel</button>
                <button type="submit" class="btn-primary flex-1" [disabled]="busy()">Save</button>
              </div>
            </form>
          } @else {
            @if (c.type === 'direct') {
              @if (peer.value()?.bio; as bio) {
                <section class="border-t border-line px-5 py-4">
                  <h4 class="text-xs font-semibold tracking-wide text-muted uppercase">Bio</h4>
                  <p class="mt-1 text-sm whitespace-pre-wrap">{{ bio }}</p>
                </section>
              }
            } @else if (c.description) {
              <section class="border-t border-line px-5 py-4">
                <h4 class="text-xs font-semibold tracking-wide text-muted uppercase">Description</h4>
                <p class="mt-1 text-sm whitespace-pre-wrap">{{ c.description }}</p>
              </section>
            }
          }

          <!-- Settings -->
          @if (c.myRole) {
            <section class="border-t border-line px-2 py-2">
              <button type="button" class="flex w-full items-center gap-3 rounded-lg px-3 py-3 text-left hover:bg-subtle" role="switch" [attr.aria-checked]="!c.isMuted" (click)="toggleMute()">
                @if (c.isMuted) { <svg lucideBellOff class="size-5 text-muted"></svg> } @else { <svg lucideBell class="size-5 text-muted"></svg> }
                <span class="flex-1 text-sm">Notifications</span>
                <span class="relative h-6 w-10 rounded-full transition-colors" [class]="c.isMuted ? 'bg-zinc-300 dark:bg-zinc-600' : 'bg-brand-600'">
                  <span class="absolute top-0.5 size-5 rounded-full bg-white shadow transition-all" [class]="c.isMuted ? 'left-0.5' : 'left-[18px]'"></span>
                </span>
              </button>
            </section>
          }

          <!-- Members -->
          @if (c.type !== 'direct' && canSeeMembers()) {
            <section class="border-t border-line py-2">
              <div class="flex items-center px-5 py-2">
                <h4 class="flex-1 text-xs font-semibold tracking-wide text-muted uppercase">
                  {{ c.type === 'channel' ? 'Subscribers' : 'Members' }} ({{ c.memberCount }})
                </h4>
                @if (canEdit()) {
                  <button type="button" class="flex items-center gap-1 text-sm font-medium text-brand-600 hover:underline dark:text-brand-400" (click)="addMembers()">
                    <svg lucideUserPlus class="size-4"></svg> Add
                  </button>
                }
              </div>
              <ul>
                @for (m of members(); track m.user.id) {
                  <li class="flex items-center gap-3 px-5 py-2 hover:bg-subtle">
                    <button type="button" class="flex min-w-0 flex-1 items-center gap-3 text-left" (click)="openUser(m.user.id)">
                      <app-avatar [id]="m.user.id" [name]="m.user.displayName" [hasAvatar]="m.user.hasAvatar" [size]="40" [online]="presence.isOnline(m.user.id)" />
                      <span class="min-w-0 flex-1">
                        <span class="block truncate text-sm font-medium">{{ m.user.displayName }}@if (m.user.id === me()?.id) { <span class="text-muted"> (you)</span> }</span>
                        <span class="block truncate text-xs text-muted">{{ presence.isOnline(m.user.id) ? 'online' : '@' + m.user.userName }}</span>
                      </span>
                    </button>
                    @if (m.role === 'owner' || m.role === 'admin') {
                      <span class="flex items-center gap-1 rounded-full bg-brand-50 px-2 py-0.5 text-[11px] font-medium text-brand-700 dark:bg-brand-950 dark:text-brand-300">
                        @if (m.role === 'owner') { <svg lucideCrown class="size-3"></svg> } @else { <svg lucideShield class="size-3"></svg> }
                        {{ roleLabel[m.role] }}
                      </span>
                    }
                    @if (memberActions(m).length) {
                      <button type="button" class="icon-btn size-8" [attr.aria-label]="'Actions for ' + m.user.displayName" [cdkMenuTriggerFor]="memberMenu">
                        <svg lucideEllipsisVertical class="size-4"></svg>
                      </button>
                      <ng-template #memberMenu>
                        <div cdkMenu class="menu">
                          @for (action of memberActions(m); track action.label) {
                            <button cdkMenuItem class="menu-item" [class.text-red-600]="action.danger" [class.dark:text-red-400]="action.danger" (cdkMenuItemTriggered)="action.run()">
                              {{ action.label }}
                            </button>
                          }
                        </div>
                      </ng-template>
                    }
                  </li>
                }
              </ul>
              @if (membersCursor()) {
                <div class="px-5 py-2">
                  <button type="button" class="btn-ghost w-full" [disabled]="membersLoading()" (click)="loadMembers(false)">Show more</button>
                </div>
              } @else if (membersLoading()) {
                <div class="flex justify-center py-3 text-muted"><svg lucideLoaderCircle class="size-5 animate-spin"></svg></div>
              }
            </section>
          }

          <!-- Danger zone -->
          @if (c.myRole && c.type !== 'direct') {
            <section class="border-t border-line px-2 py-2">
              <button type="button" class="flex w-full items-center gap-3 rounded-lg px-3 py-3 text-left text-sm text-red-600 hover:bg-red-50 dark:text-red-400 dark:hover:bg-red-950/40" (click)="leave()">
                <svg lucideLogOut class="size-5"></svg> Leave {{ c.type }}
              </button>
              @if (c.myRole === 'owner') {
                <button type="button" class="flex w-full items-center gap-3 rounded-lg px-3 py-3 text-left text-sm text-red-600 hover:bg-red-50 dark:text-red-400 dark:hover:bg-red-950/40" (click)="deleteChat()">
                  <svg lucideTrash class="size-5"></svg> Delete {{ c.type }}
                </button>
              }
            </section>
          }
        </div>
      } @else {
        <div class="flex flex-1 items-center justify-center text-muted"><svg lucideLoaderCircle class="size-6 animate-spin"></svg></div>
      }
    </aside>
  `,
})
export class ChatInfoPanel {
  private readonly chats = inject(ChatsStore);
  private readonly api = inject(ChatsApi);
  private readonly usersApi = inject(UsersApi);
  private readonly dialogs = inject(Dialogs);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  protected readonly presence = inject(PresenceStore);
  protected readonly me = inject(AuthService).user;
  protected readonly roleLabel = ROLE_LABEL;

  readonly chatId = input.required<string>();

  protected readonly chat = computed(() => this.chats.details()[this.chatId()]);
  protected readonly title = computed(() => (this.chat() ? chatTitle(this.chat()!) : ''));
  protected readonly canEdit = computed(
    () => this.chat()?.type !== 'direct' && canManageChat(this.chat()?.myRole),
  );
  protected readonly canSeeMembers = computed(() => {
    const chat = this.chat();
    if (!chat?.myRole || chat.type === 'direct') return false;
    return chat.type !== 'channel' || canManageChat(chat.myRole);
  });

  protected readonly editing = signal(false);
  protected readonly busy = signal(false);
  protected readonly formError = signal<string | null>(null);
  protected readonly members = signal<ChatMemberDto[]>([]);
  protected readonly membersCursor = signal<string | null>(null);
  protected readonly membersLoading = signal(false);

  protected readonly form = inject(NonNullableFormBuilder).group({
    title: ['', [Validators.required, Validators.maxLength(128)]],
    description: ['', Validators.maxLength(1000)],
    publicHandle: ['', Validators.pattern(HANDLE_PATTERN)],
  });

  protected readonly peer = resource({
    params: () => (this.chat()?.type === 'direct' ? this.chat()?.peer?.id : undefined),
    loader: ({ params }) => firstValueFrom(this.usersApi.get(params)),
  });

  constructor() {
    // (Re)load members when the chat changes or its member count does (someone joined/left).
    effect(() => {
      this.chatId();
      void this.chat()?.memberCount;
      const canSee = this.canSeeMembers();
      untracked(() => {
        if (canSee) {
          void this.loadMembers(true);
        } else {
          this.members.set([]);
        }
      });
    });
    effect(() => {
      this.chatId();
      untracked(() => this.editing.set(false));
    });
  }

  protected handleInvalid(): boolean {
    return this.form.controls.publicHandle.invalid;
  }

  async loadMembers(reset: boolean): Promise<void> {
    const chatId = this.chatId();
    this.membersLoading.set(true);
    try {
      const page = await firstValueFrom(
        this.api.members(chatId, reset ? null : this.membersCursor()),
      );
      if (chatId !== this.chatId()) return;
      this.members.update((list) => (reset ? page.items : [...list, ...page.items]));
      this.membersCursor.set(page.nextCursor ?? null);
      this.presence.watch(page.items.map((m) => m.user.id));
    } catch {
      // The section just stays empty; a toast here would be noise.
    } finally {
      this.membersLoading.set(false);
    }
  }

  protected memberActions(m: ChatMemberDto): { label: string; danger?: boolean; run: () => void }[] {
    const chat = this.chat();
    const myRole = chat?.myRole;
    if (!chat || m.user.id === this.me()?.id || m.role === 'owner') {
      return [];
    }
    const actions: { label: string; danger?: boolean; run: () => void }[] = [];
    if (myRole === 'owner') {
      const baseRole: ChatMemberRole = chat.type === 'channel' ? 'subscriber' : 'member';
      if (m.role !== 'admin') {
        actions.push({ label: 'Make admin', run: () => void this.setRole(m, 'admin') });
      } else {
        actions.push({ label: `Make ${ROLE_LABEL[baseRole].toLowerCase()}`, run: () => void this.setRole(m, baseRole) });
      }
      actions.push({ label: 'Transfer ownership', run: () => void this.transfer(m) });
    }
    if (myRole === 'owner' || (myRole === 'admin' && (m.role === 'member' || m.role === 'subscriber'))) {
      actions.push({ label: `Remove from ${chat.type}`, danger: true, run: () => void this.removeMember(m) });
    }
    return actions;
  }

  protected openUser(userId: string): void {
    this.dialogs.open(UserProfileDialog, { userId });
  }

  protected startEdit(): void {
    const chat = this.chat();
    if (!chat) return;
    this.form.setValue({
      title: chat.title ?? '',
      description: chat.description ?? '',
      publicHandle: chat.publicHandle ?? '',
    });
    this.formError.set(null);
    this.editing.set(true);
  }

  protected async save(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.formError.set('Check the highlighted fields.');
      return;
    }
    const value = this.form.getRawValue();
    this.busy.set(true);
    try {
      const chat = await firstValueFrom(
        this.api.update(this.chatId(), {
          title: value.title.trim(),
          description: value.description.trim() || null,
          publicHandle: value.publicHandle.trim() || null,
        }),
      );
      this.chats.setDetails(chat);
      this.editing.set(false);
      this.toast.success('Chat updated');
    } catch (error) {
      this.formError.set(problemMessage(error));
    } finally {
      this.busy.set(false);
    }
  }

  protected async uploadAvatar(input: HTMLInputElement): Promise<void> {
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    if (file.size > 5 * 1024 * 1024) {
      this.toast.error('Photos can be at most 5 MB.');
      return;
    }
    await this.run(async () => {
      this.chats.setDetails(await firstValueFrom(this.api.uploadAvatar(this.chatId(), file)));
    });
  }

  protected async removeAvatar(): Promise<void> {
    await this.run(async () => {
      this.chats.setDetails(await firstValueFrom(this.api.deleteAvatar(this.chatId())));
    });
  }

  protected async toggleMute(): Promise<void> {
    const chat = this.chat();
    if (!chat) return;
    await this.run(async () => {
      await firstValueFrom(this.api.mute(chat.id, !chat.isMuted));
      this.chats.setMuted(chat.id, !chat.isMuted);
    });
  }

  protected async addMembers(): Promise<void> {
    const ref = this.dialogs.open<number, AddMembersData, AddMembersDialog>(AddMembersDialog, {
      chatId: this.chatId(),
      existing: this.members().map((m) => m.user.id),
    });
    const added = await firstValueFrom(ref.closed);
    if (added) {
      this.toast.success(`Added ${added} ${added === 1 ? 'person' : 'people'}`);
      await this.chats.loadChat(this.chatId()).catch(() => undefined);
      await this.loadMembers(true);
    }
  }

  private async setRole(m: ChatMemberDto, role: ChatMemberRole): Promise<void> {
    await this.run(async () => {
      await firstValueFrom(this.api.setRole(this.chatId(), m.user.id, role));
      await this.loadMembers(true);
    });
  }

  private async transfer(m: ChatMemberDto): Promise<void> {
    const ok = await this.dialogs.confirm({
      title: 'Transfer ownership?',
      message: `${m.user.displayName} becomes the owner. You stay on as an admin.`,
      confirmText: 'Transfer',
    });
    if (!ok) return;
    await this.run(async () => {
      await firstValueFrom(this.api.setRole(this.chatId(), m.user.id, 'owner'));
      await this.chats.loadChat(this.chatId());
      await this.loadMembers(true);
    });
  }

  private async removeMember(m: ChatMemberDto): Promise<void> {
    const ok = await this.dialogs.confirm({
      title: 'Remove member?',
      message: `${m.user.displayName} will be removed from this chat.`,
      confirmText: 'Remove',
      danger: true,
    });
    if (!ok) return;
    await this.run(async () => {
      await firstValueFrom(this.api.removeMember(this.chatId(), m.user.id));
      await this.chats.loadChat(this.chatId());
    });
  }

  protected async leave(): Promise<void> {
    const chat = this.chat();
    if (!chat) return;
    const ok = await this.dialogs.confirm({
      title: `Leave ${chat.type}?`,
      message: chat.publicHandle
        ? 'You can join again later.'
        : 'You will need to be added again to come back.',
      confirmText: 'Leave',
      danger: true,
    });
    if (!ok) return;
    try {
      await firstValueFrom(this.api.leave(chat.id));
      this.chats.remove(chat.id, false);
      await this.router.navigate(['/chats']);
    } catch (error) {
      this.toast.error(
        toProblem(error).code === 'owner_must_transfer'
          ? 'You own this chat. Transfer ownership to another member (Members → ⋮) before leaving.'
          : error,
      );
    }
  }

  protected async deleteChat(): Promise<void> {
    const chat = this.chat();
    if (!chat) return;
    const ok = await this.dialogs.confirm({
      title: `Delete ${chat.type}?`,
      message: 'The chat and its messages will be gone for everyone. This cannot be undone.',
      confirmText: 'Delete',
      danger: true,
    });
    if (!ok) return;
    try {
      await firstValueFrom(this.api.delete(chat.id));
      this.chats.remove(chat.id, false);
      await this.router.navigate(['/chats']);
    } catch (error) {
      this.toast.error(error);
    }
  }

  private async run(action: () => Promise<void>): Promise<void> {
    this.busy.set(true);
    try {
      await action();
    } catch (error) {
      this.toast.error(error);
    } finally {
      this.busy.set(false);
    }
  }
}
