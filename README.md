# MemStack

Simple ASP.NET Web API for managing feature memory records.

## What is implemented

- Controller-based CRUD API at `/api/feature-memories`
- In-memory repository (no database yet)
- Request validation with DataAnnotations
- Status validation (`Planned`, `InProgress`, `Done`, `Blocked`)
- OpenAPI endpoint enabled in development (`/openapi/v1.json`)

## Project structure

- `MemStack/Controller/FeatureMemoryController.cs` - HTTP endpoints
- `MemStack/Services/FeatureMemoryService.cs` - business logic
- `MemStack/Data/InMemoryFeatureMemoryRepository.cs` - in-memory persistence
- `MemStack/Model/Request/*` - request/response DTOs
- `MemStack/MemStack.http` - ready-to-run API requests

## Run

```bash
dotnet run --project "MemStack/MemStack.csproj"
```

App URLs come from `MemStack/Properties/launchSettings.json`.

## Try the API quickly

Use `MemStack/MemStack.http` in your IDE, or call with curl:

```bash
curl -s http://localhost:5294/api/feature-memories | jq
```

