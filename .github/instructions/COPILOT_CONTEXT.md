# AyShort — Copilot Context

## Elevator Pitch
AyShort is a small URL shortener built to **demonstrate Hexagonal Architecture** (Ports & Adapters), **vertical slices**, and production instincts (Redis cache-aside, EF Core migrations, ProblemDetails, health, metrics). Portfolio-first; scope is tight; execution is clean.

## Business Goals (V1)
- Create short links (optional alias, optional expiration)
- Resolve short links fast
- Show basic stats (clicks, last access)

## Non-Goals (V1)
Auth, editing/deleting, custom domains, advanced analytics.

## Architecture (Hexagonal)
- **Core** (framework-free): Domain (ShortUrl, ShortCode, OriginalUrl), Use Cases (CreateShortUrl, ResolveShortUrl, GetStats), Ports (Inbound/Outbound).
- **Inbound Adapters**: Web API (ASP.NET Minimal API), optional CLI.
- **Outbound Adapters**: Persistence.Sql (EF Core), Cache.Redis (StackExchange.Redis), Codes (ICodeGenerator strategies).
- **External Systems**: SQLite/Postgres, Redis.

### Ports (Outbound)
- `IShortUrlRepository`
- `ICacheStore`
- `ICodeGenerator`
- `IClock`

### Endpoints
- `POST /links` → `{ url, alias?, expiration? }` → `201 { code, shortUrl }`
- `GET /{code}` → `302/307 Location: originalUrl` (or 404/410)
- `GET /links/{code}/stats` → `200 { createdAt, clicks, lastAccess, expiration }`

### Flows (short)
Create: validate → (alias unique OR generate code) → repo.Add → cache warm → return  
Resolve: cache GET → (hit → redirect) else repo.Find → re-cache → ++clicks → redirect

## Development Method (Vertical Slices)
Deliver features end-to-end, one slice at a time, always demoable:
1) Create Short URL (in-memory repo/cache)
2) Resolve Short URL (in-memory cache)
3) Get Stats (in-memory)
4) Swap repo → EF Core + Migrations
5) Swap cache → Redis (cache-aside + negative caching)
6) Ops polish: ProblemDetails, health/readiness, metrics, Polly

## Quality Gates (DoD for each slice)
- End-to-end works locally
- **Core has zero framework references**
- RFC 7807 **ProblemDetails** for errors
- Unit tests (use case) + 1 integration test (endpoint)
- README/docs updated (flow + example)
- Small, focused PRs (Conventional Commits)

## Design Rules (important)
- Keep adapters at the edges; **no EF/Redis/ASP.NET types in Core**
- Use **Strategy** for `ICodeGenerator`
- Use **cache-aside** with negative caching (later slice)
- Use **EF Core migrations** (not EnsureCreated) once persistence is added
- Redirect with **302/307** (not 301) in V1
- Health: `/health/live`, `/health/ready`
- Observability: cache hit/miss counters; resolve latency histogram (later slice)

## Folder Layout
```

/src
Core/                         # domain, use cases, ports
Adapters/
In/WebApi/                  # endpoints, DI, ProblemDetails, health
Out/Persistence.Sql/        # EF Core repo (M4)
Out/Cache.Redis/            # Redis cache (M5)
Out/Codes/                  # code generators (Strategy)
/tests
Unit/                         # use case tests
Integration/                  # endpoint tests
/docs                           # brief, architecture, roadmap, this file

```

## When Generating Code, Copilot Should…
- Respect **boundaries** (Core is pure).
- Prefer **small, testable** units.
- Return **ProblemDetails** for errors in the Web API.
- Add **interfaces in Core**, **implementations in Adapters**.
- Provide minimal **unit tests** for use cases; **one** integration test per endpoint change.
- Suggest **DI registrations** and **endpoints** but do not pull infra into Core.

## Things to Avoid
- Using EF/Redis/ASP.NET types in Core
- Adding features outside the active slice
- `EnsureCreated()` in production paths (use migrations)
- 301 redirects (use 302/307 in V1)
```

Commit it:

```
git add docs/COPILOT_CONTEXT.md
git commit -m "docs: add Copilot context guide"
```

---

# 2) Starter prompt for each Copilot Chat session

Paste this once at the start of a Copilot Chat session (it “sets the stage”):

```
You are assisting on the AyShort project.
Use docs/COPILOT_CONTEXT.md as the source of truth for goals, architecture, and rules.
We work in vertical slices with strict hexagonal boundaries.
Before writing code, confirm the slice scope, ports, adapters, endpoints, and tests.
Never add framework dependencies to Core.
Return ProblemDetails for HTTP errors.
Add small unit tests for use cases and one integration test per endpoint change.
```

(You can also keep this as a snippet in VS Code.)

---

# 3) Per-task prompt template (how you ask Copilot)

Use this whenever you open a new issue or micro-task:

```
Context: See docs/COPILOT_CONTEXT.md. We are working on Slice <N>: <name>.

Task:
- Implement <specific change>.
- Scope boundaries:
  - Core: <ports/use case/entities>
  - Inbound: <endpoint / mapping>
  - Outbound: <adapters>
- Constraints:
  - Core must remain framework-free.
  - Return RFC7807 ProblemDetails on errors.
  - Add unit tests for the use case (happy + one edge).
  - Add a basic integration test for the endpoint.

Deliver:
- Changed/new files with short explanations.
- DI wiring snippet (WebApi).
- Test snippets (unit + integration).
- No extra features beyond scope.
```

Example (Slice 1):

```
Context: docs/COPILOT_CONTEXT.md. Slice 1: Create Short URL (in-memory).
Task:
- Add ICreateShortUrl, IShortUrlRepository, ICodeGenerator, IClock in Core.
- Add CreateShortUrlService in Core.
- Implement Base62CodeGenerator in Adapters/Out/Codes.
- Implement InMemoryShortUrlRepository in Adapters/Out/Persistence.InMemory (or WebApi for now).
- Expose POST /links in WebApi and map errors to ProblemDetails.
- Unit test use case; integration test POST /links happy path.

Deliver: files, DI wiring, tests.
```

---

## Optional: Make Copilot “see” this automatically

* Keep `docs/COPILOT_CONTEXT.md` short (it is).
* When chatting, explicitly reference it (“Use docs/COPILOT_CONTEXT.md”).
* Add a short comment banner at the top of **Program.cs** and **Core** files:

```csharp
// AyShort: Hexagonal. Core is framework-free. See docs/COPILOT_CONTEXT.md.
// Current slice: <update me when you switch slices>
```

This keeps Copilot aligned even when you ask in-file.

---

## Bonus: PR checklist reminder for Copilot

Include this in your PR template (we already drafted one, add these lines):

```markdown
## Copilot alignment
- [ ] Changes follow docs/COPILOT_CONTEXT.md
- [ ] Core has no framework dependencies
- [ ] ProblemDetails used for HTTP errors
- [ ] Added unit test(s) for use case
- [ ] Added 1 integration test for the endpoint
```
