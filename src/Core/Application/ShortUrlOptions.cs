namespace Core.Application;

public sealed class ShortUrlOptions
{
    public string BaseUrl { get; init; } = "http://localhost:5142";
    public int MinTtlMinutes { get; init; } = 1;
    public int MaxTtlDays { get; init; } = 365;
    public int CodeLength { get; init; } = 7;
    public int NegativeCacheTtlSeconds { get; init; } = 60; // 1 minute for negative cache entries
}
