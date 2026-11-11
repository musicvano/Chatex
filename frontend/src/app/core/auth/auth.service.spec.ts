import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { ME, settle } from '../../../testing/fakes';
import { AuthResponse } from '../api/models';
import { AuthService } from './auth.service';

function authResponse(token: string): AuthResponse {
  return {
    accessToken: token,
    accessTokenExpiresAt: new Date(Date.now() + 15 * 60_000).toISOString(),
    user: ME,
  };
}

describe('AuthService', () => {
  let auth: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([{ path: '**', children: [] }])],
    });
    auth = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('restores the session from the refresh cookie', async () => {
    const restored = auth.restoreSession();
    http.expectOne({ method: 'POST', url: '/api/auth/refresh' }).flush(authResponse('t1'));
    await restored;

    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.user()?.userName).toBe('alice');
    expect(auth.accessToken()).toBe('t1');
  });

  it('stays signed out when there is no session', async () => {
    const restored = auth.restoreSession();
    http
      .expectOne('/api/auth/refresh')
      .flush({ code: 'invalid_refresh_token' }, { status: 401, statusText: 'Unauthorized' });
    await restored;

    expect(auth.isAuthenticated()).toBe(false);
  });

  it('sends only one refresh request for parallel callers', async () => {
    const first = auth.refresh();
    const second = auth.refresh();
    await settle();

    http.expectOne('/api/auth/refresh').flush(authResponse('t2'));

    expect(await first).toBe('t2');
    expect(await second).toBe('t2');
  });

  it('clears the session when a refresh fails', async () => {
    const login = auth.login({ login: 'alice', password: 'Passw0rd!' });
    http.expectOne('/api/auth/login').flush(authResponse('t1'));
    await login;
    const epoch = auth.sessionEpoch();

    const refreshed = auth.refresh();
    http.expectOne('/api/auth/refresh').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(await refreshed).toBeNull();
    expect(auth.user()).toBeNull();
    expect(auth.sessionEpoch()).toBe(epoch + 1);
  });

  it('reuses a fresh token instead of refreshing', async () => {
    const login = auth.login({ login: 'alice', password: 'Passw0rd!' });
    http.expectOne('/api/auth/login').flush(authResponse('t1'));
    await login;

    expect(await auth.getValidToken()).toBe('t1');
    http.expectNone('/api/auth/refresh');
  });
});
