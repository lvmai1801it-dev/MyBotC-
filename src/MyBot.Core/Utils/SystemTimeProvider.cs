using MyBot.Core.Abstractions;

namespace MyBot.Core.Utils;

public class SystemTimeProvider : ITimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
    
    public Task DelayAsync(int milliseconds, CancellationToken cancellationToken = default)
        => Task.Delay(milliseconds, cancellationToken);
}
