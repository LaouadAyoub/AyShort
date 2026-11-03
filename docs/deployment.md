# Portfolio deployment

Frontend: Vercel (`frontend/`). API: Render (repository root). Database: Neon
PostgreSQL 16 in Frankfurt, database `ays`. The public API uses in-memory caching;
there is no hosted Redis service.

## Public addresses

- Frontend: [app.ayshort.cc](https://app.ayshort.cc/)
- Short-link origin: [ayshort.cc](https://ayshort.cc)
- API documentation: [ayshort.cc/swagger](https://ayshort.cc/swagger)

The provider addresses `ay-short.vercel.app` and `ayshort-api.onrender.com`
remain available. The Vercel rewrite still targets the Render provider address.

## API container

The root Dockerfile publishes the existing .NET 9 API in a build stage, then copies
only its published output into a smaller ASP.NET runtime image. It runs as the
non-root `app` user and listens on the host's `PORT`, or 8080 locally.
The image disables Redis by default; environment variables can override this.

Build from the repository root with Docker Desktop running:

```powershell
docker build -t ayshort-api .
```

## Render setup

1. Create a Web Service from the AyShort GitHub repository. Select `master`
   for production and confirm the saved branch selection after refresh.
   Choose Docker, name `ayshort-api`, and region
   Frankfurt. Leave Root Directory and Docker Build Context Directory empty and
   use the root `Dockerfile`: the build needs the Core and adapter projects.
2. Review the compute plan before deploying. The smallest paid instance is
   $7/month before taxes and any usage overages. Free instances sleep after
   15 minutes idle and can take about a minute to wake up. No Render database,
   disk, Redis service, or paid workspace upgrade is required for this setup.
3. Add `ConnectionStrings__Default` as a Render environment variable. Use the Neon
   connection details in Npgsql format, with no surrounding quotes:

   ```text
   Host=<neon-host>;Port=5432;Database=ays;Username=<neon-role>;Password=<neon-password>;SSL Mode=VerifyFull;Include Error Detail=false
   ```

   Never commit the real value, paste it into chat, or put it in Vercel frontend
   variables. For initial migrations, use Neon's direct (non-pooled) connection.
4. Set `Shortener__BaseUrl=https://ayshort.cc` after verifying that custom domain
   and its HTTPS certificate. Before a custom domain exists, use the public
   Render HTTPS origin. Otherwise generated links use localhost.
5. Set Health Check Path to `/health/live`. Keep the Docker command override
   empty so the image's entrypoint starts the API on Render's assigned `PORT`.
   Deploy using the selected compute plan.
   The API applies EF Core migrations during startup, so the database must be
   reachable and the role must have migration privileges.
6. Check `/health/live`, `/health/ready`, and `/swagger`. Readiness should report
   the database Up and Redis NotConfigured. The current 500 ms DB readiness
   deadline can be exceeded when a sleeping Neon compute first wakes up; retry
   after it wakes. Use liveness for deployment checks.
7. Create a link and verify the returned public URL redirects to its destination.

Use one API replica initially. Cache entries can disappear on restart; links and
statistics remain in PostgreSQL. This setup creates a new hosted database; it does
not copy existing development data.

The original Railway configuration remains in `railway.json` as an alternative;
Render does not read it. We switched hosts because Railway rejected new account
creation on 2 November 2026 while signups were paused. See the providers' current
[status](https://status.railway.com/), [pricing](https://render.com/pricing), and
[free-tier limits](https://render.com/docs/free) before changing the setup.

## Frontend connection

The frontend calls `/api/links`. Vite's proxy is development-only.
`frontend/vercel.json` forwards `/api/:path*` to
`https://ayshort-api.onrender.com/:path*`, removing the `/api` prefix. Vercel's
project Root Directory must remain `frontend` so it loads this configuration.
The production deployment must include this file (currently from `master`);
adding it to a feature branch alone does not update the public production site.
Verify creation, validation errors, failure feedback, and redirection from the
public frontend after deployment. Keep `Shortener__BaseUrl` on Render set to
`https://ayshort.cc` so returned short links use the custom domain.

## Custom domains

Keep DNS at Namecheap and configure the hosts separately:

| DNS type | Host | Target |
| --- | --- | --- |
| CNAME | `app` | Exact target shown in the Vercel project Domains screen |
| A | `@` | Exact root-domain IP shown in Render's Custom Domains screen |
| CNAME | `www` | `ayshort-api.onrender.com` |

Add `app.ayshort.cc` to the frontend's production environment in Vercel and
`ayshort.cc` to the Render service. Verify DNS and HTTPS in both dashboards.
Keep the existing provider domains enabled so previously issued links continue
to work. The A-record target at setup was `216.24.57.1`; use the provider's
current instructions if recreating the configuration.

DNS determines which host receives a request. `Shortener__BaseUrl` determines
the origin printed in newly generated links. Changing that setting does not
replace the stored link codes or destinations. The current API has no `/`
homepage redirect to the frontend: share `https://app.ayshort.cc/` as the demo.

## Runtime maintenance

The project currently targets .NET 9. Plan a separate upgrade to a supported .NET
LTS release: .NET 9 support ends on 10 November 2026. Upgrade the projects,
dependencies, CI SDK and container images together and rerun the tests.
