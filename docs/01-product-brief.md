# AyShort — Product Brief

## Problem
Sharing long URLs is messy. We need a small service that creates short links, redirects fast, and shows basic stats.

## Audience
- Creators: shorten links with alias/expiration.
- Visitors: click short links and get redirected.

## Goals
- Create short URLs (alias?, expiration?).
- Resolve fast (snappy redirect).
- Show stats (clicks, last access).
- Teach & showcase: Hexagonal Architecture + Redis + EF Core.

## Non-Goals (V1)
Auth, editing/deleting links, custom domains, advanced analytics.

## Non-Functional
- p95 < 30 ms for cached resolves.
- 99.9% target availability (demo).
- Portable locally via Docker.
- Basic observability (metrics/logs).