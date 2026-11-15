import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthApi } from '../api/chatex-api';
import { AuthResponse, LoginRequest, MyProfileDto, RegisterRequest } from '../api/models';

/** Refresh this long before the access token expires. */
const REFRESH_AHEAD_MS = 60_000;
/** A token closer than this to expiry is refreshed before use. */
const MIN_TOKEN_LIFE_MS = 20_000;

/**
 * Holds the session. The access token lives only in memory; the refresh token is an HttpOnly cookie
 * the browser sends to /api/auth. Refreshing is single-flight: the server rotates the refresh token on
 * every use and treats reuse of an old one as theft (it revokes every session), so two parallel
 * refresh calls must never happen.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(AuthApi);
  private readonly router = inject(Router);

  private readonly token = signal<string | null>(null);
  private expiresAt = 0;
  private refreshInFlight: Promise<string | null> | null = null;
  private refreshTimer: ReturnType<typeof setTimeout> | undefined;

  readonly user = signal<MyProfileDto | null>(null);
  readonly isAuthenticated = computed(() => this.user() !== null && this.token() !== null);
  /** Increases on every sign-out so other services can drop per-user caches. */
  readonly sessionEpoch = signal(0);

  accessToken(): string | null {
    return this.token();
  }

  /** At startup: turn the refresh cookie (if any) into a session. Never throws. */
  async restoreSession(): Promise<void> {
    await this.refresh();
  }

  async login(body: LoginRequest): Promise<void> {
    this.apply(await firstValueFrom(this.api.login(body)));
  }

  async register(body: RegisterRequest): Promise<void> {
    this.apply(await firstValueFrom(this.api.register(body)));
  }

  /** Returns the new access token, or null (and signs out locally) when the session is gone. */
  refresh(): Promise<string | null> {
    this.refreshInFlight ??= this.doRefresh().finally(() => (this.refreshInFlight = null));
    return this.refreshInFlight;
  }

  /** A token that is good for at least a few more seconds, refreshing when needed. */
  async getValidToken(): Promise<string | null> {
    const token = this.token();
    if (token && this.expiresAt - Date.now() > MIN_TOKEN_LIFE_MS) {
      return token;
    }
    return this.refresh();
  }

  async logout(): Promise<void> {
    try {
      await firstValueFrom(this.api.logout());
    } catch {
      // Signing out locally is what matters.
    }
    this.endSession();
  }

  async logoutAll(): Promise<void> {
    await firstValueFrom(this.api.logoutAll());
    this.endSession();
  }

  /** Clears the session and goes to the sign-in page (session expired, password changed, ...). */
  endSession(returnUrl?: string): void {
    this.clear();
    void this.router.navigate(['/login'], {
      queryParams: returnUrl && returnUrl !== '/' ? { returnUrl } : undefined,
    });
  }

  setUser(user: MyProfileDto): void {
    this.user.set(user);
  }

  private async doRefresh(): Promise<string | null> {
    try {
      const response = await firstValueFrom(this.api.refresh());
      this.apply(response);
      return response.accessToken;
    } catch {
      this.clear();
      return null;
    }
  }

  private apply(response: AuthResponse): void {
    this.expiresAt = Date.parse(response.accessTokenExpiresAt);
    this.token.set(response.accessToken);
    this.user.set(response.user);
    this.scheduleRefresh();
  }

  private scheduleRefresh(): void {
    clearTimeout(this.refreshTimer);
    const delay = Math.max(5_000, this.expiresAt - Date.now() - REFRESH_AHEAD_MS);
    this.refreshTimer = setTimeout(() => void this.refresh(), delay);
  }

  private clear(): void {
    clearTimeout(this.refreshTimer);
    const hadSession = this.user() !== null;
    this.token.set(null);
    this.user.set(null);
    this.expiresAt = 0;
    if (hadSession) {
      this.sessionEpoch.update((n) => n + 1);
    }
  }
}
