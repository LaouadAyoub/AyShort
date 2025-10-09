# AyShort — Architecture

## 1) Guiding Principle
**Hexagonal Architecture (Ports & Adapters)**  
- Keep **Core** (Domain + Use Cases) **framework-free**.  
- Put all technology at the **edges** (HTTP, DB, Redis, code generation).  
- Communicate through **ports (interfaces)**; edges implement **adapters**.

---

## 2) High-Level View

```

Clients
│
▼
Inbound Adapters (Web API / CLI)
│        calls inbound ports (use cases)
▼
Core (Domain + Use Cases + Ports)
│        depends on outbound ports (interfaces)
▼
Outbound Adapters (Persistence.Sql, Cache.Redis, Codes)
│
▼
External Systems (Postgres/SQLite, Redis)

````

---

## 3) Core Components

### 3.1 Domain (entities & value objects)
- **ShortUrl**
  - `Code` (string)
  - `OriginalUrl` (string)
  - `CreatedAt` (Utc)
  - `Expiration` (nullable Utc)
  - `ClicksCount` (int)
  - `LastAccessAt` (nullable Utc)
- **Value Objects**
  - **ShortCode**: allowed charset/length (e.g., Base62 + `-`/`_` if allowed).
  - **OriginalUrl**: absolute, `http|https`, max length (e.g., 2048).

**Invariants (examples):**
- URL must be absolute and `http` or `https`.
- If `Expiration` is set, it must be in the future.
- `ShortCode` must match allowed pattern/length.

### 3.2 Use Cases (application services)
- **CreateShortUrl**: validate → (alias? check unique : generate code) → persist → warm cache → return short URL.
- **ResolveShortUrl**: cache-first → DB on miss → (not found/expired?) → increment click + last access → re-cache → return original URL.
- **GetStats**: read authoritative stats from repository.

### 3.3 Ports (interfaces)
- **Inbound Ports** (Core offers):
  - `ICreateShortUrl`
  - `IResolveShortUrl`
  - `IGetStats`
- **Outbound Ports** (Core needs):
  - `IShortUrlRepository` (persist/find by code, unique checks)
  - `ICacheStore` (get/set by code, TTL, negative caching)
  - `ICodeGenerator` (Strategy: base62 counter/random/hash)
  - `IClock` (UTC now; testable time source)
  - *(Optional)* `IMetrics` / `IClickRecorder`

---

## 4) Adapters (edges)

### 4.1 Inbound Adapters
- **Web API (ASP.NET Core Minimal API)**  
  - Maps HTTP requests/responses to inbound ports (use cases).
  - Returns redirects (302/307) or **ProblemDetails** errors.

### 4.2 Outbound Adapters
- **Persistence.Sql (EF Core + SQLite/Postgres)**  
  - Implements `IShortUrlRepository`.  
  - Enforces unique index on `Code`.  
  - Translates DB uniqueness violations to a domain conflict.
- **Cache.Redis (StackExchange.Redis)** ✅ Slice 5  
  - Implements `ICacheStore` with **cache-aside** and **negative caching**.  
  - Key: `{code}` → value: `{originalUrl}` or `"__NOT_FOUND__"` (negative marker).
  - Positive cache TTL: 24 hours (configurable via `Redis:DefaultTtlSeconds`).
  - Negative cache TTL: 60 seconds (configurable via `Redis:NegativeTtlSeconds`).
  - Graceful degradation: cache failures don't break requests (logs error, returns null).
  - Fallback: if Redis not configured, uses in-memory `InMemoryCacheStore`.
- **Codes**  
  - Implements `ICodeGenerator` (e.g., Random Base62).

---

## 5) Data Model (authoritative store)

**Table: `Links`**

| Column         | Type        | Notes                          |
|----------------|-------------|--------------------------------|
| Code (PK)      | text/varchar| Unique short code              |
| OriginalUrl    | text        | Validated URL                  |
| CreatedAtUtc   | datetime    | UTC                            |
| ExpirationUtc  | datetime?   | Nullable                       |
| ClicksCount    | int         | Aggregate count                |
| LastAccessAtUtc| datetime?   | Nullable                       |

Indexes:
- PK/Unique on `Code`.
- (Optional) index on `ExpirationUtc` for cleanup.

---

## 6) API Contracts (HTTP)

### 6.1 Create
- **POST** `/links`  
**Request (JSON):**
```json
{
  "url": "https://example.com/very/long/path",
  "alias": "my-campaign",      
  "expiration": "2026-01-01T00:00:00Z"
}
````

**Response 201 (JSON):**

```json
{
  "code": "my-campaign",
  "shortUrl": "https://sho.rt/my-campaign"
}
```

**Errors:**

* 400 ProblemDetails (invalid URL/alias/expiration)
* 409 ProblemDetails (alias taken)

### 6.2 Resolve

* **GET** `/{code}`
  **Response:**
* 302/307 → `Location: https://example.com/...`
  **Errors:**
* 404 ProblemDetails (unknown code)
* 410 ProblemDetails (expired)

### 6.3 Stats

* **GET** `/links/{code}/stats`
  **Response 200 (JSON):**

```json
{
  "code": "my-campaign",
  "createdAt": "2025-09-29T12:00:00Z",
  "expiration": "2026-01-01T00:00:00Z",
  "clicks": 1234,
  "lastAccess": "2025-09-29T13:37:00Z"
}
```

**Errors:** 404 ProblemDetails

---

## 7) Sequence Diagrams (ASCII)

### 7.1 Create (with Cache Warming - Slice 5)

```
Client
  │  POST /links { url, alias?, expiration? }
  ▼
Web API (Inbound Adapter)
  │  calls ICreateShortUrl
  ▼
CreateShortUrl (Use Case)
  │  validate URL/alias/expiration
  │  if no alias → ICodeGenerator.Generate()
  │  IShortUrlRepository.Add(new ShortUrl)
  │  ICacheStore.Set(code → originalUrl, 24h TTL)  ← Cache warming
  ▼
Response → 201 { code, shortUrl }
```

### 7.2 Resolve (with Redis + Negative Caching - Slice 5)

```
Client
  │  GET /{code}
  ▼
Web API (Inbound Adapter)
  │  calls IResolveShortUrl
  ▼
ResolveShortUrl (Use Case)
  │  ICacheStore.Get(code)
  │  │
  │  ├─ if value == "__NOT_FOUND__" → NotFoundException (404) [negative cache hit]
  │  │
  │  ├─ if cache hit (URL found):
  │  │  ├─ IShortUrlRepository.GetByCode(code)  [to update clicks]
  │  │  ├─ if found & not expired:
  │  │  │  ├─ RecordAccess() → ++clicks, lastAccess = now
  │  │  │  └─ UpdateAsync()
  │  │  └─ return redirect with cached URL
  │  │
  │  └─ if cache miss:
  │     ├─ IShortUrlRepository.GetByCode(code)
  │     ├─ if not found:
  │     │  ├─ ICacheStore.Set(code → "__NOT_FOUND__", 60s TTL)  ← negative cache
  │     │  └─ NotFoundException (404)
  │     ├─ if expired:
  │     │  ├─ ICacheStore.Set(code → "__NOT_FOUND__", 60s TTL)
  │     │  └─ ExpiredException (410)
  │     ├─ RecordAccess() → ++clicks, lastAccess = now
  │     ├─ UpdateAsync()
  │     ├─ ICacheStore.Set(code → originalUrl, 24h TTL)  ← positive cache
  │     └─ return redirect
  ▼
Response → 302/307 Location: originalUrl
```

### 7.3 Stats

```
Client
  │  GET /links/{code}/stats
  ▼
Web API
  │  calls IGetStats
  ▼
GetStats (Use Case)
  │  IShortUrlRepository.FindByCode(code)
  │  if not found → 404
  ▼
Response → 200 { createdAt, expiration, clicks, lastAccess }
```

---

## 8) Non-Functional Requirements (SLOs)

* **Latency**: p95 < **30 ms** on cached resolves (single region).
* **Availability**: target **99.9%** (demo scope).
* **Observability**:

  * Metrics: cache hit/miss counters, resolve latency histogram.
  * Structured logs for create/resolve.
* **Security**: accept `http|https` only, length caps, (later) rate limit `POST /links`.

---

## 9) Key Design Decisions (Trade-offs)

* **Redirect 302/307 vs 301**: prefer **temporary** (302/307) so targets can change; avoids over-caching by CDNs during dev.
* **Code Generation**: start with **Random Base62** (low collision risk + retry). Can swap via Strategy (`ICodeGenerator`).
* **Cache Strategy**: **cache-aside** with **negative caching** to reduce DB load and protect from brute-force.
* **Persistence**: EF Core + **migrations** (not `EnsureCreated`) for evolvability.
* **Ports in Core**: keeps Core independent and testable; edges are swappable.

---

## 10) Errors & Health

* **Errors**: use **RFC 7807 ProblemDetails** (`type`, `title`, `status`, `detail`).
* **Health**:

  * `/health/live` → process up.
  * `/health/ready` → DB + Redis reachable.
* **Resilience**: short timeouts; retries/backoff (Polly); circuit breaker for Redis with fallback to DB.

---

## 11) Configuration (examples)

* `Shortener:BaseUrl = https://sho.rt`
* `ConnectionStrings:Default = "Host=...;Database=...;User Id=...;Password=..."`
* `Redis:Connection = "localhost:6379"`  ✅ Slice 5
* `Redis:DefaultTtlSeconds = 86400` (24 hours)  ✅ Slice 5
* `Redis:NegativeTtlSeconds = 60` (1 minute)  ✅ Slice 5

---

## 12) Run Modes

* **Local (dev)**: Web API + PostgreSQL (Docker) + Redis (Docker)  ✅ Slice 5
* **CI (tests)**: spin Postgres/Redis via Docker; run unit + integration tests.
* **Demo/Prod (optional)**: Azure App Service + managed Postgres/Redis.
* **Fallback**: If Redis not configured, uses in-memory cache (single instance only).

---

## 13) Future Extensions (out of scope V1)

* Auth / multi-user ownership.
* Link editing/deletion.
* Custom domains.
* Advanced analytics (geo/device), exports.
* Background cleanup of expired links.