namespace Core.Application.DTOs;

public sealed record GetStatsRequest(string Code);
public sealed record GetStatsResult(
    DateTimeOffset CreatedAt, 
    int Clicks, 
    DateTimeOffset? LastAccess, 
    DateTimeOffset? Expiration);