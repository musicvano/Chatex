import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ME, settle } from '../../../testing/fakes';
import { authInterceptor } from './auth.interceptor';
import { AuthService } from './auth.service';

const response = (token: string) => ({
  accessToken: token,
  accessTokenExpiresAt: new Date(Date.now() + 15 * 60_000).toISOString(),
  user: ME,
});

describe('authInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let auth: AuthService;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([{ path: '**', children: [] }]),
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);

    const login = auth.login({ login: 'alice', password: 'x' });
    backend.expectOne('/api/auth/login').flush(response('token-1'));
    await login;
  });

  afterEach(() => backend.verify());

  it('adds the bearer token to API calls', async () => {
    const result = firstValueFrom(http.get('/api/me'));
    const req = backend.expectOne('/api/me');
    expect(req.request.headers.get('Authorization')).toBe('Bearer token-1');
    req.flush(ME);
    await result;
  });

  it('leaves anonymous auth calls alone but sends cookies', async () => {
    const result = firstValueFrom(http.post('/api/auth/refresh', null));
    const req = backend.expectOne('/api/auth/refresh');
    expect(req.request.headers.has('Authorization')).toBe(false);
    expect(req.request.withCredentials).toBe(true);
    req.flush(response('token-2'));
    await result;
  });

  it('does not touch requests outside /api', async () => {
    const result = firstValueFrom(http.get('/assets/x.json'));
    const req = backend.expectOne('/assets/x.json');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush({});
    await result;
  });

  it('refreshes once on 401 and retries with the new token', async () => {
    const result = firstValueFrom(http.get('/api/chats'));
    backend.expectOne('/api/chats').flush(null, { status: 401, statusText: 'Unauthorized' });
    await settle();

    backend.expectOne('/api/auth/refresh').flush(response('token-2'));
    await settle();

    const retry = backend.expectOne('/api/chats');
    expect(retry.request.headers.get('Authorization')).toBe('Bearer token-2');
    retry.flush({ items: [] });
    expect(await result).toEqual({ items: [] });
  });

  it('signs out when the refresh fails', async () => {
    const result = firstValueFrom(http.get('/api/chats'));
    backend.expectOne('/api/chats').flush(null, { status: 401, statusText: 'Unauthorized' });
    await settle();
    backend.expectOne('/api/auth/refresh').flush(null, { status: 401, statusText: 'Unauthorized' });

    await expect(result).rejects.toBeInstanceOf(HttpErrorResponse);
    expect(auth.isAuthenticated()).toBe(false);
  });

  it('does not loop when the retried request is rejected again', async () => {
    const result = firstValueFrom(http.get('/api/chats'));
    backend.expectOne('/api/chats').flush(null, { status: 401, statusText: 'Unauthorized' });
    await settle();
    backend.expectOne('/api/auth/refresh').flush(response('token-2'));
    await settle();
    backend.expectOne('/api/chats').flush(null, { status: 401, statusText: 'Unauthorized' });

    await expect(result).rejects.toMatchObject({ status: 401 });
    backend.expectNone('/api/auth/refresh');
  });
});
