# Copilot Instructions for AyShort

Goal: Assist development of AyShort – a URL shortener built with Hexagonal Architecture and vertical slices. Optimize for preserving the clean boundaries (Core vs Adapters) and delivering one slice end‑to‑end at a time.

## Architecture Essentials
- Core (`src/Core`): Pure domain + application. Contains:
  - Domain: entities (`ShortUrl`), value objects (`ShortCode`, `OriginalUrl`), domain exceptions.
  - Application: use cases (services), ports (interfaces), DTOs, options (`ShortUrlOptions`).
  - NO framework (ASP.NET / EF / Redis) references allowed.
- Inbound Adapter: `WebApi` (minimal API). Only maps HTTP -> ports and converts domain exceptions -> ProblemDetails.
- Outbound Adapters (current):
  - `Persistence.InMemory` for `IShortUrlRepository` (temporary until SQL slice).
  - `Time` for `IClock` implementation (`SystemClock`).
  - `Base62` code generation currently lives in `Persistence.InMemory` (may move to a dedicated Codes adapter later).
- Planned future adapters: SQL (EF Core), Redis cache, metrics, resilience decorators.

## Vertical Slice Pattern
Each slice introduces only what it needs across all layers. Avoid adding unused ports, properties, or tables early.
1. Define the user story + acceptance criteria.
2. Add/extend ports in Core.
3. Implement use case service (pure logic, injected ports/options/clock).
4. Add or adjust adapters minimally.
5. Expose endpoint (if inbound feature) in WebApi.
6. Add unit tests (use case invariants) + integration test (endpoint contract).
7. Update docs (architecture sequence + examples).

## Current Slice (1: Create Short URL)
- Endpoint: POST `/links` → returns `{ code, shortUrl }` (201).
- Core ports used: `ICreateShortUrl`, `IShortUrlRepository`, `ICodeGenerator`, `IClock`.
- Business rules: valid absolute http/https URL; optional alias uniqueness; expiration within configured min/max; generated code retries up to 10 for uniqueness.
- Errors surfaced as ProblemDetails with domain exception name as title.

## Conventions
- Namespaces: `Core.*`, `Adapters.In.*`, `Adapters.Out.*`.
- Exceptions: domain-specific (Validation, Conflict, NotFound, Expired) thrown from Core; WebApi maps them.
- DI lifetime: lightweight services as singletons; in-memory repo is singleton.
- Code generation: random Base62, configurable length via `ShortUrlOptions.CodeLength`.
- DTOs are records; value objects perform all validation in factory (`Create`).
- Use case interface names start with `I` and verbs (`ICreateShortUrl`).

## Testing Guidance
- Unit tests: operate directly on use case with fakes (`FakeRepo`, `FakeClock`, `FakeGen`). Avoid hitting HTTP or real time.
- Integration tests: use `WebApplicationFactory<Program>`; assert status codes and JSON contracts.
- Add new tests alongside new behavior; do not retrofit unrelated broad refactors.

## When Adding New Features (Examples)
- Resolve slice will add: `IResolveShortUrl`, repository lookup method, maybe cache port later.
- Stats slice: add `IGetStats`, extend repo (avoid leaking persistence models into Core).
- Persistence slice: introduce EF Core project; repository implementation translates DB exceptions to domain exceptions.

## Guardrails for Copilot
- Do NOT reference ASP.NET, EF, or Redis types inside `Core`.
- Do NOT pre-create unused adapters or ports “for later”.
- Keep ProblemDetails mapping centralized in `Program.cs` (don’t scatter try/catch blocks).
- Favor value object validation over scattered guard clauses in use cases.
- Use dependency injection instead of static helpers (except pure utility constants).

## Suggested Commands
- Build: `dotnet build`
- Test all: `dotnet test`
- Run API: `dotnet run --project src/Adapters/In/WebApi/WebApi.csproj`
- Spin infra (future): `docker compose up -d`

## Documentation Updates
When a slice changes runtime behavior (new endpoint, new error path, new sequence), update:
- `docs/02-architecture.md` (sequence diagrams / API contract changes).
- `README.md` (quick examples for new endpoints).

## What Not To Do
- Don’t inline adapter implementations back into `Program.cs`.
- Don’t couple tests to internal randomness—inject deterministic fakes.
- Don’t add persistence concerns (migrations, DbContext) before the persistence slice.

Stay minimal, slice by slice. Ask for clarification if a requirement seems outside the currently active slice.
