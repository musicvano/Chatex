import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/auth/guards';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    title: 'Sign in · Chatex',
    loadComponent: () => import('./features/auth/login.page').then((m) => m.LoginPage),
  },
  {
    path: 'register',
    canActivate: [guestGuard],
    title: 'Create account · Chatex',
    loadComponent: () => import('./features/auth/register.page').then((m) => m.RegisterPage),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./features/shell/shell').then((m) => m.Shell),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'chats' },
      {
        path: 'chats',
        loadComponent: () => import('./features/shell/no-chat.page').then((m) => m.NoChatPage),
      },
      {
        path: 'chats/:chatId',
        loadComponent: () =>
          import('./features/conversation/conversation.page').then((m) => m.ConversationPage),
        children: [
          {
            path: 'info',
            loadComponent: () =>
              import('./features/chat-info/chat-info.panel').then((m) => m.ChatInfoPanel),
          },
        ],
      },
      {
        path: 'discover',
        loadComponent: () => import('./features/discover/discover.page').then((m) => m.DiscoverPage),
      },
      {
        path: 'settings',
        loadComponent: () => import('./features/settings/settings.page').then((m) => m.SettingsPage),
      },
    ],
  },
  { path: '**', redirectTo: 'chats' },
];
