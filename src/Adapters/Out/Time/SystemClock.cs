using Core.Application.Ports.Out;

namespace Adapters.Out.Time;

public sealed class SystemClock : IClock
{
	public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
