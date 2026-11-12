# Testing

[← Docs index](Index.md)

## Backend

Docker must be running. The tests start a disposable PostgreSQL container, apply the real migrations and wipe the data before each test. Run them from the repository root, where `global.json` selects the Microsoft Testing Platform runner:

```bash
dotnet test --solution Chatex.slnx
```

One project, one class or matching methods:

```bash
dotnet test --project Tests/Chatex.Business.Tests
```

```bash
dotnet test --project Tests/Chatex.Business.Tests --filter-class "*ChatServiceTests"
```

```bash
dotnet test --project Tests/Chatex.Api.Tests --filter-method "*Refresh*"
```

### Test projects

| Project | What it tests | Base class |
|---|---|---|
| `Chatex.Testing` | shared `PostgresFixture` (one `postgres:18-alpine` container per test assembly, Respawn reset) and `DatabaseTest` | — |
| `Chatex.DataAccess.Tests` | repositories, keyset SQL, file storage, unit of work | `DbTestBase` |
| `Chatex.Business.Tests` | services through the full DI graph | `BusinessTestBase` |
| `Chatex.Api.Tests` | HTTP endpoints and the SignalR hub on the real app | `ApiTestBase` |

The tests use a real PostgreSQL, never an in-memory provider or SQLite, because the keyset queries must run on the real database. All tests in an assembly share one database, so parallel runs are turned off in each `xunit.runner.json`.

- **`BusinessTestBase`** uses a controllable clock (`Time`, a `FakeTimeProvider`), in-memory file storage (`Files`) and a `RecordingNotifier` (`Notifier`) that captures realtime events. Each service call runs in its own DI scope, like separate HTTP requests. `ShouldSucceed()` and `ShouldFail(kind, code)` assert results.
- **`ApiTestBase`** hosts the app with `WebApplicationFactory` at `https://localhost`, so the `Secure` refresh cookie round-trips. `RegisterAsync` returns a signed-in client; `ConnectHubAsync` opens a SignalR connection and returns once the server has joined the user's chat groups.

Tests use xUnit v3 and Shouldly.

## Frontend

Unit tests run with Vitest through the Angular CLI, from `Chatex.Web/`:

```bash
npm test -- --watch=false
```

One spec file:

```bash
npm test -- --watch=false --include src/app/state/chats.store.spec.ts
```

Specs sit next to the code (`*.spec.ts`); shared fakes are in `src/testing/fakes.ts`.

## Planned

Playwright end-to-end tests, with two browser contexts as two users to check presence, typing and read receipts.
