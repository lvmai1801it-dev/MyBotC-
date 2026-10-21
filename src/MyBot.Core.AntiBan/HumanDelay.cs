using System;
using System.Threading;
using System.Threading.Tasks;
using MyBot.Core.Abstractions;

namespace MyBot.Core.AntiBan;

/// <summary>
/// Engine tạo độ trễ ngẫu nhiên theo phân phối chuẩn Gaussian (Box-Muller transform)
/// mô phỏng hành vi bấm chuột / chạm màn hình của con người nhằm chống ban.
/// </summary>
public class HumanDelay
{
    public static readonly (int mean, int std, int min, int max) DropTroopTiming = (80, 15, 50, 130);
    public static readonly (int mean, int std, int min, int max) NextButtonTiming = (1800, 350, 1100, 3200);

    private readonly ITimeProvider _time;
    private readonly Random _rng;

    public HumanDelay(ITimeProvider time, Random rng)
    {
        _time = time;
        _rng = rng;
    }

    public HumanDelay(ITimeProvider time) : this(time, new Random()) { }

    public HumanDelay(ITimeProvider time, int seed) : this(time, new Random(seed)) { }

    public int NextDelay((int mean, int std, int min, int max) timing)
        => NextDelay(timing.mean, timing.std, timing.min, timing.max);

    /// <summary>
    /// Sinh độ trễ (ms) tiếp theo dựa trên phân phối Gaussian đã được clamp trong khoảng [minMs, maxMs].
    /// </summary>
    public int NextDelay(int meanMs, int stdMs, int minMs, int maxMs)
    {
        if (minMs > maxMs)
        {
            throw new ArgumentException($"minMs ({minMs}) không được lớn hơn maxMs ({maxMs})");
        }

        if (meanMs < minMs)
        {
            throw new ArgumentException($"meanMs ({meanMs}) không được nhỏ hơn minMs ({minMs})");
        }

        if (meanMs > maxMs)
        {
            throw new ArgumentException($"meanMs ({meanMs}) không được lớn hơn maxMs ({maxMs})");
        }

        if (minMs == maxMs)
        {
            return minMs;
        }

        if (stdMs == 0)
        {
            return Math.Max(minMs, Math.Min(maxMs, meanMs));
        }

        // Box-Muller transform: lấy 2 số ngẫu nhiên đều u1, u2 trong (0, 1)
        double u1 = _rng.NextDouble();
        double u2 = _rng.NextDouble();

        // Chống u1 == 0 dẫn tới ln(0) = -inf
        if (u1 <= 0.0)
        {
            u1 = double.Epsilon;
        }

        double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        double value = meanMs + stdMs * z;

        return Math.Max(minMs, Math.Min(maxMs, (int)Math.Round(value)));
    }

    public Task DelayAsync((int mean, int std, int min, int max) timing, CancellationToken ct = default)
        => DelayAsync(timing.mean, timing.std, timing.min, timing.max, ct);

    /// <summary>
    /// Đợi bất đồng bộ một khoảng thời gian ngẫu nhiên theo phân phối Gaussian thông qua ITimeProvider.
    /// </summary>
    public async Task DelayAsync(int meanMs, int stdMs, int minMs, int maxMs, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        int ms = NextDelay(meanMs, stdMs, minMs, maxMs);
        await _time.DelayAsync(ms, ct);
    }
}
