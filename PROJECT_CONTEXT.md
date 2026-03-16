# MemStack Project Context

## What Is MemStack
MemStack is a knowledge backend for storing and structuring business feature memory.

## Why MemStack Exists
Teams need a durable way to remember:
- what customers requested
- what was implemented
- how business logic works now
- how money-related logic is handled
- how to reply to customer questions later

## Relationship With Nexwork
Nexwork is the workflow tool.
MemStack is the knowledge store.
Nexwork captures requirements, implementation context, and later asks MemStack for structured answers.

## Storage Model
MemStack should write:
- `Features/<year>/<feature-slug>/requirement.md`
- `Features/<year>/<feature-slug>/implementation.md`
- `Wiki/<topic>.md`

Feature records keep per-feature history.
Wiki records accumulate domain knowledge across multiple features.

## Why Markdown Is Important
Markdown is:
- readable by humans
- easy to version in Git
- easy for AI to chunk and retrieve
- easier to keep as long-term business memory than raw database rows only

## What Future AI Should Understand
When AI reads this project, it should know:
- this is not a generic note app
- this project preserves business logic memory
- requirement history and implementation history both matter
- wiki/topic documents represent current domain knowledge
- low-token retrieval should use metadata and section-level search first

## Long-Term Goal
MemStack should help answer questions like:
- what was requested?
- what changed?
- how does promotion logic work now?
- how is money-related logic handled?
- how should support explain this behavior to a customer?
