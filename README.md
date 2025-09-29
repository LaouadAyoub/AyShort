# AyShort

A small URL shortener built with **Hexagonal Architecture** and **vertical slices**.

## Endpoints (V1)
- POST /links → create short link
- GET /{code} → redirect
- GET /links/{code}/stats → basic stats

## Run Dev Infra
```bash
docker compose up -d
```
