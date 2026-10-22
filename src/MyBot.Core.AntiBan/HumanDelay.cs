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
    /// <summary>Bộ tham số thời gian (mean, std, min, max) dùng khi thả quân.</summary>
    public static readonly (int mean, int std, int min, int max) DropTroopTiming = (80, 15, 50, 130);

    /// <summary>Bộ tham số thời gian (mean, std, min, max) dùng khi bấm nút Next.</summary>
    public static readonly (int mean, int std, int min, int max) NextButtonTiming = (1800, 350, 1100, 3200);

    private readonly ITimeProvider _time;
    private readonly Random _rng;

    /// <summary>
    /// Khởi tạo bộ sinh độ trễ với nguồn ngẫu nhiên do caller cung cấp.
    /// </summary>
    /// <param name="time">Nguồn thời gian dùng cho việc chờ bất đồng bộ.</param>
    /// <param name="rng">Bộ sinh số ngẫu nhiên (cho phép tái lập hành vi).</param>
    public HumanDelay(ITimeProvider time, Random rng)
    {
        _time = time;
        _rng = rng;
    }

    /// <summary>Khởi tạo bộ sinh độ trễ với nguồn ngẫu nhiên mặc định.</summary>
    /// <param name="time">Nguồn thời gian dùng cho việc chờ bất đồng bộ.</param>
    public HumanDelay(ITimeProvider time) : this(time, new Random()) { }

    /// <summary>Khởi tạo bộ sinh độ trễ với seed cố định để tái lập chuỗi ngẫu nhiên.</summary>
    /// <param name="time">Nguồn thời gian dùng cho việc chờ bất đồng bộ.</param>
    /// <param name="seed">Seed khởi tạo bộ sinh số ngẫu nhiên.</param>
    public HumanDelay(ITimeProvider time, int seed) : this(time, new Random(seed)) { }

    /// <summary>Sinh độ trễ (ms) tiếp theo từ một bộ tham số timing có sẵn.</summary>
    /// <param name="timing">Bộ tham số (mean, std, min, max) tính bằng mili-giây.</param>
    /// <returns>Độ trễ tính bằng mili-giây.</returns>
    public int NextDelay((int mean, int std, int min, int max) timing)
        => NextDelay(timing.mean, timing.std, timing.min, timing.max);

    /// <summary>
    /// Sinh độ trễ (ms) tiếp theo dựa trên phân phối Gaussian đã được clamp trong khoảng [minMs, maxMs].
    /// </summary>
    /// <param name="meanMs">Giá trị trung bình (ms).</param>
    /// <param name="stdMs">Độ lệch chuẩn (ms).</param>
    /// <param name="minMs">Giá trị nhỏ nhất (ms).</param>
    /// <param name="maxMs">Giá trị lớn nhất (ms).</param>
    /// <returns>Độ trễ tính bằng mili-giây.</returns>
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

    /// <summary>Đợi bất đồng bộ một khoảng thời gian ngẫu nhiên từ bộ tham số timing có sẵn.</summary>
    /// <param name="timing">Bộ tham số (mean, std, min, max) tính bằng mili-giây.</param>
    /// <param name="ct">Token hủy tác vụ.</param>
    /// <returns>Task hoàn tất sau khi hết thời gian chờ.</returns>
    public Task DelayAsync((int mean, int std, int min, int max) timing, CancellationToken ct = default)
        => DelayAsync(timing.mean, timing.std, timing.min, timing.max, ct);

    /// <summary>
    /// Đợi bất đồng bộ một khoảng thời gian ngẫu nhiên theo phân phối Gaussian thông qua ITimeProvider.
    /// </summary>
    /// <param name="meanMs">Giá trị trung bình (ms).</param>
    /// <param name="stdMs">Độ lệch chuẩn (ms).</param>
    /// <param name="minMs">Giá trị nhỏ nhất (ms).</param>
    /// <param name="maxMs">Giá trị lớn nhất (ms).</param>
    /// <param name="ct">Token hủy tác vụ.</param>
    /// <returns>Task hoàn tất sau khi hết thời gian chờ.</returns>
    public async Task DelayAsync(int meanMs, int stdMs, int minMs, int maxMs, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        int ms = NextDelay(meanMs, stdMs, minMs, maxMs);
        await _time.DelayAsync(ms, ct);
    }
}
