# Getting started

[← Docs index](Index.md)

## Prerequisites

- [.NET SDK 10.0.403](https://dotnet.microsoft.com/download) or later in the 10.0 line
- Docker Desktop, for the local database (`compose.yaml`) and the tests; a local PostgreSQL 18 also works
- Node.js 24 or later with npm, for the frontend
- The EF Core CLI, pinned in `dotnet-tools.json` (run from the repository root):

```bash
dotnet tool restore
```

## Configure

Development settings are in `Source/Chatex.Api/appsettings.Development.json`:

| Setting                                                                     | Purpose                                                                                                    |
| --------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------- |
| `ConnectionStrings:Chatex`                                                  | PostgreSQL connection string                                                                               |
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:SigningKey`                              | token settings; the key must be at least 32 characters                                                     |
| `Jwt:AccessTokenLifetime`, `Jwt:RefreshTokenLifetime`                       | optional; defaults 15 minutes and 30 days                                                                  |
| `Storage:RootPath`                                                          | folder for uploaded files (default `.uploads` at the repository root, git-ignored)                         |
| `Attachments:MaxBytes`                                                      | optional upload limit (default 20 MB)                                                                      |
| `Attachments:AllowedContentTypes`                                           | optional list of media types that can be uploaded (images, PDF, text, zip and Office documents by default) |
| `Presence:OfflineGraceSeconds`                                              | optional delay before a disconnected user is reported offline (default 5)                                  |
| `RateLimiting:AuthPermitLimit`, `RateLimiting:AuthWindowSeconds`            | optional; requests per client IP to `/api/auth` per window (default 20 per 60 seconds)                     |
| `Maintenance:Enabled`, `Maintenance:Interval`, `Maintenance:StaleUploadAge` | optional; the hourly cleanup of unsent uploads (older than 24 hours) and old refresh tokens                |
| `DataProtection:KeysPath`                                                   | optional; folder that keeps the Data Protection keys across restarts (in memory when unset)                |

The dev signing key in that file is for local use only. In production, settings come from environment variables (for example `Jwt__SigningKey`).

## Create the database

Start PostgreSQL 18 in Docker. It listens on port 5432 with user and password `postgres` and keeps its data in the `chatex_postgres-data` volume:

```bash
docker compose up -d
```

`dotnet ef` reads the connection string from the `CHATEX_DB` environment variable and otherwise uses `Host=localhost;Database=chatex;Username=postgres;Password=postgres`.

```bash
dotnet ef database update --project Source/Chatex.DataAccess
```

## Run the API

```bash
dotnet run --project Source/Chatex.Api --launch-profile http
```

Then open http://localhost:5214/scalar for the interactive API reference (Development only; the raw document is at `/openapi/v1.json`). [`Chatex.Api.http`](../Source/Chatex.Api/Chatex.Api.http) has sample requests for Rider, Visual Studio or VS Code. `GET /health` answers `200` without a token while the database is reachable, and `503` when it is not.

## Run the frontend

Start the API first. The dev server proxies `/api` and `/hubs` to `http://localhost:5214` (`Source/Chatex.Web/proxy.conf.json`), so the refresh cookie stays same-origin.

```bash
cd Source/Chatex.Web
```

```bash
npm install
```

```bash
npm start
```

Then open http://localhost:4200. See [Frontend](Frontend.md) for linting, tests and the production build.
