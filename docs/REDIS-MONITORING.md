# Redis Monitoring Quick Start

## 🚀 Quick Commands

### Check if Redis is working
```powershell
# Is Redis running?
docker ps | Select-String redis

# Can you connect?
docker exec ayshort-redis redis-cli PING
# Should return: PONG
```

### See what's cached
```powershell
# List all cached short codes
docker exec ayshort-redis redis-cli KEYS "*"

# Get a specific cached URL
docker exec ayshort-redis redis-cli GET "abc123"

# Check how long until expiry (in seconds)
docker exec ayshort-redis redis-cli TTL "abc123"
# 86400 = 24 hours
# 60 = 1 minute (negative cache)
# -1 = no expiry
# -2 = key doesn't exist
```

### Watch Redis in real-time
```powershell
# See EVERY command as it happens (press Ctrl+C to stop)
docker exec -it ayshort-redis redis-cli MONITOR

# Then in another terminal, use your API
# You'll see commands like:
# "SET" "abc123" "https://example.com" "EX" "86400"
# "GET" "abc123"
```

### Get statistics
```powershell
# See cache hits vs misses
docker exec ayshort-redis redis-cli INFO stats | Select-String "keyspace"

# Output example:
# keyspace_hits:42      ← Found in cache
# keyspace_misses:8     ← Had to fetch from DB
# Hit rate = 42/(42+8) = 84% 🎯
```

### Clean up (testing only!)
```powershell
# Delete a specific key
docker exec ayshort-redis redis-cli DEL "abc123"

# Delete ALL keys (⚠️ DANGER! Only for local testing)
docker exec ayshort-redis redis-cli FLUSHDB
```

---

## 🎨 Visual Monitoring Tools

### 1. RedisInsight (Recommended) ⭐
- **Download**: https://redis.io/insight/
- **Free**, official tool from Redis
- **Features**:
  - 🔍 Browse all keys visually
  - ⏱️ See TTL countdown
  - 📊 Memory usage graphs
  - 🔥 Real-time command profiler
  - 🛠️ Built-in CLI

**Setup:**
1. Download and install RedisInsight
2. Click "Add Database"
3. Enter:
   - Host: `localhost`
   - Port: `6379`
   - Name: `AyShort Local`
4. Connect!

**What you'll see:**
- All your short codes as keys
- Their cached URLs as values
- TTL counting down in real-time
- Which keys are accessed most

### 2. Redis Commander (Web-based)
```powershell
# Run in Docker (one command)
docker run --rm -d `
  --name redis-commander `
  -e REDIS_HOSTS=local:host.docker.internal:6379 `
  -p 8081:8081 `
  rediscommander/redis-commander:latest

# Open browser
Start-Process "http://localhost:8081"

# Stop when done
docker stop redis-commander
```

### 3. Redis CLI (Built-in, always available)
```powershell
# Interactive mode
docker exec -it ayshort-redis redis-cli

# Inside Redis CLI:
redis> KEYS *                    # List all
redis> GET abc123                # Get value
redis> TTL abc123                # Time to live
redis> MONITOR                   # Watch commands
redis> INFO stats                # Statistics
redis> DBSIZE                    # Total keys
redis> exit
```

---

## 🧪 Manual Testing Workflow

### Test 1: Cache Warming (POST /links)
```powershell
# 1. Create a short link
$body = @{ url = "https://example.com/test" } | ConvertTo-Json
$result = Invoke-RestMethod -Uri "http://localhost:5142/links" -Method POST -Body $body -ContentType "application/json"
$code = $result.code

# 2. Immediately check Redis
docker exec ayshort-redis redis-cli GET $code
# ✅ Should show the URL instantly!

# 3. Check TTL
docker exec ayshort-redis redis-cli TTL $code
# ✅ Should be ~86400 (24 hours)
```

### Test 2: Cache Hit (GET /{code})
```powershell
# 1. Resolve the link twice
Invoke-WebRequest -Uri "http://localhost:5142/$code" -MaximumRedirection 0 -ErrorAction SilentlyContinue
Invoke-WebRequest -Uri "http://localhost:5142/$code" -MaximumRedirection 0 -ErrorAction SilentlyContinue

# 2. Check stats
docker exec ayshort-redis redis-cli INFO stats | Select-String "keyspace_hits"
# ✅ Hit count should increase
```

### Test 3: Negative Cache (404)
```powershell
# 1. Try a non-existent code
Invoke-WebRequest -Uri "http://localhost:5142/doesnotexist" -ErrorAction SilentlyContinue

# 2. Check if negative marker is cached
docker exec ayshort-redis redis-cli GET "doesnotexist"
# ✅ Should show: __NOT_FOUND__

# 3. Check TTL (should be short)
docker exec ayshort-redis redis-cli TTL "doesnotexist"
# ✅ Should be ~60 seconds

# 4. Try again immediately (should be faster, no DB hit)
Invoke-WebRequest -Uri "http://localhost:5142/doesnotexist" -ErrorAction SilentlyContinue
```

---

## 📊 Understanding the Output

### TTL (Time To Live)
| Value | Meaning |
|-------|---------|
| `86400` | 24 hours (positive cache - valid URL) |
| `60` | 1 minute (negative cache - 404) |
| `-1` | No expiration set (shouldn't happen) |
| `-2` | Key doesn't exist |

### Cache Hit Rate
```
Hit Rate = hits / (hits + misses)

Example:
- keyspace_hits: 42
- keyspace_misses: 8
- Hit Rate = 42 / (42 + 8) = 84% ✅

Good: >70%
Great: >90%
```

### Key Patterns
```
p4Z9eM6                    ← Valid short code (cached URL)
__NOT_FOUND__              ← Negative cache marker value (not a key name)
```

---

## 🔧 Troubleshooting

### "Cannot connect to Redis"
```powershell
# Start Redis
docker compose up -d redis

# Verify it's running
docker ps | Select-String redis

# Test connection
docker exec ayshort-redis redis-cli PING
```

### "No keys in cache"
1. Is the API running with Redis configured?
2. Check `appsettings.Development.json` has `Redis:Connection`
3. Create a link via POST /links
4. Run `docker exec ayshort-redis redis-cli KEYS "*"`

### "API using InMemory instead of Redis"
Check API logs on startup. Should NOT see Redis connection errors.

---

## 🎯 Production Monitoring

For production environments, use:

1. **Redis Enterprise Cloud** - Managed Redis with built-in dashboard
2. **AWS ElastiCache** - CloudWatch metrics integration
3. **Azure Cache for Redis** - Azure Monitor integration
4. **Datadog/New Relic** - APM with Redis monitoring
5. **Prometheus + Grafana** - Custom dashboards

All provide:
- ✅ Hit/miss ratio over time
- ✅ Memory usage trends
- ✅ Latency percentiles (p50, p95, p99)
- ✅ Eviction rates
- ✅ Connection count
- ✅ Alerting

---

## 📚 Related Documentation

- **Full Guide**: `docs/04-testing-redis.md`
- **Test Script**: `scripts/test-redis-cache.ps1`
- **Architecture**: `docs/02-architecture.md` (see Redis adapter section)

---

## ✅ Summary

**Redis is working when you see:**
1. ✅ Keys appear after POST /links
2. ✅ GET requests are fast (cache hits)
3. ✅ 404s create `__NOT_FOUND__` markers
4. ✅ TTLs count down (24h for valid, 60s for 404)

**Best monitoring: RedisInsight** (free, visual, powerful)

**Quick check: `docker exec ayshort-redis redis-cli MONITOR`** (watch live)
