# AyShort

![CI](https://github.com/LaouadAyoub/AyShort/actions/workflows/ci.yml/badge.svg)

A small URL shortener built with **Hexagonal Architecture** and **vertical slices**.

## Endpoints (V1)
- POST /links → create short link ✅ Slice 1 
- GET /{code} → redirect ✅ Slice 2 
- GET /links/{code}/stats → basic stats ✅ Slice 3

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
	▼
Response → 201 { code, shortUrl }
```

## Resolve Flow (Slice 2)

```text
Client
	│  GET /{code}
	▼
Web API (Inbound Adapter)
	│  calls IResolveShortUrl
	▼
ResolveShortUrl (Use Case)
	│  ICacheStore.Get(code)      ── cache hit? → return redirect
	│  IShortUrlRepository.GetByCode(code)
	│  if not found → 404
	│  if expired → 410
	│  ++ClicksCount; LastAccessAt = now; UpdateAsync
	│  ICacheStore.Set(code → originalUrl, 24h TTL)
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
