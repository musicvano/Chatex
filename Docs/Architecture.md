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

1. An endpoint in `Chatex.Api/Endpoints` reads the user id from the token and calls one service method.
2. The service validates the command with FluentValidation.
3. It loads the chat and the caller's membership through `ChatAccess`, which applies the visibility rules (see [Chat rules](ChatRules.md)), and checks permissions with `ChatPermissions`.
4. It changes entities through repositories and saves them with `IUnitOfWork`, inside a transaction when several writes must succeed together. Unique-index violations come back as `UniqueConstraintException` and become `Conflict` errors.
5. After the commit it calls `IChatNotifier`, which the Api implements with SignalR (see [Realtime](Realtime.md)).
6. The endpoint maps the `Result` to `200`/`201`/`204` or to a problem details response.

## Service registration

`Program.cs` calls `AddApi()` before `AddBusiness()`. `AddApi` registers the SignalR `IChatNotifier`; `AddBusiness` registers the no-op `NullChatNotifier` only if no notifier exists yet. `AddBusiness` also registers the data access layer and validates the `Jwt`, `Attachments` and `Storage` settings on startup.

## Repository layout

```
Chatex.slnx                       solution
global.json                       SDK pin, Microsoft Testing Platform runner
Directory.Build.props             shared build settings (nullable, warnings as errors)
Directory.Packages.props          central NuGet versions
Chatex.Api/                       web host: Endpoints, Hubs (SignalR, presence), Infrastructure (auth, errors, files), Contracts
Chatex.Business/                  Auth, Users, Chats, Messages, Attachments, Common
Chatex.DataAccess/                Entities, Configurations, Repositories, Migrations, Storage
Tests/
  Chatex.Testing/                 shared PostgreSQL container fixture
  Chatex.DataAccess.Tests/        repository and storage tests
  Chatex.Business.Tests/          service tests through the full DI graph
  Chatex.Api.Tests/               HTTP tests against the real app (WebApplicationFactory)
Chatex.Web/                       Angular app (see Frontend.md)
Docs/                             this documentation
```
