import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter, withComponentInputBinding, withRouterConfig } from '@angular/router';
import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth.interceptor';
import { AuthService } from './core/auth/auth.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideHttpClient(withInterceptors([authInterceptor])),
    provideRouter(
      routes,
      withComponentInputBinding(),
      // chats/:chatId/info reads :chatId from its parent route.
      withRouterConfig({ paramsInheritanceStrategy: 'always' }),
    ),
    // Turn the refresh cookie into a session before the first navigation runs the guards.
    provideAppInitializer(() => inject(AuthService).restoreSession()),
  ],
};
