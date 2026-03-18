# MemStack

MemStack is the knowledge backend for structured feature memory. It stores requirement history, implementation notes, and topic wiki documents so Nexwork and future tools can retrieve grounded business context later.

## What is implemented

- ASP.NET Core 10 Web API
- SQLite persistence through EF Core
- Feature memory CRUD, search, ask, and Nexwork sync endpoints
- Git-backed markdown generation for:
  - `Features/<year>/<feature-slug>/requirement.md`
  - `Features/<year>/<feature-slug>/implementation.md`
  - `Wiki/<topic>.md`
  - `PROJECT_CONTEXT.md`
- Remote file upsert support for GitHub and GitLab when Nexwork sends storage repo + Git account context

## Project structure

- `MemStack/Controller/FeatureMemoryController.cs` - HTTP endpoints
- `MemStack/Services/FeatureMemoryService.cs` - business logic and Nexwork sync mapping
- `MemStack/Data/GitRepository.cs` - markdown writing and remote Git upserts
- `MemStack/Data/MemStackDbContext.cs` - EF Core DB context
- `MemStack/Model/*` - entities and DTOs
- `PROJECT_CONTEXT.md` - high-level purpose and architecture intent

## Run locally

```bash
dotnet run --project "MemStack/MemStack.csproj"
```

Default local URL comes from `MemStack/Properties/launchSettings.json`.

## Try the API quickly

```bash
curl -s http://localhost:5294/api/feature-memories | jq
curl -s http://localhost:5294/healthz | jq
```

## Deploy to Render

This repo is prepared for Docker-based Render deployment.

### Runtime behavior

- Binds to Render's `PORT` automatically
- Accepts forwarded headers from the Render proxy
- Exposes:
  - `/` basic service status
  - `/healthz` health endpoint
  - `/openapi/v1.json` OpenAPI document

### Recommended environment variables

- `ASPNETCORE_ENVIRONMENT=Production`
- `DATABASE_PATH=/var/data/memstack.db`
- `GitPersistence__Enabled=false`

Optional:
- `ConnectionStrings__Default=Data Source=/var/data/memstack.db`

`DATABASE_PATH` is preferred because the app will create the parent folder automatically.

### Render service settings

- Runtime: `Docker`
- Root Directory: leave empty
- Branch: `main`

Render will build from the repo `Dockerfile`, so you do not need to enter custom build or start commands.

If you want persistent SQLite storage, mount a Render disk and point `DATABASE_PATH` to that mount path, for example `/var/data/memstack.db`.
