# AyShort MCP — first local checkpoint

Scope: specification v0.1 slices 0–1 only. Implemented: a local Streamable
HTTP host and one temporary, harmless `server_info` tool. Link creation and
statistics through MCP are planned, not implemented. No AI key is needed.

## Phase 0 findings

- `AyShort.sln` targets .NET 10. `Core` contains domain rules, use cases and
  ports; `src/Adapters/In/WebApi` maps HTTP requests to those use cases.
  Outbound adapters provide PostgreSQL, Redis, in-memory cache, codes and time.
  React consumes the REST API. Existing backend tests use xUnit.
- The API requires PostgreSQL and applies migrations at startup. Redis is
  optional when `Redis:Connection` is empty; the checked-in configuration enables it.
  Connection-string placeholders must be overridden for local execution.
- There is no authentication/authorization middleware on the current endpoints.
  Keep the MCP demo local. Remote writes/analytics require a separate security review.
- CI restores, builds and tests with .NET 10 and a dedicated PostgreSQL test
  database. Adding the project to the solution includes it in the existing build.

| REST endpoint | Request | Success response |
| --- | --- | --- |
| `POST /links` | `{url, alias?, expiration?}` | 201 `{code, shortUrl}`, Location `/links/{code}` |
| `GET /{code}` | Route code | 302 redirect to original URL; records access |
| `GET /links/{code}/stats` | Route code | 200 `{createdAt, clicks, lastAccess, expiration}` |

Creation DTOs are in `Core/Application/DTOs/CreateShortUrlDtos.cs`; stats DTOs
are in `GetStatsDtos.cs`. Stats does **not** include the code or original URL.
Domain URL validation accepts absolute HTTP(S), at most 2048 characters.
Codes match `[a-zA-Z0-9_-]{3,20}`. Errors use ProblemDetails: validation 400,
conflict 409, missing 404, expired 410, unexpected 500.

The official `ModelContextProtocol.AspNetCore` package is pinned to **2.4.0**.
NuGet lists a native .NET 10 target; restore and compilation confirmed compatibility.
No framework upgrade was needed. Sources:
[official SDK](https://github.com/modelcontextprotocol/csharp-sdk),
[package frameworks](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore/2.4.0#supportedframeworks-body-tab),
[HTTP transport guidance](https://github.com/modelcontextprotocol/csharp-sdk/blob/main/docs/concepts/transports/transports.md).

## Structure and decisions

```text
src/Adapters/In/McpServer/
  AyShort.McpServer.csproj
  Program.cs
  appsettings.json
  Properties/launchSettings.json
  Tools/ServerInfoTools.cs
scripts/Test-McpCheckpoint.ps1
```

The separate ASP.NET Core process fits the inbound-adapter layout. It has no
Core/database reference. `AddMcpServer().WithHttpTransport()` registers SDK
lifecycle and dispatch; `WithTools<ServerInfoTools>()` registers the explicit
tool class; `MapMcp("/mcp")` maps the transport endpoint. Attributes describe
the tool and let the SDK generate its JSON schema. Annotations are client hints,
not authorization.

The SDK's default stateless HTTP mode is sufficient for request/response tools.
There is no session ID to persist in this checkpoint. The host binds to
`127.0.0.1:5143`, with loopback host names allowed. Do not override this to a
public binding for the demo.

The next reviewed slice should add `AyShortApiClient.cs` as a small typed
HttpClient plus `Tools/LinkTools.cs`, map `create_short_link` to `POST /links`,
and remove `server_info`. Configure base address, timeout and cancellation then.
After that, add `get_link_stats`, explicit response mapping and useful tool
errors. No direct database calls or duplicated shortening logic are needed.

## Run and reproduce

From the repository root, in PowerShell (.NET 10 SDK):

```powershell
dotnet run --project src/Adapters/In/McpServer/AyShort.McpServer.csproj
```

In a second terminal:

```powershell
./scripts/Test-McpCheckpoint.ps1
```

This is a raw HTTP protocol smoke test, not a general MCP client. It asserts
initialization, exactly one tool, object input schema, read-only annotation,
matching request IDs and successful invocation. It accepts JSON or a single
SSE response. PostgreSQL and the API are not required for this temporary tool.

For interactive review, run `npx @modelcontextprotocol/inspector`, open its
printed UI URL, select **Streamable HTTP**, and connect to
`http://127.0.0.1:5143/mcp`. List tools and call `server_info` with `{}`.
Inspector UI has not been manually verified in this checkpoint; the HTTP script
provides the protocol verification.

## Message flow

1. Client sends `initialize` with protocol version and client capabilities.
2. SDK returns the negotiated version and server capabilities; client sends
   `notifications/initialized` and uses the negotiated `MCP-Protocol-Version` header.
3. `tools/list` returns tool metadata and input JSON schema.
4. `tools/call` names `server_info` with empty arguments; the SDK dispatches to
   the C# method and returns its text with the same request ID.

Observed call and response (HTTP/SSE envelope omitted):

```json
{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"server_info","arguments":{}}}
{"result":{"content":[{"type":"text","text":"AyShort MCP server: local Streamable HTTP checkpoint. Link creation and statistics are not implemented yet."}]},"id":3,"jsonrpc":"2.0"}
```

HTTP carries the message; JSON-RPC identifies the operation and correlates the
response; MCP supplies initialization, discovery and tool-call semantics.
SSE response framing is part of Streamable HTTP and does not mean the legacy
SSE transport is being used. Later, a tool call will make a REST request to
AyShort; the current tool returns only local information.

## Verification — 24 November 2026

- `dotnet build AyShort.sln -c Release`: passed, zero warnings/errors.
- `dotnet test tests/Unit/UnitTests.csproj -c Release --no-build --no-restore`:
  14 passed.
- With `TEST_DB_CONNECTION` pointing to the Docker `ayshort_test` database on
  port 5433, `dotnet test tests/Integration/IntegrationTests.csproj -c Release
  --no-build --no-restore`: 20 passed.
- Existing API run with README local configuration: liveness Alive; readiness
  Healthy (PostgreSQL and Redis Up); `POST /links` for `https://example.com`
  returned 201 with code `mxofowp`; stats returned clicks 0 and null last access
  and expiration. Disposable local development data only.
- `./scripts/Test-McpCheckpoint.ps1`: passed initialize, tools/list, tools/call;
  negotiated `2025-11-25`, observed `text/event-stream` responses.

Initial restricted runs hit NuGet DNS/test-host access problems; verification
above used approved access. A subsequent restore resolved missing xUnit
references. Release verification avoided Debug file locks from the running API.

Stop here for review before adding real API tools.
