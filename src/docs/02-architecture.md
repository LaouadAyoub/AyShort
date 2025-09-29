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
- **Cache.Redis (StackExchange.Redis)**  
  - Implements `ICacheStore` with **cache-aside** and **negative caching**.  
  - Key: `link:{code}` → value: `{ originalUrl, expiration }` (+ TTL).
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

### 7.1 Create

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
  │  ICacheStore.Set(code → originalUrl, TTL)
  ▼
Response → 201 { code, shortUrl }
```

### 7.2 Resolve

```
Client
  │  GET /{code}
  ▼
Web API (Inbound Adapter)
  │  calls IResolveShortUrl
  ▼
ResolveShortUrl (Use Case)
  │  ICacheStore.Get(code)      ── cache hit? → return redirect
  │  IShortUrlRepository.FindByCode(code)
  │  if not found → negative cache (short TTL) → 404
  │  if expired → 410
  │  ++ClicksCount; LastAccessAt = now; save
  │  ICacheStore.Set(code → originalUrl, TTL)
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
* `Redis:Connection = "localhost:6379"`
* `Cache:DefaultTtlSeconds = 86400`
* `Cache:NegativeTtlSeconds = 60`

---

## 12) Run Modes

* **Local (dev)**: Web API + SQLite (file) + Redis (Docker) or in-memory cache during early slices.
* **CI (tests)**: spin Postgres/Redis via Docker; run unit + integration tests.
* **Demo/Prod (optional)**: Azure App Service + managed Postgres/Redis.

---

## 13) Future Extensions (out of scope V1)

* Auth / multi-user ownership.
* Link editing/deletion.
* Custom domains.
* Advanced analytics (geo/device), exports.
* Background cleanup of expired links.