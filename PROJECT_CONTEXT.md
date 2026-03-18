# MemStack Project Context

## What MemStack is

MemStack is a knowledge backend for storing and structuring business feature memory outside Nexwork.

It is not the workflow UI.
It is not the Git worktree manager.
It is not the customer-facing frontend.

Its job is to preserve structured business context over time.

## Why it exists

Teams need a durable way to remember:

- what customers requested
- what was implemented
- how business logic works now
- how money-related logic is handled
- how support should explain behavior later
- which projects and sprint items were involved

## Relationship with Nexwork

Nexwork owns workflow and execution:

- Git account reuse
- workspace and project selection
- feature creation
- worktree and branch operations
- extension UI

MemStack owns memory and structure:

- requirement capture persistence
- implementation summaries
- topic wiki documents
- metadata and retrieval-ready structure
- ask/search backend responses

## Current integration model

Nexwork now sends:

- requirement data from the Feature Memory step
- selected project relationships
- storage repository information
- active Git account context
- lifecycle sync events for:
  - `feature.created`
  - `feature.completed`
  - `project.status.updated`

MemStack uses that to create or update structured markdown and, when configured, write it into the selected GitHub or GitLab repository.

## Storage model

### Feature records

MemStack writes:

- `Features/<year>/<feature-slug>/requirement.md`
- `Features/<year>/<feature-slug>/implementation.md`

These keep per-feature history.

### Wiki records

MemStack writes:

- `Wiki/<topic>.md`

These accumulate domain knowledge across multiple features.

## Why markdown matters

Markdown is used because it is:

- readable by humans
- durable in Git
- easy for AI to chunk and cite
- better for long-term business memory than opaque database rows alone

## What future AI should understand

When AI reads this repo, it should understand:

- this is a structured business-memory system, not a generic note app
- feature history and topic knowledge are separate but related
- wiki pages represent current domain knowledge
- feature pages preserve change history
- low-token retrieval should rely on metadata and section-level search first

## Deployment state

MemStack is currently prepared for:

- Docker-based Render deployment
- SQLite persistence via `DATABASE_PATH`
- health endpoint at `/healthz`
- OpenAPI JSON at `/openapi/v1.json`
- Swagger UI at `/swagger`

## Long-term goal

MemStack should help answer questions like:

- what was requested?
- what changed?
- how does promotion logic work now?
- how is money-related logic handled?
- how should support explain this behavior?
- which feature or sprint changed this rule?
