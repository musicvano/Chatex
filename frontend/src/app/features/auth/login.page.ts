import { ChangeDetectionStrategy, Component, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { problemMessage } from '../../core/api/problem';
import { AuthService } from '../../core/auth/auth.service';
import { ICONS } from '../../shared/ui/icons';
import { AuthLayout } from './auth-layout';

@Component({
  selector: 'app-login-page',
  imports: [ReactiveFormsModule, RouterLink, AuthLayout, ICONS],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <app-auth-layout heading="Welcome back" subheading="Sign in to continue to Chatex">
      <form [formGroup]="form" (ngSubmit)="submit()" class="space-y-4" novalidate>
        @if (error()) {
          <p class="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950 dark:text-red-300" role="alert">
            {{ error() }}
          </p>
        }
        <div>
          <label class="label" for="login">User name or email</label>
          <input
            id="login"
            class="input"
            formControlName="login"
            autocomplete="username"
            autocapitalize="none"
            [class.input-invalid]="invalid('login')"
          />
          @if (invalid('login')) {
            <p class="field-error">Enter your user name or email.</p>
          }
        </div>
        <div>
          <label class="label" for="password">Password</label>
          <input
            id="password"
            class="input"
            type="password"
            formControlName="password"
            autocomplete="current-password"
            [class.input-invalid]="invalid('password')"
          />
          @if (invalid('password')) {
            <p class="field-error">Enter your password.</p>
          }
        </div>
        <button type="submit" class="btn-primary w-full" [disabled]="busy()">
          @if (busy()) {
            <svg lucideLoaderCircle class="size-4 animate-spin"></svg>
          }
          Sign in
        </button>
      </form>
      <p class="mt-6 text-center text-sm text-muted">
        New to Chatex?
        <a routerLink="/register" [queryParams]="returnUrl() ? { returnUrl: returnUrl() } : {}" class="font-medium text-brand-600 hover:underline dark:text-brand-400">Create an account</a>
      </p>
    </app-auth-layout>
  `,
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly returnUrl = input<string>();

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = inject(NonNullableFormBuilder).group({
    login: ['', Validators.required],
    password: ['', Validators.required],
  });

  protected invalid(name: 'login' | 'password'): boolean {
    const control = this.form.controls[name];
    return control.invalid && control.touched;
  }

  protected async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    try {
      const { login, password } = this.form.getRawValue();
      await this.auth.login({ login: login.trim(), password });
      await this.router.navigateByUrl(safeReturnUrl(this.returnUrl()));
    } catch (error) {
      this.error.set(problemMessage(error));
    } finally {
      this.busy.set(false);
    }
  }
}

/** Only follow in-app paths, never another origin. */
export function safeReturnUrl(url: string | undefined): string {
  return url && url.startsWith('/') && !url.startsWith('//') ? url : '/chats';
}
