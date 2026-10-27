# AyShort

![CI](https://github.com/LaouadAyoub/AyShort/actions/workflows/ci.yml/badge.svg)

A small URL shortener built with **Hexagonal Architecture** and **vertical slices**.

## Endpoints (V1)
- POST /links → create short link ✅ Slice 1 
- GET /{code} → redirect ✅ Slice 2 
- GET /links/{code}/stats → basic stats ✅ Slice 3
- Persistence: EF Core + PostgreSQL ✅ Slice 4
- Cache: Redis (with fallback to in-memory) ✅ Slice 5

---

## Create Flow (Slice 1)

```text
Client
	│  POST /links { url, alias?, expiration? }
	▼
Web API (Inbound Adapter)
	│  calls ICreateShortUrl
	▼
CreateShortUrl (Use Case)
	│  validate URL/alias/expiration
	│  if no alias → ICodeGenerator.Generate()
	│  retry generation until unique (bounded)
	│  IShortUrlRepository.Add(new ShortUrl)
	│  ICacheStore.Set(code → url, 24h TTL) [Cache warming]
	▼
Response → 201 { code, shortUrl }
```

## Resolve Flow (Slice 2 + Slice 5: Redis Cache)

```text
Client
	│  GET /{code}
	▼
Web API (Inbound Adapter)
	│  calls IResolveShortUrl
	▼
ResolveShortUrl (Use Case)
	│  ICacheStore.Get(code)
	│  │
	│  ├─ If negative marker ("__NOT_FOUND__") → throw NotFoundException (404)
	│  │
	│  ├─ If cache hit (URL found):
	│  │  ├─ Get entity from DB (to update clicks)
	│  │  ├─ ++ClicksCount; LastAccessAt = now
	│  │  ├─ UpdateAsync
	│  │  └─ Return cached URL
	│  │
	│  └─ If cache miss:
	│     ├─ IShortUrlRepository.GetByCode(code)
	│     ├─ If not found → cache negative marker (60s TTL) → 404
	│     ├─ If expired → cache negative marker (60s TTL) → 410
	│     ├─ ++ClicksCount; LastAccessAt = now
	│     ├─ UpdateAsync
	│     ├─ ICacheStore.Set(code → url, 24h TTL)
	│     └─ Return URL
	▼
Response → 302 Redirect Location: originalUrl
```

## Stats Flow (Slice 3)

```text
Client
	│  GET /links/{code}/stats
	▼
Web API (Inbound Adapter)
	│  calls IGetStats
	▼
GetStats (Use Case)
	│  IShortUrlRepository.GetByCode(code)
	│  if not found → 404
	│  map entity to stats DTO
	▼
Response → 200 { createdAt, clicks, lastAccess, expiration }
```

### Example Request
```http
POST /links HTTP/1.1
Content-Type: application/json

{
	"url": "https://example.com/very/long/path?x=1",
	"alias": "my-campaign",
	"expiration": "2026-01-01T00:00:00Z"
}
```

### Example Success (201)
```json
{
	"code": "my-campaign",
	"shortUrl": "https://sho.rt/my-campaign"
}
```

### Example Error (alias taken, 409)
```json
{
	"type": "about:blank",
	"title": "ConflictException",
	"status": 409,
	"detail": "Alias already in use."
}
```

### Example Stats Request
```http
GET /links/my-campaign/stats HTTP/1.1
```

### Example Stats Response (200)
```json
{
	"createdAt": "2025-10-01T10:00:00Z",
	"clicks": 42,
	"lastAccess": "2025-10-01T18:30:15Z",
	"expiration": "2026-01-01T00:00:00Z"
}
```

### Example Stats Error (not found, 404)
```json
{
	"type": "about:blank",
	"title": "NotFoundException",
	"status": 404,
	"detail": "Short URL with code 'unknown' not found."
}
```

---

## Persistence (Slice 4: SQL)

AyShort uses EF Core for SQL persistence (PostgreSQL recommended). The SQL adapter is isolated in `Adapters/Out/Persistence.Sql`.

**Hexagonal boundaries:** Core has no framework dependencies. All infrastructure (EF, Redis, DI) lives in Adapters.

**Connection string:**
- Use placeholders in config files (`appsettings.json`, `appsettings.Development.json`).
- Supply real credentials via environment variables or .NET User Secrets (local dev only).
- Never commit secrets to source control.

**Migrations:**
Run EF Core migrations from the SQL adapter:
```powershell
dotnet ef migrations add <Name> --project src/Adapters/Out/Persistence.Sql --startup-project src/Adapters/In/WebApi
dotnet ef database update --project src/Adapters/Out/Persistence.Sql --startup-project src/Adapters/In/WebApi
```

**Testing:**
Unit tests cover use cases in Core. Integration tests verify endpoint contracts and persistence.

**Vertical slice delivery:**
Each feature is implemented end-to-end, respecting boundaries and keeping changes minimal.

---

## Integration Tests (Isolated Test Database)

Integration tests run against a real PostgreSQL database instance separate from the dev database to avoid polluting local data.

1. Start the dedicated test database (defined as `postgres-test` in `docker-compose.yml`):
	```powershell
	docker compose up -d postgres-test
	```
2. Create (if not already) a database whose name includes `_test` (example: `ayshort_test`). The guard requires `_test` in the name to prevent accidental prod/dev usage.
3. Set the `TEST_DB_CONNECTION` environment variable (the test harness maps this to the runtime `ConnectionStrings:Default`). Example:
	```powershell
	$env:TEST_DB_CONNECTION = "Host=__HOST__;Port=__PORT__;Database=__DB__;User Id=__USER__;Password=__PASSWORD__";
	```
4. Run tests:
	```powershell
	dotnet test
	```

Notes:
- If `TEST_DB_CONNECTION` is missing, integration tests fail fast with a clear error.
- The safety check rejects databases without `_test` in their name.
- Migrations are applied automatically on test host startup.
- Unit tests do not require any database.

### CI (GitHub Actions) Setup

The workflow spins up a disposable Postgres service and sets `TEST_DB_CONNECTION` automatically. Excerpt:

```yaml
services:
	postgres-test:
		image: postgres:16
		ports:
			- 5433:5432
		env:
			POSTGRES_USER: test
			POSTGRES_PASSWORD: test
			POSTGRES_DB: ayshort_test
		options: >-
			--health-cmd "pg_isready -U test -d ayshort_test" \
			--health-interval 5s \
			--health-timeout 5s \
			--health-retries 10

env:
	TEST_DB_CONNECTION: Host=localhost;Port=5433;Database=ayshort_test;User Id=test;Password=test;Include Error Detail=true
```

Tests are split so unit tests run first (fast feedback) followed by integration tests that need the database.


---

## Run Dev Infra
```bash
docker compose up -d
```

---

## 📚 Documentation

- **[Architecture](docs/02-architecture.md)** - Hexagonal design, flows, and technical decisions
- **[Product Brief](docs/01-product-brief.md)** - Goals, features, and requirements
- **[Roadmap](docs/03-roadmap.md)** - Vertical slices and implementation progress
- **[Testing Redis Cache](docs/04-testing-redis.md)** - Complete guide to verify Redis is working
- **[Redis Monitoring Quick Start](docs/REDIS-MONITORING.md)** - Commands and tools for monitoring cache

---

## 🧪 Testing Redis Cache

### Quick Check
```powershell
# Is Redis working?
docker exec ayshort-redis redis-cli PING
# Should return: PONG

# See cached keys
docker exec ayshort-redis redis-cli KEYS "*"

# Watch cache in real-time
docker exec -it ayshort-redis redis-cli MONITOR
```

### Visual Monitoring
- **RedisInsight** (recommended): https://redis.io/insight/
- **Redis Commander**: `docker run -d -p 8081:8081 -e REDIS_HOSTS=local:host.docker.internal:6379 rediscommander/redis-commander`

### Test Script
```powershell
# Automated test that verifies cache warming, hits, and negative caching
.\scripts\test-redis-cache.ps1
```

See **[docs/04-testing-redis.md](docs/04-testing-redis.md)** for detailed testing guide.

---

This starts:
- **PostgreSQL** (host port 5434) - main database
- **PostgreSQL-test** (port 5433) - isolated test database
- **Redis** (port 6379) - cache

### Configuration

**Redis Cache (Slice 5):**
- Configure Redis in `appsettings.json`:
  ```json
  "Redis": {
    "Connection": "localhost:6379",
    "DefaultTtlSeconds": 86400,  // 24 hours for positive cache
    "NegativeTtlSeconds": 60      // 1 minute for negative cache (404s)
  }
  ```
- If `Redis:Connection` is not set or Redis is unavailable, the app automatically falls back to in-memory cache
- **Negative caching:** Unknown codes are cached with `"__NOT_FOUND__"` marker for 60 seconds to prevent repeated DB lookups from brute-force probing
- **Cache warming:** New short URLs are immediately cached on creation for instant first-resolve

**Database:**
- Set connection string via environment variable or User Secrets:
  ```powershell
  $env:ConnectionStrings__Default = "Host=127.0.0.1;Port=5434;Database=ays;User Id=ays;Password=ays"
  ```
- For Visual Studio, save the same value in User Secrets:
  ```powershell
  dotnet user-secrets set "ConnectionStrings:Default" "Host=127.0.0.1;Port=5434;Database=ays;User Id=ays;Password=ays;Include Error Detail=true" --project src/Adapters/In/WebApi/WebApi.csproj
  ```
- The Docker database uses host port 5434 to avoid conflicts with a local PostgreSQL installation on port 5432. After changing the port mapping, run `docker compose up -d postgres` to recreate the container with its existing data volume.

**Running the API:**
```powershell
dotnet run --project src/Adapters/In/WebApi/WebApi.csproj
```
