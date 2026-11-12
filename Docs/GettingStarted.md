# Getting started

[← Docs index](Index.md)

## Prerequisites

- [.NET SDK 10.0.403](https://dotnet.microsoft.com/download) or later in the 10.0 line
- PostgreSQL 18 (local install or Docker)
- Docker Desktop, for the tests
- Node.js 24 or later with npm, for the frontend
- The EF Core CLI:

```bash
dotnet tool install --global dotnet-ef
```

## Configure

Development settings are in `Chatex.Api/appsettings.Development.json`:

| Setting | Purpose |
|---|---|
| `ConnectionStrings:Chatex` | PostgreSQL connection string |
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:SigningKey` | token settings; the key must be at least 32 characters |
| `Jwt:AccessTokenLifetime`, `Jwt:RefreshTokenLifetime` | optional; defaults 15 minutes and 30 days |
| `Storage:RootPath` | folder for uploaded files (default `.uploads` at the repository root, git-ignored) |
| `Attachments:MaxBytes` | optional upload limit (default 20 MB) |
| `Attachments:AllowedContentTypes` | optional list of media types that can be uploaded (images, PDF, text, zip and Office documents by default) |
| `Presence:OfflineGraceSeconds` | optional delay before a disconnected user is reported offline (default 5) |

The dev signing key in that file is for local use only. In production, settings come from environment variables (for example `Jwt__SigningKey`).

## Create the database

`dotnet ef` reads the connection string from the `CHATEX_DB` environment variable and otherwise uses `Host=localhost;Database=chatex;Username=postgres;Password=postgres`.

```bash
dotnet ef database update --project Chatex.DataAccess
```

## Run the API

```bash
dotnet run --project Chatex.Api --launch-profile http
```

Then open http://localhost:5214/scalar for the interactive API reference (Development only; the raw document is at `/openapi/v1.json`). [`Chatex.Api.http`](../Chatex.Api/Chatex.Api.http) has sample requests for Rider, Visual Studio or VS Code. `GET /health` answers `200` without a token.

## Run the frontend

Start the API first. The dev server proxies `/api` and `/hubs` to `http://localhost:5214` (`Chatex.Web/proxy.conf.json`), so the refresh cookie stays same-origin.

```bash
cd Chatex.Web
```

```bash
npm install
```

```bash
npm start
```

Then open http://localhost:4200. See [Frontend](Frontend.md) for linting, tests and the production build.
