# Portfolio deployment

Frontend: Vercel (`frontend/`). API: Railway (repository root). Database: Neon
PostgreSQL 16 in Frankfurt, database `ays`. The public API uses in-memory caching;
there is no hosted Redis service.

## API container

The root Dockerfile publishes the existing .NET 9 API in a build stage, then copies
only its published output into a smaller ASP.NET runtime image. It runs as the
non-root `app` user and listens on Railway's `PORT`, or 8080 locally.
The image disables Redis by default; environment variables can override this.

Build from the repository root with Docker Desktop running:

```powershell
docker build -t ayshort-api .
```

## Railway setup

1. Create an API service from the AyShort GitHub repository using the branch that
   contains these deployment files. Use the repository root, not `frontend/` or
   the WebApi subdirectory: the build needs the referenced Core and adapter projects.
2. Review the account's plan and usage settings before enabling paid hosting.
3. Add `ConnectionStrings__Default` as a Railway service variable. Use the Neon
   connection details in Npgsql format, with no surrounding quotes:

   ```text
   Host=<neon-host>;Port=5432;Database=ays;Username=<neon-role>;Password=<neon-password>;SSL Mode=VerifyFull;Include Error Detail=false
   ```

   Never commit the real value, paste it into chat, or put it in Vercel frontend
   variables. For initial migrations, use Neon's direct (non-pooled) connection.
4. Generate the service's public Railway domain. Set `Shortener__BaseUrl` to its
   full HTTPS origin, with no trailing path. Otherwise generated links use localhost.
5. Deploy. `railway.json` selects the Dockerfile and `/health/live` health check.
   The API applies EF Core migrations during startup, so the database must be
   reachable and the role must have migration privileges.
6. Check `/health/live`, `/health/ready`, and `/swagger`. Readiness should report
   the database Up and Redis NotConfigured. The current 500 ms DB readiness
   deadline can be exceeded when a sleeping Neon compute first wakes up; retry
   after it wakes. Railway uses liveness for deployment checks.
7. Create a link and verify the returned public URL redirects to its destination.

Use one API replica initially. Cache entries can disappear on restart; links and
statistics remain in PostgreSQL. This setup creates a new hosted database; it does
not copy existing development data.

## Frontend connection (next checkpoint)

The frontend currently calls `/api/links`. Vite's proxy is development-only.
After the API's public domain exists, configure Vercel to proxy `/api/:path*` to
the API's `/:path*`. Do not route to localhost. Verify creation, validation errors,
failure feedback, and redirection from the public frontend.

## Runtime maintenance

The project currently targets .NET 9. Plan a separate upgrade to a supported .NET
LTS release: .NET 9 support ends on 10 November 2026. Upgrade the projects,
dependencies, CI SDK and container images together and rerun the tests.
