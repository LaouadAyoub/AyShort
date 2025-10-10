# Testing & Monitoring Redis Cache

This guide shows you how to verify that Redis caching is working in AyShort and how to monitor cache behavior.

---

## 1. Start Redis

### Using Docker Compose (Recommended)
```powershell
# Start Redis (and optionally Postgres)
docker compose up -d redis

# Verify Redis is running
docker ps | Select-String redis
```

### Using Redis CLI to verify connection
```powershell
# Connect to Redis CLI inside the container
docker exec -it ayshort-redis redis-cli

# Inside Redis CLI, test with:
PING
# Should respond: PONG

# Exit
exit
```

---

## 2. Manual Testing (Step-by-Step)

### Test 1: Verify Cache Warming on Creation

1. **Start the API**:
```powershell
dotnet run --project src/Adapters/In/WebApi/WebApi.csproj
```

2. **Create a short link**:
```powershell
# Using PowerShell
$body = @{
    url = "https://example.com/test-cache"
    alias = "testcache"
} | ConvertTo-Json

Invoke-RestMethod -Uri "http://localhost:5142/links" -Method POST -Body $body -ContentType "application/json"
```

3. **Check Redis directly**:
```powershell
# Connect to Redis CLI
docker exec -it ayshort-redis redis-cli

# Inside Redis CLI:
GET testcache
# Should show: "https://example.com/test-cache"

# Check TTL (time to live)
TTL testcache
# Should show remaining seconds (e.g., 86399 for ~24 hours)

exit
```

✅ **Expected**: The link is immediately cached after creation.

---

### Test 2: Verify Cache Hit on Resolve

1. **First resolve** (cache should be hit):
```powershell
# Using a browser or curl
Start-Process "http://localhost:5142/testcache"

# Or using PowerShell (don't follow redirect to see the 302)
Invoke-WebRequest -Uri "http://localhost:5142/testcache" -MaximumRedirection 0 -ErrorAction SilentlyContinue
```

2. **Check application logs** to see cache behavior (you'll see faster response times).

3. **Verify in Redis**:
```powershell
docker exec -it ayshort-redis redis-cli

# Check the key still exists and TTL is refreshed/maintained
GET testcache
TTL testcache
```

✅ **Expected**: Link resolves instantly from cache.

---

### Test 3: Verify Negative Caching (404 Protection)

1. **Try a non-existent code**:
```powershell
Invoke-WebRequest -Uri "http://localhost:5142/doesnotexist123" -ErrorAction SilentlyContinue
# Should get 404
```

2. **Check Redis for negative cache marker**:
```powershell
docker exec -it ayshort-redis redis-cli

# Check if negative marker was cached
GET doesnotexist123
# Should show: "__NOT_FOUND__"

# Check TTL (should be short, ~60 seconds)
TTL doesnotexist123
﻿# Redis Cache – Quick Guide

Simple, fast checklist to confirm Redis caching works in AyShort. No fluff.

---

## 1. Start Things
```powershell
docker compose up -d redis       # Start Redis
dotnet run --project src/Adapters/In/WebApi/WebApi.csproj
```
Check Redis is up:
```powershell
docker ps | Select-String redis
docker exec -it ayshort-redis redis-cli PING   # Expect PONG
```

---

## 2. Create & Cache
```powershell
$body = @{ url = "https://example.com/cache"; alias = "cachetest" } | ConvertTo-Json
Invoke-RestMethod -Uri "http://localhost:5142/links" -Method POST -Body $body -ContentType "application/json"
```
Check Redis:
```powershell
docker exec -it ayshort-redis redis-cli GET cachetest
docker exec -it ayshort-redis redis-cli TTL cachetest   # ~86400 seconds
```
✅ If value + TTL show → cache warming works.

---

## 3. Resolve (Cache Hit)
```powershell
Invoke-WebRequest -Uri "http://localhost:5142/cachetest" -MaximumRedirection 0 -ErrorAction SilentlyContinue
```
Should return 302 fast. Key still exists.

---

## 4. Negative Cache (404)
```powershell
Invoke-WebRequest -Uri "http://localhost:5142/doesnotexist123" -ErrorAction SilentlyContinue | Out-Null
docker exec -it ayshort-redis redis-cli GET doesnotexist123   # __NOT_FOUND__
docker exec -it ayshort-redis redis-cli TTL doesnotexist123   # ~60
```
✅ Marker + short TTL → negative caching works.

---

## 5. Fallback (Optional)
```powershell
docker compose stop redis
# Restart API (Ctrl+C then run again)
```
App should still function (uses in‑memory cache). Restart Redis later:
```powershell
docker compose start redis
```

---

## 6. Handy Commands
```redis
KEYS *          # List keys (dev only)
GET key         # See value
TTL key         # Seconds left
DEL key         # Remove
FLUSHDB         # Wipe (dev only!)
INFO stats      # Basic metrics
MONITOR         # Stream all commands (Ctrl+C to exit)
```
Live watch while using API:
```powershell
docker exec -it ayshort-redis redis-cli MONITOR
```

---

## 7. GUI (Optional)
Use RedisInsight if you want a UI: https://redis.io/insight/ (connect to localhost:6379).

---

## 8. If Something’s Wrong
| Issue | Quick Check |
|-------|-------------|
| PING fails | `docker ps` (container running?) |
| Key missing after create | Did POST succeed? Check API output & logs |
| No `__NOT_FOUND__` | Request may not be 404; try a random code |
| TTL = -1 | Expiry not applied → configuration issue |
| Very slow resolves | Redis down? Using DB path first time |

---

## 9. Success Criteria
You’re good when:
* Created link appears in Redis with TTL.
* Resolves are instant (after first).
* Unknown codes set `__NOT_FOUND__` with short TTL.
* App still runs with Redis stopped.

Done. Keep it simple.
1. Open RedisInsight
