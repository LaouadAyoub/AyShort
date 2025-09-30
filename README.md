# AyShort

![CI](https://github.com/LaouadAyoub/AyShort/actions/workflows/ci.yml/badge.svg)

A small URL shortener built with **Hexagonal Architecture** and **vertical slices**.

## Endpoints (V1)
- POST /links → create short link ✅ Slice 1 
- GET /{code} → redirect ✅ Slice 2 
- GET /links/{code}/stats → basic stats

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

---

## Run Dev Infra
```bash
docker compose up -d
```
