namespace Core.Application.Ports.Out;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
