# Chatex

A web messenger with direct chats, groups and channels, built on ASP.NET Core, SignalR and PostgreSQL, with an Angular and Tailwind CSS frontend that works on phones, tablets and desktops.

> **Status:** the backend (data access, business layer, HTTP API and SignalR hub) and the web frontend are done and tested. Playwright end-to-end tests and CI/CD come next. See [BACKLOG.md](BACKLOG.md) for the full tooling plan.

## Features

- **Accounts**: registration, login by user name or email, account lockout after 5 failed attempts, JWT access tokens with rotating refresh tokens (reusing a refresh token ends every session of that user).
- **Profiles**: display name, bio, avatar, last seen, user search.
- **Chats**, Telegram style:
  - **Direct**: one chat per pair of users.
  - **Group**: up to 1000 members, everyone can post.
  - **Channel**: only the owner and admins post, everyone else subscribes.
  - Public groups and channels have a unique `@handle`, can be searched and joined by anyone. Private chats are invisible to non-members.
  - Roles: owner, admin, member, subscriber. Ownership can be transferred.
- **Messages**: send, reply, edit, delete, read receipts, unread counts, history with cursor (keyset) pagination.
- **Attachments**: uploads with a size limit and a content-type allow-list, downloads for chat members only, cleanup of uploads that were never sent.
- **Realtime** over SignalR: new, edited and deleted messages, read receipts, membership and chat changes, typing indicators, online presence with last seen.

## Tech stack

| Area | Choice |
|---|---|
| Runtime | .NET 10 (C# 14) |
| Database | PostgreSQL 18, EF Core 10 with Npgsql, snake_case naming |
| Api | Minimal APIs, ProblemDetails (RFC 9457), OpenAPI with Scalar |
| Realtime | ASP.NET Core SignalR, in-memory presence (one process, no backplane) |
| Auth | ASP.NET Core Identity, JWT bearer (HMAC-SHA256), refresh token in an HttpOnly cookie |
| Validation | FluentValidation |
| Tests | xUnit v3, Shouldly, Testcontainers (PostgreSQL), Respawn, WebApplicationFactory |
| Frontend | Angular 22 (standalone, signals, zoneless), Tailwind CSS v4, Angular CDK, `@microsoft/signalr`, Lucide icons, Vitest |
| Planned | Playwright, GitHub Actions, Nginx + systemd on Ubuntu |

## Architecture

The backend has three layers. Each one only references the layer below it.

```
Chatex.Api          Presentation: HTTP endpoints, auth, problem details, SignalR hub
   │
Chatex.Business     Business logic: services, rules and permissions, validation, DTOs
   │
Chatex.DataAccess   Data access: EF Core entities, repositories, unit of work, migrations, file storage
```

- Services return `Result<T>` for expected failures (`Validation`, `NotFound`, `Forbidden`, `Conflict`, `Unauthorized`) and throw only on bugs.
- The business layer never touches `DbContext`. It goes through repository interfaces and `IUnitOfWork`.
- Message ids are UUIDv7, so they sort by time and double as pagination cursors. Cursors given to clients are opaque strings.
- Services raise realtime events through `IChatNotifier` after each commit. The Api implements it with SignalR; without the Api (for example in the business tests) a no-op version is used.

## Repository layout

```
backend/
  Chatex.slnx                     solution
  global.json                     SDK pin, Microsoft Testing Platform runner
  Directory.Build.props           shared build settings (nullable, warnings as errors)
  Directory.Packages.props        central NuGet versions
  src/
    Chatex.Api/                   web host: Endpoints, Hubs (SignalR, presence), Infrastructure (auth, errors, files), Contracts
    Chatex.Business/              Auth, Users, Chats, Messages, Attachments, Common
    Chatex.DataAccess/            Entities, Configurations, Repositories, Migrations, Storage
  tests/
    Chatex.Testing/               shared PostgreSQL container fixture
    Chatex.DataAccess.Tests/      repository and storage tests
    Chatex.Business.Tests/        service tests through the full DI graph
    Chatex.Api.Tests/             HTTP tests against the real app (WebApplicationFactory)
frontend/                         Angular app (see "Frontend" below)
BACKLOG.md                        tooling and deployment plan
LICENSE                           MIT license
```

## Getting started

### Prerequisites

- [.NET SDK 10.0.403](https://dotnet.microsoft.com/download) or later in the 10.0 line
- PostgreSQL 18 (local install or Docker)
- Docker Desktop, for the tests
- Node.js 24 or later with npm, for the frontend
- The EF Core CLI:

```bash
dotnet tool install --global dotnet-ef
```

### Configure

Development settings are in `backend/src/Chatex.Api/appsettings.Development.json`:

| Setting | Purpose |
|---|---|
| `ConnectionStrings:Chatex` | PostgreSQL connection string |
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:SigningKey` | token settings; the key must be at least 32 characters |
| `Storage:RootPath` | folder for uploaded files (default `backend/.uploads`, git-ignored) |
| `Attachments:MaxBytes` | optional upload limit (default 20 MB) |
| `Presence:OfflineGraceSeconds` | optional delay before a disconnected user is reported offline (default 5) |

The dev signing key in that file is for local use only. In production, settings come from environment variables (for example `Jwt__SigningKey`).

### Create the database

`dotnet ef` reads the connection string from the `CHATEX_DB` environment variable and otherwise uses `Host=localhost;Database=chatex;Username=postgres;Password=postgres`.

```bash
cd backend
```

```bash
dotnet ef database update --project src/Chatex.DataAccess
```

### Run the API

```bash
dotnet run --project backend/src/Chatex.Api --launch-profile http
```

Then open http://localhost:5214/scalar for the interactive API reference (Development only; the raw document is at `/openapi/v1.json`). [`Chatex.Api.http`](backend/src/Chatex.Api/Chatex.Api.http) has sample requests for Rider, Visual Studio or VS Code.

## HTTP API

All endpoints live under `/api` and need an access token (`Authorization: Bearer <token>`), except register, login, refresh and logout.

| Area | Routes |
|---|---|
| Auth | `POST /api/auth/register`, `/login`, `/refresh`, `/logout`, `/logout-all` |
| Profile | `GET`/`PUT /api/me`, `PUT /api/me/password`, `PUT`/`DELETE /api/me/avatar` |
| Users | `GET /api/users?query=`, `GET /api/users/{id}`, `GET /api/users/{id}/avatar` |
| Chats | `GET`/`POST /api/chats`, `POST /api/chats/direct`, `GET /api/chats/search?query=`, `GET`/`PUT`/`DELETE /api/chats/{id}`, avatar, `join`, `leave`, `members`, `members/{userId}/role`, `mute`, `read` |
| Messages | `GET`/`POST /api/chats/{id}/messages?before=&after=&limit=`, `PUT`/`DELETE /api/messages/{id}` |
| Attachments | `POST /api/attachments` (multipart, field `file`), `GET /api/attachments/{id}` |

- **Tokens.** Login, register and refresh return the access token in the body and set the refresh token as the `chatex_refresh` cookie (`HttpOnly`, `Secure`, `SameSite=Strict`, path `/api/auth`). Scripts never see it; the browser sends it only to `/api/auth/refresh` and `/api/auth/logout`.
- **Errors** are `application/problem+json`. The `code` field is a stable machine-readable code (for example `chat_not_found`, `not_a_member`); validation errors also have `errors` keyed by field. Status codes: validation 400, unauthorized 401, forbidden 403, not found 404, conflict 409.
- **Enums** are camelCase strings (`"group"`, `"admin"`). **Cursors** are opaque strings: pass `nextCursor` back as `cursor` or `before`.
- **Files.** Avatars and attachments need the access token too, so the frontend loads them with `HttpClient` as blobs instead of plain `<img src>` URLs.

## Realtime (SignalR)

The hub lives at `/hubs/chat` and needs the same access token as the API. Browsers cannot send headers on WebSocket and Server-Sent Events requests, so the token may also come as the `access_token` query parameter, which the server accepts only on `/hubs` paths. Payloads are JSON with camelCase names and camelCase enum strings, the same as the HTTP API.

On connect the server adds the connection to one group per chat of the user (`chat:{chatId}`), so chat events reach current members only. Members added later join the group on their open connections right away; removed members get `MemberRemoved` and then nothing more from that chat.

| Server to client | Arguments | When |
|---|---|---|
| `MessageCreated` | `message` | a message was sent |
| `MessageUpdated` | `message` | a message was edited |
| `MessageDeleted` | `chatId, messageId` | a message was deleted |
| `MessagesRead` | `chatId, userId, lastReadMessageId` | a member's read position moved forward (not for channels) |
| `MembersAdded` | `chatId, userIds[]` | users joined or were added, including everyone in a new chat |
| `MemberRemoved` | `chatId, userId` | a user left or was removed |
| `ChatUpdated` | `chatId` | title, description, handle, avatar or roles changed; refetch the chat |
| `ChatDeleted` | `chatId` | the owner deleted the chat |
| `Typing` | `chatId, userId` | another member is typing; expire the indicator after about 5 s |
| `UserOnline` | `userId` | a user who shares a chat with you opened their first connection |
| `UserOffline` | `userId, lastSeenAt` | that user's last connection closed and they did not return within the grace period |

| Client to server | Returns | Notes |
|---|---|---|
| `Typing(chatId)` | nothing | throttle to one call every 3 s; fails with a `HubException` if you are not a member |
| `GetOnlineUsers(userIds[])` | `userIds[]` | which of the given users are online now |

- **Presence** is kept in memory: a user is online while they have at least one connection (tabs and devices count separately). When the last one closes, the server waits `Presence:OfflineGraceSeconds` so a page reload does not flicker, then saves `lastSeenAt` and sends `UserOffline`.
- **Reconnects.** The handshake finishes before the server has joined the groups. After connecting or reconnecting, fetch what you missed with `GET /api/chats/{id}/messages?after=` rather than relying on events alone.

### Run the tests

Docker must be running. The tests start a disposable PostgreSQL container, apply the real migrations and wipe the data before each test. Run them from `backend/`, where `global.json` selects the Microsoft Testing Platform runner:

```bash
cd backend
```

```bash
dotnet test --solution Chatex.slnx
```

## Frontend

The web app in `frontend/` is an Angular 22 workspace styled with Tailwind CSS v4. It covers the whole API: sign-in and registration, the chat list, messaging with history paging, replies, edits, deletes and attachments, groups and channels with members and roles, public chat discovery, profile, password and theme settings.

- **Layouts.** Phones show one pane at a time with a bottom tab bar; tablets add a nav rail and keep the chat list beside the conversation; desktops also dock the chat info panel. Light, dark and system themes.
- **Session.** The access token lives only in memory. On startup the app calls `/api/auth/refresh` (the HttpOnly cookie restores the session), refreshes about a minute before expiry, and retries a request once after a 401. Refreshes are single-flight because the server revokes every session when an old refresh token is reused.
- **Realtime.** One SignalR connection per signed-in tab. Events update the chat list, unread counts, open conversation, typing and presence; after a reconnect the app refetches the chat list and fills the gap with `?after=`.
- **Files.** Avatars and attachments are fetched with `HttpClient` as blobs, because they need the bearer token.

```
frontend/src/app/
  core/      api (HttpClient wrappers, DTOs, problem details), auth (service, interceptor, guards),
             realtime (SignalR, presence, typing), media (blob URLs), ui (theme, toasts)
  state/     chats.store, messages.store (signal-based)
  shared/ui/ avatar, dialogs, user search, pipes, icons
  features/  auth, shell, chat-list, conversation, chat-info, new-chat, discover, settings, user-profile
```

### Run the frontend

Start the API first (see above). The dev server proxies `/api` and `/hubs` to `http://localhost:5214` (`frontend/proxy.conf.json`), so the refresh cookie stays same-origin.

```bash
cd frontend
```

```bash
npm install
```

```bash
npm start
```

Then open http://localhost:4200.

### Check the frontend

```bash
npm run lint
```

```bash
npm test -- --watch=false
```

```bash
npm run build
```

The production build lands in `frontend/dist/chatex/browser`; serve it with an SPA fallback to `index.html` and proxy `/api` and `/hubs` (with WebSocket upgrade) to the API.

## Working on the schema

After changing an entity or a configuration in `Chatex.DataAccess`, add a migration:

```bash
dotnet ef migrations add <Name> --project backend/src/Chatex.DataAccess --output-dir Migrations
```

To check that the model and the migrations agree:

```bash
dotnet ef migrations has-pending-model-changes --project backend/src/Chatex.DataAccess
```

## Roadmap

1. ~~Api: Minimal API endpoints, JWT authentication, ProblemDetails mapping for `Result` errors, OpenAPI with Scalar.~~ Done.
2. ~~SignalR hub: presence, typing indicators, an `IChatNotifier` implementation.~~ Done.
3. ~~Angular frontend: responsive layouts, every API feature, realtime updates.~~ Done.
4. Playwright end-to-end tests.
5. CI/CD with GitHub Actions, deployment to a DigitalOcean droplet.

## License

[MIT](LICENSE)
