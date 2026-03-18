# MemStack

MemStack is the external knowledge backend for Feature Memory. It stores structured business context so Nexwork and future tools can retrieve feature intent, implementation notes, topic-level wiki knowledge, and grounded answers later.

## Current implementation

- ASP.NET Core 10 Web API
- SQLite persistence through EF Core
- CRUD, search, ask, and Nexwork sync endpoints
- markdown generation for:
  - `Features/<year>/<feature-slug>/requirement.md`
  - `Features/<year>/<feature-slug>/implementation.md`
  - `Wiki/<topic>.md`
  - `PROJECT_CONTEXT.md`
- remote GitHub and GitLab file upserts
- Docker-based Render deployment support
- Swagger UI support

## Relationship with Nexwork

Nexwork is the workflow client.
MemStack is the knowledge store.

Nexwork now sends:

- requirement context during feature creation
- selected project relationships
- lifecycle sync events
- storage repository details
- active Git account context for remote repo writes

MemStack receives that data, structures it, and writes durable markdown to the selected storage repository.

## Main endpoints

- `GET /api/feature-memories`
- `POST /api/feature-memories`
- `POST /api/feature-memories/search`
- `POST /api/feature-memories/ask`
- `POST /api/feature-memories/sync-from-nexwork`
- `GET /healthz`
- `GET /openapi/v1.json`
- `GET /swagger`

## Storage model

### Feature records

- `Features/<year>/<feature-slug>/requirement.md`
- `Features/<year>/<feature-slug>/implementation.md`

These preserve per-feature history.

### Wiki records

- `Wiki/<topic>.md`

These accumulate domain-level knowledge such as promotion, checkout, pricing, or tax.

## Important implementation areas

- `MemStack/Controller/FeatureMemoryController.cs`
  API surface
- `MemStack/Services/FeatureMemoryService.cs`
  business logic and Nexwork sync mapping
- `MemStack/Data/GitRepository.cs`
  markdown writing and remote Git upserts
- `MemStack/Data/MemStackDbContext.cs`
  database context
- `PROJECT_CONTEXT.md`
  high-level architecture intent

## Local development

```bash
dotnet run --project "MemStack/MemStack.csproj"
```

Useful checks:

```bash
dotnet build MemStack.sln
```

## Swagger

MemStack now exposes Swagger UI at:

```text
/swagger
```

OpenAPI JSON:

```text
/openapi/v1.json
```

## Deploy to Render

This repo is prepared for Docker-based Render deployment.

Recommended settings:

- Runtime: `Docker`
- Branch: `main`
- Root Directory: empty

Recommended environment variables:

- `ASPNETCORE_ENVIRONMENT=Production`
- `DATABASE_PATH=/var/data/memstack.db`
- `GitPersistence__Enabled=false`

If you want persistent SQLite storage, attach a Render disk and mount it at `/var/data`.
