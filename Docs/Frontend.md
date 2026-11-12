# Frontend

[← Docs index](Index.md)

The web app in `Chatex.Web/` is an Angular 22 workspace styled with Tailwind CSS v4. It covers the whole API: sign-in and registration, the chat list, messaging with history paging, replies, edits, deletes and attachments, groups and channels with members and roles, public chat discovery, profile, password and theme settings.

Components are standalone with inline templates, state uses signals, change detection is zoneless, and every route is lazy-loaded.

## Behavior

- **Layouts.** Phones show one pane at a time with a bottom tab bar; tablets add a nav rail and keep the chat list beside the conversation; desktops also dock the chat info panel. Light, dark and system themes.
- **Session.** The access token lives only in memory. On startup the app calls `/api/auth/refresh` (the HttpOnly cookie restores the session), refreshes about a minute before expiry, and retries a request once after a 401. Refreshes are single-flight because the server revokes every session when an old refresh token is reused.
- **Realtime.** One SignalR connection per signed-in tab. Events update the chat list, unread counts, open conversation, typing and presence; after a reconnect the app refetches the chat list and fills the gap with `?after=`. See [Realtime](Realtime.md).
- **Files.** Avatars and attachments are fetched with `HttpClient` as blobs, because they need the bearer token.

## Source layout

```
Chatex.Web/src/app/
  core/      api (HttpClient wrappers, DTOs, problem details), auth (service, interceptor, guards),
             realtime (SignalR, presence, typing), media (blob URLs), ui (theme, toasts)
  state/     chats.store, messages.store (signal-based)
  shared/ui/ avatar, dialogs, user search, pipes, icons
  features/  auth, shell, chat-list, conversation, chat-info, new-chat, discover, settings, user-profile
```

## Run

Start the API first (see [Getting started](GettingStarted.md)). The dev server proxies `/api` and `/hubs` to `http://localhost:5214` (`Chatex.Web/proxy.conf.json`), so the refresh cookie stays same-origin.

```bash
cd Chatex.Web
```

```bash
npm install
```

```bash
npm start
```

Then open http://localhost:4200.

## Check

```bash
npm run lint
```

```bash
npm test -- --watch=false
```

```bash
npm run build
```

`npm run format` runs Prettier (100-column lines, single quotes, Tailwind class sorting); `npm run format:check` only reports unformatted files, as CI does. Unit tests are described in [Testing](Testing.md#frontend).

## Production build

The production build lands in `Chatex.Web/dist/chatex/browser`; serve it with an SPA fallback to `index.html` and proxy `/api` and `/hubs` (with WebSocket upgrade) to the API. [Deployment](Deployment.md) has the Nginx site used in production.
