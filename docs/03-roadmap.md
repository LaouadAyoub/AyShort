# AyShort — Roadmap (vertical slices inside hexagonal)

## Slice 1 — Create Short URL (in-memory) ✅ COMPLETED
Inbound: POST /links
Core: CreateShortUrl (ports: IShortUrlRepository, ICodeGenerator, IClock)
Adapters: in-memory repo + Base62 generator
Tests: unit (use case), integration (endpoint)

## Slice 2 — Resolve Short URL (in-memory cache) ✅ COMPLETED
Inbound: GET /{code}
Core: ResolveShortUrl (ports: ICacheStore, IShortUrlRepository, IClock)
Adapters: in-memory cache
Tests: hit/miss, 404, 410

## Slice 3 — Get Stats ✅ COMPLETED
Inbound: GET /links/{code}/stats
Core: GetStats
Adapters: repo (in-memory)
Tests: happy / 404

## Slice 4 — Persistence (EF Core + migrations) ✅ COMPLETED
Swap in EF repository (SQLite/Postgres). Handle unique constraint → 409.
Integration test full flow with real DB.

## Slice 5 — Redis Cache ✅ COMPLETED
Implement Redis ICacheStore (TTL + negative cache). Warm on create.
Fallback to in-memory if Redis unavailable.
Negative caching: "__NOT_FOUND__" marker with 60s TTL.
Cache warming: immediate caching after link creation.
Tests: unit + integration with real Redis.
Metrics: (deferred to Ops slice).

## Slice 6 — Ops & Resilience
ProblemDetails, health/live & ready, latency histogram, Polly retry/CB.

## Slice 7 — Portfolio Polish
CLI or tiny UI, deploy once, screenshots, CHANGELOG.
