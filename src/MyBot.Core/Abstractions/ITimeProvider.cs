namespace MyBot.Core.Abstractions;

public interface ITimeProvider
{
    DateTime UtcNow { get; }
    Task DelayAsync(int milliseconds, CancellationToken cancellationToken = default);
}
