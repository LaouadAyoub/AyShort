# AyShort

A small URL shortener built with **Hexagonal Architecture** and **vertical slices**.

## Endpoints (V1)
- POST /links → create short link
- GET /{code} → redirect
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
