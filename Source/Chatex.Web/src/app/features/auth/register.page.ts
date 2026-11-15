import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { fieldErrors, problemMessage, toProblem } from '../../core/api/problem';
import { AuthService } from '../../core/auth/auth.service';
import { ICONS } from '../../shared/ui/icons';
import { AuthLayout } from './auth-layout';
import { safeReturnUrl } from './login.page';

type Field = 'displayName' | 'userName' | 'email' | 'password';

/** Mirrors the server rules so most mistakes are caught before submitting. */
export const USER_NAME_PATTERN = /^[a-zA-Z][a-zA-Z0-9_]*$/;
export const PASSWORD_PATTERN = /\d/;

@Component({
  selector: 'app-register-page',
  imports: [ReactiveFormsModule, RouterLink, AuthLayout, ICONS],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-auth-layout heading="Create your account" subheading="It takes less than a minute">
      <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-4" novalidate>
        @if (error()) {
          <p
            class="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300"
            role="alert"
          >
            {{ error() }}
          </p>
        }
        <div>
          <label class="label" for="displayName">Display name</label>
          <input
            id="displayName"
            class="input"
            formControlName="displayName"
            autocomplete="name"
            maxlength="64"
            [class.input-invalid]="message('displayName')"
          />
          @if (message('displayName'); as m) {
            <p class="field-error">{{ m }}</p>
          }
        </div>
        <div>
          <label class="label" for="userName">User name</label>
          <div class="relative">
            <span class="pointer-events-none absolute top-1/2 left-3 -translate-y-1/2 text-muted"
              >&#64;</span
            >
            <input
              id="userName"
              class="input pl-7"
              formControlName="userName"
              autocomplete="username"
              autocapitalize="none"
              maxlength="32"
              [class.input-invalid]="message('userName')"
            />
          </div>
          @if (message('userName'); as m) {
            <p class="field-error">{{ m }}</p>
          } @else {
            <p class="mt-1 text-xs text-muted">
              3–32 characters: letters, digits and _. Starts with a letter.
            </p>
          }
        </div>
        <div>
          <label class="label" for="email">Email</label>
          <input
            id="email"
            class="input"
            type="email"
            formControlName="email"
            autocomplete="email"
            [class.input-invalid]="message('email')"
          />
          @if (message('email'); as m) {
            <p class="field-error">{{ m }}</p>
          }
        </div>
        <div>
          <label class="label" for="password">Password</label>
          <input
            id="password"
            class="input"
            type="password"
            formControlName="password"
            autocomplete="new-password"
            [class.input-invalid]="message('password')"
          />
          @if (message('password'); as m) {
            <p class="field-error">{{ m }}</p>
          } @else {
            <p class="mt-1 text-xs text-muted">At least 8 characters, including a digit.</p>
          }
        </div>
        <button type="submit" class="btn-primary w-full" [disabled]="busy()">
          @if (busy()) {
            <svg lucideLoaderCircle class="size-4 animate-spin"></svg>
          }
          Create account
        </button>
      </form>
      <p class="mt-6 text-center text-sm text-muted">
        Already have an account?
        <a
          routerLink="/login"
          [queryParams]="returnUrl() ? { returnUrl: returnUrl() } : {}"
          class="font-medium text-brand-600 hover:underline dark:text-brand-400"
          >Sign in</a
        >
      </p>
    </app-auth-layout>
  `,
})
export class RegisterPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly returnUrl = input<string>();

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  private readonly serverErrors = signal<Partial<Record<Field, string>>>({});

  protected readonly form = inject(NonNullableFormBuilder).group({
    displayName: ['', [Validators.required, Validators.maxLength(64)]],
    userName: [
      '',
      [
        Validators.required,
        Validators.minLength(3),
        Validators.maxLength(32),
        Validators.pattern(USER_NAME_PATTERN),
      ],
    ],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    password: [
      '',
      [
        Validators.required,
        Validators.minLength(8),
        Validators.maxLength(128),
        Validators.pattern(PASSWORD_PATTERN),
      ],
    ],
  });

  constructor() {
    // A server error for a field goes away as soon as the user edits that field.
    for (const name of Object.keys(this.form.controls) as Field[]) {
      this.form.controls[name].valueChanges.subscribe(() =>
        this.serverErrors.update((e) => ({ ...e, [name]: undefined })),
      );
    }
  }

  protected message(name: Field): string | null {
    const server = this.serverErrors()[name];
    if (server) {
      return server;
    }
    const control = this.form.controls[name];
    if (!control.touched || control.valid) {
      return null;
    }
    const e = control.errors ?? {};
    if (e['required']) return 'This field is required.';
    if (name === 'userName') {
      if (e['minlength']) return 'At least 3 characters.';
      if (e['pattern']) return 'Start with a letter; use only letters, digits and _.';
    }
    if (name === 'email') return 'Enter a valid email address.';
    if (name === 'password') {
      if (e['minlength']) return 'At least 8 characters.';
      if (e['pattern']) return 'Include at least one digit.';
    }
    if (e['maxlength']) return 'Too long.';
    return 'Invalid value.';
  }

  protected async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    try {
      const value = this.form.getRawValue();
      await this.auth.register({
        displayName: value.displayName.trim(),
        userName: value.userName.trim(),
        email: value.email.trim(),
        password: value.password,
      });
      await this.router.navigateByUrl(safeReturnUrl(this.returnUrl()));
    } catch (error) {
      const problem = toProblem(error);
      if (problem.code === 'user_name_taken') {
        this.serverErrors.set({ userName: problemMessage(error) });
      } else if (problem.code === 'email_taken') {
        this.serverErrors.set({ email: problemMessage(error) });
      } else if (Object.keys(problem.errors).length) {
        const fields: Partial<Record<Field, string>> = {};
        for (const name of ['displayName', 'userName', 'email', 'password'] as Field[]) {
          fields[name] = fieldErrors(error, name)[0];
        }
        this.serverErrors.set(fields);
      } else {
        this.error.set(problemMessage(error));
      }
    } finally {
      this.busy.set(false);
    }
  }
}
