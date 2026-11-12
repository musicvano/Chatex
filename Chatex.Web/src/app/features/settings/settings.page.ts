import { CdkMenu, CdkMenuItem, CdkMenuTrigger } from '@angular/cdk/menu';
import { ChangeDetectionStrategy, Component, effect, inject, signal, untracked } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ProfileApi, avatarUrl } from '../../core/api/chatex-api';
import { fieldErrors, problemMessage, toProblem } from '../../core/api/problem';
import { AuthService } from '../../core/auth/auth.service';
import { BlobUrlService } from '../../core/media/blob-url.service';
import { ThemePreference, ThemeService } from '../../core/ui/theme.service';
import { ToastService } from '../../core/ui/toast.service';
import { Avatar } from '../../shared/ui/avatar';
import { Dialogs } from '../../shared/ui/dialogs';
import { ICONS } from '../../shared/ui/icons';
import { PASSWORD_PATTERN } from '../auth/register.page';

/** Profile, password, appearance and sessions. */
@Component({
  selector: 'app-settings-page',
  imports: [ReactiveFormsModule, Avatar, CdkMenu, CdkMenuItem, CdkMenuTrigger, ICONS],
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'flex min-h-0 flex-1 flex-col overflow-y-auto' },
  template: `
    <div class="mx-auto w-full max-w-2xl space-y-6 px-4 pt-safe pb-24 md:px-8 md:pb-10">
      <header class="pt-6">
        <h1 class="text-2xl font-bold tracking-tight">Settings</h1>
      </header>

      @if (auth.user(); as me) {
        <!-- Profile -->
        <section class="card p-5 sm:p-6" aria-labelledby="profile-heading">
          <h2 id="profile-heading" class="text-base font-semibold">Profile</h2>
          <div class="mt-4 flex flex-col items-center gap-4 sm:flex-row sm:items-start">
            <div class="relative">
              <app-avatar [id]="me.id" [name]="me.displayName" [hasAvatar]="me.hasAvatar" [size]="88" />
              <button type="button" class="absolute -right-1 -bottom-1 flex size-9 items-center justify-center rounded-full border-2 border-surface bg-brand-600 text-white shadow" aria-label="Change photo" [cdkMenuTriggerFor]="avatarMenu">
                <svg lucideCamera class="size-4"></svg>
              </button>
              <ng-template #avatarMenu>
                <div cdkMenu class="menu">
                  <button cdkMenuItem class="menu-item" (cdkMenuItemTriggered)="avatarInput.click()"><svg lucideCamera class="size-4"></svg>Upload photo</button>
                  @if (me.hasAvatar) {
                    <button cdkMenuItem class="menu-item text-red-600 dark:text-red-400" (cdkMenuItemTriggered)="removeAvatar()"><svg lucideTrash class="size-4"></svg>Remove photo</button>
                  }
                </div>
              </ng-template>
              <input #avatarInput type="file" accept="image/jpeg,image/png,image/webp" class="hidden" (change)="uploadAvatar(avatarInput)" />
            </div>
            <form [formGroup]="profile" (ngSubmit)="saveProfile()" class="w-full flex-1 space-y-4" novalidate>
              <div class="grid gap-4 sm:grid-cols-2">
                <div>
                  <span class="label">User name</span>
                  <p class="input bg-subtle text-muted">&#64;{{ me.userName }}</p>
                </div>
                <div>
                  <span class="label">Email</span>
                  <p class="input truncate bg-subtle text-muted">{{ me.email }}</p>
                </div>
              </div>
              <div>
                <label class="label" for="displayName">Display name</label>
                <input id="displayName" class="input" formControlName="displayName" maxlength="64" [class.input-invalid]="profile.controls.displayName.invalid && profile.controls.displayName.touched" />
              </div>
              <div>
                <label class="label" for="bio">Bio</label>
                <textarea id="bio" class="input min-h-24" formControlName="bio" maxlength="500" placeholder="A few words about you"></textarea>
                <p class="mt-1 text-right text-xs text-muted">{{ profile.controls.bio.value.length }} / 500</p>
              </div>
              <div class="flex justify-end">
                <button type="submit" class="btn-primary" [disabled]="savingProfile() || profile.pristine">Save profile</button>
              </div>
            </form>
          </div>
        </section>

        <!-- Appearance -->
        <section class="card p-5 sm:p-6" aria-labelledby="theme-heading">
          <h2 id="theme-heading" class="text-base font-semibold">Appearance</h2>
          <div class="mt-4 grid grid-cols-3 gap-2" role="radiogroup" aria-labelledby="theme-heading">
            @for (option of themes; track option.value) {
              <button
                type="button"
                role="radio"
                class="flex flex-col items-center gap-2 rounded-xl border px-3 py-4 text-sm transition-colors"
                [class]="theme.preference() === option.value ? 'border-brand-500 bg-brand-50 text-brand-700 dark:bg-brand-950 dark:text-brand-200' : 'border-line hover:bg-subtle'"
                [attr.aria-checked]="theme.preference() === option.value"
                (click)="theme.set(option.value)"
              >
                @switch (option.value) {
                  @case ('light') { <svg lucideSun class="size-5"></svg> }
                  @case ('dark') { <svg lucideMoon class="size-5"></svg> }
                  @case ('system') { <svg lucideMonitor class="size-5"></svg> }
                }
                {{ option.label }}
              </button>
            }
          </div>
        </section>

        <!-- Password -->
        <section class="card p-5 sm:p-6" aria-labelledby="password-heading">
          <h2 id="password-heading" class="text-base font-semibold">Change password</h2>
          <p class="mt-1 text-sm text-muted">You will be signed out on all devices, including this one.</p>
          <form [formGroup]="password" (ngSubmit)="changePassword()" class="mt-4 space-y-4" novalidate>
            <div>
              <label class="label" for="currentPassword">Current password</label>
              <input id="currentPassword" class="input" type="password" autocomplete="current-password" formControlName="currentPassword" />
              @if (passwordError('currentPassword'); as m) { <p class="field-error">{{ m }}</p> }
            </div>
            <div>
              <label class="label" for="newPassword">New password</label>
              <input id="newPassword" class="input" type="password" autocomplete="new-password" formControlName="newPassword" />
              @if (passwordError('newPassword'); as m) {
                <p class="field-error">{{ m }}</p>
              } @else {
                <p class="mt-1 text-xs text-muted">At least 8 characters, including a digit.</p>
              }
            </div>
            <div class="flex justify-end">
              <button type="submit" class="btn-primary" [disabled]="changingPassword()"><svg lucideKeyRound class="size-4"></svg>Change password</button>
            </div>
          </form>
        </section>

        <!-- Sessions -->
        <section class="card p-5 sm:p-6" aria-labelledby="sessions-heading">
          <h2 id="sessions-heading" class="text-base font-semibold">Sessions</h2>
          <div class="mt-4 flex flex-col gap-2 sm:flex-row">
            <button type="button" class="btn-secondary" (click)="auth.logout()"><svg lucideLogOut class="size-4"></svg>Sign out</button>
            <button type="button" class="btn-secondary text-red-600 dark:text-red-400" (click)="logoutAll()">Sign out everywhere</button>
          </div>
        </section>
      }
    </div>
  `,
})
export class SettingsPage {
  protected readonly auth = inject(AuthService);
  protected readonly theme = inject(ThemeService);
  private readonly api = inject(ProfileApi);
  private readonly blobs = inject(BlobUrlService);
  private readonly toast = inject(ToastService);
  private readonly dialogs = inject(Dialogs);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly themes: { value: ThemePreference; label: string }[] = [
    { value: 'light', label: 'Light' },
    { value: 'dark', label: 'Dark' },
    { value: 'system', label: 'System' },
  ];

  protected readonly savingProfile = signal(false);
  protected readonly changingPassword = signal(false);
  private readonly passwordServerErrors = signal<Record<string, string | undefined>>({});

  protected readonly profile = this.fb.group({
    displayName: ['', [Validators.required, Validators.maxLength(64)]],
    bio: ['', Validators.maxLength(500)],
  });

  protected readonly password = this.fb.group({
    currentPassword: ['', Validators.required],
    newPassword: ['', [Validators.required, Validators.minLength(8), Validators.maxLength(128), Validators.pattern(PASSWORD_PATTERN)]],
  });

  constructor() {
    effect(() => {
      const me = this.auth.user();
      untracked(() => {
        if (me && this.profile.pristine) {
          this.profile.reset({ displayName: me.displayName, bio: me.bio ?? '' });
        }
      });
    });
    this.password.valueChanges.subscribe(() => this.passwordServerErrors.set({}));
  }

  protected passwordError(name: 'currentPassword' | 'newPassword'): string | null {
    const server = this.passwordServerErrors()[name];
    if (server) return server;
    const control = this.password.controls[name];
    if (!control.touched || control.valid) return null;
    if (control.errors?.['required']) return 'This field is required.';
    if (control.errors?.['minlength']) return 'At least 8 characters.';
    if (control.errors?.['pattern']) return 'Include at least one digit.';
    return 'Invalid value.';
  }

  protected async saveProfile(): Promise<void> {
    if (this.profile.invalid) {
      this.profile.markAllAsTouched();
      return;
    }
    const value = this.profile.getRawValue();
    this.savingProfile.set(true);
    try {
      const me = await firstValueFrom(
        this.api.update({ displayName: value.displayName.trim(), bio: value.bio.trim() || null }),
      );
      this.auth.setUser(me);
      this.profile.reset({ displayName: me.displayName, bio: me.bio ?? '' });
      this.toast.success('Profile saved');
    } catch (error) {
      this.toast.error(error);
    } finally {
      this.savingProfile.set(false);
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
    try {
      const me = await firstValueFrom(this.api.uploadAvatar(file));
      this.blobs.invalidate(avatarUrl.user(me.id));
      this.auth.setUser(me);
      this.toast.success('Photo updated');
    } catch (error) {
      this.toast.error(error);
    }
  }

  protected async removeAvatar(): Promise<void> {
    try {
      const me = await firstValueFrom(this.api.deleteAvatar());
      this.blobs.invalidate(avatarUrl.user(me.id));
      this.auth.setUser(me);
    } catch (error) {
      this.toast.error(error);
    }
  }

  protected async changePassword(): Promise<void> {
    if (this.password.invalid) {
      this.password.markAllAsTouched();
      return;
    }
    const ok = await this.dialogs.confirm({
      title: 'Change password?',
      message: 'You will be signed out everywhere and need to sign in again with the new password.',
      confirmText: 'Change password',
    });
    if (!ok) return;
    this.changingPassword.set(true);
    try {
      await firstValueFrom(this.api.changePassword(this.password.getRawValue()));
      this.toast.success('Password changed. Sign in with your new password.');
      this.auth.endSession();
    } catch (error) {
      const current = fieldErrors(error, 'currentPassword')[0];
      const next = fieldErrors(error, 'newPassword')[0];
      if (current || next) {
        this.passwordServerErrors.set({ currentPassword: current, newPassword: next });
      } else if (toProblem(error).code === 'wrong_password') {
        this.passwordServerErrors.set({ currentPassword: problemMessage(error) });
      } else {
        this.toast.error(error);
      }
    } finally {
      this.changingPassword.set(false);
    }
  }

  protected async logoutAll(): Promise<void> {
    const ok = await this.dialogs.confirm({
      title: 'Sign out everywhere?',
      message: 'Every device signed in to your account, including this one, will be signed out.',
      confirmText: 'Sign out everywhere',
      danger: true,
    });
    if (ok) {
      await this.auth.logoutAll().catch((error) => this.toast.error(error));
    }
  }
}
