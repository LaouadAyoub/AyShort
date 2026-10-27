using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

public class OperationalEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OperationalEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

    [Theory]
    [InlineData("Development", "{")]
    [InlineData("Production", "{")]
    [InlineData("Production", "")]
    public async Task Invalid_json_returns_400_problem_details(string environment, string body)
    {
        using var factory = _factory.WithWebHostBuilder(builder => builder.UseEnvironment(environment));
        using var client = factory.CreateClient();
        using var response = await client.PostAsync("/links", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(400, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("/links", problem.RootElement.GetProperty("instance").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("traceId").GetString()));
        Assert.DoesNotContain("JsonException", problem.RootElement.GetProperty("detail").GetString());
    }

    [Theory]
    [InlineData(false, HttpStatusCode.OK, "Up")]
    [InlineData(true, HttpStatusCode.ServiceUnavailable, "Down")]
    public async Task Readiness_bounds_the_wait_for_redis(bool stalled, HttpStatusCode status, string redisStatus)
    {
        var ping = new TaskCompletionSource<TimeSpan>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!stalled)
            ping.SetResult(TimeSpan.Zero);

        var database = DispatchProxy.Create<IDatabase, RedisProxy>();
        ((RedisProxy)(object)database).Handle = (_, _) => ping.Task;
        var connection = DispatchProxy.Create<IConnectionMultiplexer, RedisProxy>();
        ((RedisProxy)(object)connection).Handle = (method, _) => method.Name switch
        {
            nameof(IConnectionMultiplexer.GetDatabase) => database,
            nameof(IDisposable.Dispose) => null,
            _ => throw new NotSupportedException(method.Name)
        };

        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IConnectionMultiplexer>();
            services.AddSingleton(connection);
        }));
        using var client = factory.CreateClient();
        // Start the host before measuring the request deadline.
        using var live = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);

        try
        {
            using var response = await client.GetAsync("/health/ready").WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(status, response.StatusCode);
            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("Up", payload.RootElement.GetProperty("dependencies").GetProperty("database").GetString());
            Assert.Equal(redisStatus, payload.RootElement.GetProperty("dependencies").GetProperty("redis").GetString());
        }
        finally
        {
            // Let an unfinished ping complete even when the regression test fails.
            ping.TrySetResult(TimeSpan.Zero);
        }
    }

    public class RedisProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Handle { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handle(targetMethod!, args);
    }
}
