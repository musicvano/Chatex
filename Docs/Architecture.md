# Architecture

[← Docs index](Index.md)

## Backend layers

The backend has three layers. Each one only references the layer below it.

```
Chatex.Api          Presentation: HTTP endpoints, auth, problem details, SignalR hub
   │
Chatex.Business     Business logic: services, rules and permissions, validation, DTOs
   │
Chatex.DataAccess   Data access: EF Core entities, repositories, unit of work, migrations, file storage
```

- Services return `Result<T>` for expected failures (`Validation`, `NotFound`, `Forbidden`, `Conflict`, `Unauthorized`) and throw only on bugs. The Api turns failures into problem details; see [error codes](HttpApi.md#error-codes).
- The business layer never touches `DbContext`. It goes through repository interfaces and `IUnitOfWork`.
- Message ids are UUIDv7, so they sort by time and double as pagination cursors. Cursors given to clients are opaque strings.
- Services raise realtime events through `IChatNotifier` after each commit. The Api implements it with SignalR; without the Api (for example in the business tests) a no-op version is used.

## How a request flows

1. An endpoint in `Source/Chatex.Api/Endpoints` reads the user id from the token and calls one service method.
2. The service validates the command with FluentValidation.
3. It loads the chat and the caller's membership through `ChatAccess`, which applies the visibility rules (see [Chat rules](ChatRules.md)), and checks permissions with `ChatPermissions`.
4. It changes entities through repositories and saves them with `IUnitOfWork`, inside a transaction when several writes must succeed together. Unique-index violations come back as `UniqueConstraintException` and become `Conflict` errors.
5. After the commit it calls `IChatNotifier`, which the Api implements with SignalR (see [Realtime](Realtime.md)).
6. The endpoint maps the `Result` to `200`/`201`/`204` or to a problem details response.

## Service registration

Each layer has a `DependencyInjection` class with one extension method: `AddApi`, `AddBusiness` and `AddDataAccess`. `Program.cs` calls `AddApi()` before `AddBusiness()`. `AddApi` registers the SignalR `IChatNotifier`; `AddBusiness` registers the no-op `NullChatNotifier` only if no notifier exists yet. `AddBusiness` also registers the data access layer and validates the `Jwt`, `Attachments` and `Storage` settings on startup.

## Running in production

- **Health.** `/health` runs `SELECT 1` on a plain connection (`DatabaseHealthCheck` in DataAccess), so it answers 503 within seconds when the database is gone. The deploy uses it to decide on rollback.
- **Retries.** EF Core retries transient database failures. Explicit transactions go through `IUnitOfWork.ExecuteInTransactionAsync`, which replays the whole lambda on a retry, so everything a transaction saves must be tracked inside that lambda.
- **Maintenance.** `MaintenanceService` (Api, a hosted service) runs hourly. It deletes uploads that were never sent with a message and refresh tokens that have been expired or revoked for a full token lifetime.
- **Rate limiting.** `/api/auth` is throttled per client IP (`Infrastructure/RateLimiting.cs`). Behind Nginx the IP comes from `X-Forwarded-For`.
- **Data Protection** keys are written to `DataProtection:KeysPath` when it is set, so they survive restarts.

## Repository layout

```
Chatex.slnx                       solution
global.json                       SDK pin, Microsoft Testing Platform runner
Directory.Build.props             shared build settings (nullable, analyzers, warnings as errors, NuGet audit)
Directory.Packages.props          central NuGet versions
compose.yaml                      local PostgreSQL 18 for development
Source/
  Chatex.Api/                     web host: Endpoints, Hubs (SignalR, presence), Infrastructure (auth, errors, files, rate limiting), Maintenance, Contracts
  Chatex.Business/                Auth, Users, Chats, Messages, Attachments, Common
  Chatex.DataAccess/              Entities, Configurations, Repositories, Migrations, Storage
  Chatex.Web/                     Angular app (see Frontend.md)
Tests/
  Directory.Build.props           settings shared by the test projects, xunit.runner.json
  Chatex.Testing/                 shared PostgreSQL container fixture
  Chatex.DataAccess.Tests/        repository and storage tests
  Chatex.Business.Tests/          service tests through the full DI graph
  Chatex.Api.Tests/               HTTP tests against the real app (WebApplicationFactory)
Deploy/                           server files: systemd unit, Nginx site, settings template, activation script
Docs/                             this documentation
```
