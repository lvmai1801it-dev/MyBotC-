using System;
using MyBot.Core.Abstractions;

namespace MyBot.Core.Tests.Mocks;

/// <summary>
/// Nguồn thời gian giả: mỗi lần delay sẽ cộng dồn <see cref="UtcNow"/> để test
/// các ngưỡng timeout mà không phải chờ thật.
/// </summary>
public sealed class FakeTimeProvider : ITimeProvider
{
    /// <summary>Thời điểm bắt đầu cố định để test tất định.</summary>
    public DateTime UtcNow { get; private set; } = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>Cộng thời gian giả rồi trả về ngay.</summary>
    /// <param name="milliseconds">Số mili-giây cần cộng.</param>
    /// <param name="cancellationToken">Token huỷ.</param>
    /// <returns>Task đã hoàn thành.</returns>
    public Task DelayAsync(int milliseconds, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        UtcNow = UtcNow.AddMilliseconds(milliseconds);
        return Task.CompletedTask;
    }
}
