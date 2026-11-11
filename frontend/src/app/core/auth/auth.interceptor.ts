import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';

/** Auth routes that don't take a bearer token (the refresh cookie is enough, or nothing is). */
const ANONYMOUS = ['/api/auth/register', '/api/auth/login', '/api/auth/refresh', '/api/auth/logout'];

function withToken(req: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  return token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
}

/**
 * Adds the bearer token to /api calls and sends cookies to /api/auth. On a 401 it refreshes the
 * session once and retries; if that fails the user is signed out.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith('/api/')) {
    return next(req);
  }
  if (req.url.startsWith('/api/auth/')) {
    req = req.clone({ withCredentials: true });
  }
  if (ANONYMOUS.includes(req.url)) {
    return next(req);
  }

  const auth = inject(AuthService);
  const router = inject(Router);
  const sentToken = auth.accessToken();

  return next(withToken(req, sentToken)).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401) {
        return throwError(() => error);
      }
      // Another request may already have refreshed the session while this one was in flight.
      const current = auth.accessToken();
      const retryToken = current && current !== sentToken ? Promise.resolve(current) : auth.refresh();
      return from(retryToken).pipe(
        switchMap((token) => {
          if (!token) {
            auth.endSession(router.url);
            return throwError(() => error);
          }
          return next(withToken(req, token));
        }),
      );
    }),
  );
};
