# Chatex

A web messenger with direct chats, groups and channels, built on ASP.NET Core, SignalR and PostgreSQL, with an Angular and Tailwind CSS frontend that works on phones, tablets and desktops.

> **Status:** the backend (data access, business layer, HTTP API and SignalR hub) and the web frontend are done and tested. Playwright end-to-end tests and CI/CD come next. See [BACKLOG.md](BACKLOG.md) for the full tooling plan.

## Features

- **Accounts**: registration, login by user name or email, account lockout after 5 failed attempts, JWT access tokens with rotating refresh tokens (reusing a refresh token ends every session of that user).
- **Profiles**: display name, bio, avatar, last seen, user search.
- **Chats**, Telegram style: direct chats (one per pair of users), groups (up to 1000 members) and channels (only the owner and admins post). Public groups and channels have a unique `@handle` and can be searched and joined; private ones are invisible to non-members. Roles: owner, admin, member, subscriber. See [Chat rules](Docs/ChatRules.md).
- **Messages**: send, reply, edit, delete, read receipts, unread counts, history with cursor (keyset) pagination.
- **Attachments**: uploads with a size limit and a content-type allow-list, downloads for chat members only.
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

## Quick start

You need the .NET 10 SDK, PostgreSQL 18, Node.js 24 and the `dotnet-ef` tool; see [Getting started](Docs/GettingStarted.md) for details and configuration.

```bash
dotnet ef database update --project Chatex.DataAccess
```

```bash
dotnet run --project Chatex.Api --launch-profile http
```

```bash
npm --prefix Chatex.Web install
```

```bash
npm --prefix Chatex.Web start
```

Open http://localhost:4200 for the app and http://localhost:5214/scalar for the API reference.

## Documentation

| Document | What it covers |
|---|---|
| [Getting started](Docs/GettingStarted.md) | prerequisites, configuration, database, running the API and the web app |
| [Architecture](Docs/Architecture.md) | backend layers, the `Result` pattern, request flow, repository layout |
| [Data model](Docs/DataModel.md) | entities, ids, paging, soft deletes, file storage, migrations |
| [Chat rules](Docs/ChatRules.md) | chat types, roles, permissions, visibility |
| [HTTP API](Docs/HttpApi.md) | routes, tokens, paging, limits, error codes |
| [Realtime](Docs/Realtime.md) | SignalR hub events, presence, reconnects |
| [Frontend](Docs/Frontend.md) | the Angular app |
| [Testing](Docs/Testing.md) | backend and frontend tests |

## Roadmap

1. ~~Api: Minimal API endpoints, JWT authentication, ProblemDetails mapping for `Result` errors, OpenAPI with Scalar.~~ Done.
2. ~~SignalR hub: presence, typing indicators, an `IChatNotifier` implementation.~~ Done.
3. ~~Angular frontend: responsive layouts, every API feature, realtime updates.~~ Done.
4. Playwright end-to-end tests.
5. CI/CD with GitHub Actions, deployment to a DigitalOcean droplet.

## License

[MIT](LICENSE)
