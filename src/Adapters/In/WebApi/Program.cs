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
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton(new ShortUrlOptions
{
    BaseUrl = builder.Configuration["Shortener:BaseUrl"] ?? "http://localhost:5142",
    MinTtlMinutes = 1,
    MaxTtlDays = 365,
    CodeLength = 7
});

// Add PostgreSQL database
builder.Services.AddDbContext<AyShortDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<ICodeGenerator, Base62CodeGenerator>();
// Switch from InMemory to SQL repository
builder.Services.AddScoped<IShortUrlRepository, SqlShortUrlRepository>();
builder.Services.AddSingleton<ICacheStore, InMemoryCacheStore>();
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

app.Use(async (ctx, next) =>
{
    try { await next(); }
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
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/problem+json";
        await ctx.Response.WriteAsJsonAsync(new
        {
            type = "about:blank",
            title = ex.GetType().Name,
            status,
            detail = ex.Message
        });
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