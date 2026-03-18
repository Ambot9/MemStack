# AGENTS.md - MemStack

## Project Overview

MemStack is the external knowledge backend used by Nexwork Feature Memory.

It stores and structures business feature memory so future agents, developers, and support tools can understand:

- what was requested
- what was implemented
- how logic currently works
- how money-related rules are handled
- how to answer support-style questions later

## Current role in the system

- Nexwork owns workflow and execution
- MemStack owns knowledge storage and structure

Nexwork sends:
- requirement context
- selected project relationships
- storage repository information
- active Git account context
- lifecycle sync events

MemStack receives that data and writes structured markdown plus searchable metadata.

## Storage model

MemStack writes:

- `Features/<year>/<feature-slug>/requirement.md`
- `Features/<year>/<feature-slug>/implementation.md`
- `Wiki/<topic>.md`
- `PROJECT_CONTEXT.md`

Important distinction:
- feature files preserve per-feature history
- wiki files preserve topic-level accumulated knowledge

## Important implementation areas

- `MemStack/Program.cs`
  app startup, database migration, health routes, Swagger, Render-friendly hosting
- `MemStack/Controller/FeatureMemoryController.cs`
  API endpoints
- `MemStack/Services/FeatureMemoryService.cs`
  mapping, sync, ask/search behavior
- `MemStack/Data/GitRepository.cs`
  markdown generation and remote GitHub / GitLab upserts
- `MemStack/Data/MemStackDbContext.cs`
  EF Core persistence

## Main endpoints

- `GET /api/feature-memories`
- `POST /api/feature-memories`
- `POST /api/feature-memories/search`
- `POST /api/feature-memories/ask`
- `POST /api/feature-memories/sync-from-nexwork`
- `GET /healthz`
- `GET /swagger`
- `GET /openapi/v1.json`

## Development

```bash
dotnet build MemStack.sln
dotnet run --project "MemStack/MemStack.csproj"
```

## Deployment

This repo is currently prepared for Docker-based Render deployment.

Important environment variables:
- `ASPNETCORE_ENVIRONMENT=Production`
- `DATABASE_PATH=/var/data/memstack.db`
- `GitPersistence__Enabled=false`

## Guidance for future agents

- do not treat this as a generic notes app
- prefer structured markdown and section-aware updates
- keep Nexwork concerns out of MemStack UI logic
- preserve the boundary: Nexwork executes workflow, MemStack preserves knowledge
- when updating docs, keep `README.md`, `PROJECT_CONTEXT.md`, and this `AGENTS.md` aligned
