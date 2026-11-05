# Chatex

A web messenger with direct chats, groups and channels, built on ASP.NET Core and PostgreSQL. Realtime updates (SignalR) and the Angular frontend are planned next.

> **Status:** the backend data access and business layers are done and tested. HTTP endpoints, the SignalR hub and the frontend are not built yet. See [BACKLOG.md](BACKLOG.md) for the full tooling plan.

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

## Tech stack

| Area | Choice |
|---|---|
| Runtime | .NET 10 (C# 14) |
| Database | PostgreSQL 18, EF Core 10 with Npgsql, snake_case naming |
| Auth | ASP.NET Core Identity, JWT (HMAC-SHA256) |
| Validation | FluentValidation |
| Tests | xUnit v3, Shouldly, Testcontainers (PostgreSQL), Respawn |
| Planned | SignalR, Angular 22 + Tailwind CSS v4, Playwright, GitHub Actions, Nginx + systemd on Ubuntu |

## Architecture

The backend has three layers. Each one only references the layer below it.

```
Chatex.Api          Presentation: HTTP endpoints, SignalR hub (to come)
   │
Chatex.Business     Business logic: services, rules and permissions, validation, DTOs
   │
Chatex.DataAccess   Data access: EF Core entities, repositories, unit of work, migrations, file storage
```

- Services return `Result<T>` for expected failures (`Validation`, `NotFound`, `Forbidden`, `Conflict`, `Unauthorized`) and throw only on bugs.
- The business layer never touches `DbContext`. It goes through repository interfaces and `IUnitOfWork`.
- Message ids are UUIDv7, so they sort by time and double as pagination cursors. Cursors given to clients are opaque strings.
- Realtime events go through `IChatNotifier`. It does nothing until the Api provides a SignalR version.

## Repository layout

```
backend/
  Chatex.slnx                     solution
  global.json                     SDK pin, Microsoft Testing Platform runner
  Directory.Build.props           shared build settings (nullable, warnings as errors)
  Directory.Packages.props        central NuGet versions
  src/
    Chatex.Api/                   web host (currently only /health)
    Chatex.Business/              Auth, Users, Chats, Messages, Attachments, Common
    Chatex.DataAccess/            Entities, Configurations, Repositories, Migrations, Storage
  tests/
    Chatex.Testing/               shared PostgreSQL container fixture
    Chatex.DataAccess.Tests/      repository and storage tests
    Chatex.Business.Tests/        service tests through the full DI graph
BACKLOG.md                        tooling and deployment plan
LICENSE                           MIT license
```

## Getting started

### Prerequisites

- [.NET SDK 10.0.403](https://dotnet.microsoft.com/download) or later in the 10.0 line
- PostgreSQL 18 (local install or Docker)
- Docker Desktop, for the tests
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

Then open http://localhost:5214/health.

### Run the tests

Docker must be running. The tests start a disposable PostgreSQL container, apply the real migrations and wipe the data before each test.

```bash
dotnet test --solution backend/Chatex.slnx
```

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

1. Api: Minimal API endpoints, JWT authentication, ProblemDetails mapping for `Result` errors, OpenAPI with Scalar.
2. SignalR hub: presence, typing indicators, an `IChatNotifier` implementation.
3. Angular frontend.
4. Playwright end-to-end tests.
5. CI/CD with GitHub Actions, deployment to a DigitalOcean droplet.

## License

[MIT](LICENSE)
