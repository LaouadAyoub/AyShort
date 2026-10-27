using Core.Application;
using Core.Application.DTOs;
using Core.Application.Ports.In;
using Core.Application.Ports.Out;
using Core.Application.Services;
using Core.Domain.Exceptions;
using Adapters.Out.Persistence.InMemory;
using Adapters.Out.Persistence.Sql;
using Adapters.Out.Time;
using Adapters.Out.Codes;
using Adapters.Out.Cache.Redis;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// Let the error middleware format binding failures consistently in every environment.
builder.Services.Configure<Microsoft.AspNetCore.Routing.RouteHandlerOptions>(options =>
    options.ThrowOnBadRequest = true);

builder.Services.AddSingleton(new ShortUrlOptions
{
    BaseUrl = builder.Configuration["Shortener:BaseUrl"] ?? "http://localhost:5142",
    MinTtlMinutes = 1,
    MaxTtlDays = 365,
    CodeLength = 7,
    NegativeCacheTtlSeconds = 60
});

// Add PostgreSQL database
builder.Services.AddDbContext<AyShortDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<ICodeGenerator, Base62CodeGenerator>();
// Switch from InMemory to SQL repository
builder.Services.AddScoped<IShortUrlRepository, SqlShortUrlRepository>();

// Configure cache: Use Redis if configured, otherwise fallback to InMemory
var redisConnection = builder.Configuration["Redis:Connection"];
if (!string.IsNullOrWhiteSpace(redisConnection))
{
    // Redis is configured - use it
    var redis = ConnectionMultiplexer.Connect(redisConnection);
    builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
    
    var redisOptions = new RedisCacheOptions
    {
        Connection = redisConnection,
        DefaultTtlSeconds = builder.Configuration.GetValue<int>("Redis:DefaultTtlSeconds", 86400),
        NegativeTtlSeconds = builder.Configuration.GetValue<int>("Redis:NegativeTtlSeconds", 60)
    };
    builder.Services.AddSingleton(redisOptions);
    builder.Services.AddSingleton<ICacheStore, RedisCacheStore>();
}
else
{
    // No Redis configured - use in-memory cache as fallback
    builder.Services.AddSingleton<ICacheStore, InMemoryCacheStore>();
}

// Services that use repository must also be scoped
builder.Services.AddScoped<ICreateShortUrl, CreateShortUrlService>();
builder.Services.AddScoped<IResolveShortUrl, ResolveShortUrlService>();
builder.Services.AddScoped<IGetStats, GetStatsService>();

var app = builder.Build();

// Apply migrations automatically on startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AyShortDbContext>();
    context.Database.Migrate();
}

app.UseSwagger();
app.UseSwaggerUI();

// Slice 6: Observability & Resilience
// Health Endpoints: /health/live (liveness) and /health/ready (readiness)
//  - live: cheap, always 200 unless catastrophic
//  - ready: verifies critical dependencies (DB + Redis if configured)

app.MapGet("/health/live", () => Results.Ok(new { status = "Alive" }))
    .WithName("Liveness")
    .Produces(StatusCodes.Status200OK);

app.MapGet("/health/ready", async (IServiceProvider sp, CancellationToken ct) =>
{
    var logger = sp.GetRequiredService<ILogger<Program>>();
    var dbOk = false;
    var redisConfigured = sp.GetService<IConnectionMultiplexer>() != null;
    var redisOk = !redisConfigured; // if not configured, treat as OK

    // DB readiness: try a lightweight connectivity check with timeout
    try
    {
        using var scope = sp.CreateScope();
        var ctxDb = scope.ServiceProvider.GetRequiredService<AyShortDbContext>();
        using var canConnectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        canConnectCts.CancelAfter(TimeSpan.FromMilliseconds(500));
        dbOk = await ctxDb.Database.CanConnectAsync(canConnectCts.Token);
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Readiness check: database unreachable");
        dbOk = false;
    }

    // Redis readiness (only if configured)
    if (redisConfigured)
    {
        try
        {
            var redis = sp.GetRequiredService<IConnectionMultiplexer>();
            var db = redis.GetDatabase();
            using var pingCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            pingCts.CancelAfter(TimeSpan.FromMilliseconds(200));
            // Redis does not accept a cancellation token; bound the wait for its reply.
            await db.PingAsync().WaitAsync(pingCts.Token);
            redisOk = true; // if PingAsync returns we are good
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Readiness check: redis unreachable");
            redisOk = false;
        }
    }

    var overallOk = dbOk && redisOk;
    var payload = new
    {
        status = overallOk ? "Healthy" : "Unhealthy",
        dependencies = new
        {
            database = dbOk ? "Up" : "Down",
            redis = redisConfigured ? (redisOk ? "Up" : "Down") : "NotConfigured"
        }
    };

    if (overallOk)
        return Results.Ok(payload);

    // Return 503 with same payload; using Results.Json to include body
    return Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable);
})
    .WithName("Readiness")
    .Produces(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status503ServiceUnavailable);

// Request logging scope middleware: attaches TraceId to all subsequent logs for this request
app.Use(async (ctx, next) =>
{
    var logger = ctx.RequestServices.GetRequiredService<ILogger<Program>>();
    using (logger.BeginScope(new Dictionary<string, object>
    {
        ["TraceId"] = ctx.TraceIdentifier
    }))
    {
        await next();
    }
});

// Global error translation middleware: converts ALL exceptions to RFC7807 ProblemDetails
app.Use(async (ctx, next) =>
{
    var logger = ctx.RequestServices.GetRequiredService<ILogger<Program>>();
    try
    {
        await next();
    }
    catch (BadHttpRequestException ex)
    {
        logger.LogWarning(ex, "Invalid HTTP request: status={Status}", ex.StatusCode);
        await Results.Problem(
            statusCode: ex.StatusCode,
            title: "Invalid request",
            detail: "The request body or parameters are invalid.",
            instance: ctx.Request.Path.Value,
            extensions: new Dictionary<string, object?> { ["traceId"] = ctx.TraceIdentifier })
            .ExecuteAsync(ctx);
    }
    catch (DomainException ex)
    {
        var status = ex switch
        {
            ValidationException => StatusCodes.Status400BadRequest,
            ConflictException => StatusCodes.Status409Conflict,
            NotFoundException => StatusCodes.Status404NotFound,
            ExpiredException => StatusCodes.Status410Gone,
            _ => StatusCodes.Status500InternalServerError
        };

        // Choose log level based on domain exception type
        var level = ex switch
        {
            ValidationException => LogLevel.Warning,
            ConflictException => LogLevel.Warning,
            NotFoundException => LogLevel.Information,
            ExpiredException => LogLevel.Information,
            _ => LogLevel.Error
        };
        logger.Log(level, ex, "Domain exception handled: {ExceptionType} status={Status} code={Code?}", ex.GetType().Name, status, (ex as ConflictException)?.Message);

        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/problem+json";
        var problem = new
        {
            type = "about:blank",
            title = ex.GetType().Name,
            status,
            detail = ex.Message,
            traceId = ctx.TraceIdentifier,
            instance = ctx.Request.Path.Value
        };
        await ctx.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
    }
    catch (Exception ex)
    {
        // Unexpected exception: always 500, generic title; do not leak internal stack details to client
        logger.LogError(ex, "Unhandled exception encountered.");
        const int status = StatusCodes.Status500InternalServerError;
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/problem+json";
        var problem = new
        {
            type = "about:blank",
            title = "InternalServerError",
            status,
            detail = "An unexpected error occurred.",
            traceId = ctx.TraceIdentifier,
            instance = ctx.Request.Path.Value
        };
        await ctx.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
    }
});

app.MapPost("/links", async (ICreateShortUrl useCase, CreateShortUrlRequest req, CancellationToken ct) =>
{
    var result = await useCase.ExecuteAsync(req, ct);
    return Results.Created($"/links/{result.Code}", result);
})
    .Produces<CreateShortUrlResult>(StatusCodes.Status201Created)
    .Produces(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status409Conflict);

app.MapGet("/{code}", async (IResolveShortUrl useCase, string code, CancellationToken ct) =>
{
    var result = await useCase.ExecuteAsync(new ResolveShortUrlRequest(code), ct);
    return Results.Redirect(result.OriginalUrl, permanent: false);
})
    .Produces(StatusCodes.Status302Found)
    .Produces(StatusCodes.Status404NotFound)
    .Produces(StatusCodes.Status410Gone);

app.MapGet("/links/{code}/stats", async (IGetStats useCase, string code, CancellationToken ct) =>
{
    var result = await useCase.ExecuteAsync(new GetStatsRequest(code), ct);
    return Results.Ok(result);
})
    .Produces<GetStatsResult>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound);

app.Run();

public partial class Program { }
