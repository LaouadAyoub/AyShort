# AyShort — Roadmap (vertical slices inside hexagonal)

## Slice 1 — Create Short URL (in-memory)
Inbound: POST /links
Core: CreateShortUrl (ports: IShortUrlRepository, ICodeGenerator, IClock)
Adapters: in-memory repo + Base62 generator
Tests: unit (use case), integration (endpoint)

## Slice 2 — Resolve Short URL (in-memory cache)
Inbound: GET /{code}
Core: ResolveShortUrl (ports: ICacheStore, IShortUrlRepository, IClock)
Adapters: in-memory cache
Tests: hit/miss, 404, 410

## Slice 3 — Get Stats
Inbound: GET /links/{code}/stats
Core: GetStats
Adapters: repo (in-memory)
Tests: happy / 404

## Slice 4 — Persistence (EF Core + migrations)
Swap in EF repository (SQLite/Postgres). Handle unique constraint → 409.
Integration test full flow with real DB.

## Slice 5 — Redis Cache
Implement Redis ICacheStore (TTL + negative cache). Warm on create.
Metrics: cache hit/miss.

## Slice 6 — Ops & Resilience
ProblemDetails, health/live & ready, latency histogram, Polly retry/CB.

## Slice 7 — Portfolio Polish
CLI or tiny UI, deploy once, screenshots, CHANGELOG.
