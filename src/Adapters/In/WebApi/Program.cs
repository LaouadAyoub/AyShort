using Core.Application;
using Core.Application.DTOs;
using Core.Application.Ports.In;
using Core.Application.Ports.Out;
using Core.Application.Services;
using Core.Domain.Exceptions;
using Adapters.Out.Persistence.InMemory;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton(new ShortUrlOptions
{
    BaseUrl = builder.Configuration["Shortener:BaseUrl"] ?? "http://localhost:5000",
    MinTtlMinutes = 1,
    MaxTtlDays = 365,
    CodeLength = 7
});

builder.Services.AddSingleton<IClock>(new SystemClock());
builder.Services.AddSingleton<ICodeGenerator, Base62CodeGenerator>();
builder.Services.AddSingleton<IShortUrlRepository, InMemoryShortUrlRepository>();
builder.Services.AddSingleton<ICreateShortUrl, CreateShortUrlService>();

var app = builder.Build();

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

app.Run();

public partial class Program { }

sealed class SystemClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
