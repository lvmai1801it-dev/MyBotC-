using System;
using System.Threading;
using System.Threading.Tasks;
using MyBot.Core.Abstractions;

namespace MyBot.Core.AntiBan;

/// <summary>
/// Lên lịch nghỉ giải lao ngẫu nhiên sau một số trận đánh nhất định,
/// mô phỏng thói quen nghỉ ngơi của người chơi thật.
/// </summary>
public class MicroBreakScheduler
{
    private readonly ITimeProvider _time;
    private readonly IBotLogger _logger;
    private readonly Random _rng;

    private readonly int _minRaids;
    private readonly int _maxRaids;
    private readonly int _minDurationMin;
    private readonly int _maxDurationMin;

    private int _raidCountSinceBreak;
    private int _nextBreakAtRaid;
    private bool _onBreak;
    private bool _isBreaking;

    /// <summary>Khởi tạo với cấu hình mặc định (nghỉ sau 15-25 trận, nghỉ 5-15 phút).</summary>
    /// <param name="time">Nguồn thời gian dùng cho việc chờ.</param>
    /// <param name="logger">Logger ghi nhận sự kiện nghỉ.</param>
    public MicroBreakScheduler(ITimeProvider time, IBotLogger logger)
        : this(time, logger, 15, 25, 5, 15, new Random()) { }

    /// <summary>Khởi tạo với cấu hình mặc định và seed cố định để tái lập lịch nghỉ.</summary>
    /// <param name="time">Nguồn thời gian dùng cho việc chờ.</param>
    /// <param name="logger">Logger ghi nhận sự kiện nghỉ.</param>
    /// <param name="seed">Seed khởi tạo bộ sinh số ngẫu nhiên.</param>
    public MicroBreakScheduler(ITimeProvider time, IBotLogger logger, int seed)
        : this(time, logger, 15, 25, 5, 15, new Random(seed)) { }

    /// <summary>Khởi tạo với cấu hình số trận và thời lượng nghỉ tuỳ chỉnh, dùng seed cố định.</summary>
    /// <param name="time">Nguồn thời gian dùng cho việc chờ.</param>
    /// <param name="logger">Logger ghi nhận sự kiện nghỉ.</param>
    /// <param name="minRaids">Số trận tối thiểu trước khi nghỉ.</param>
    /// <param name="maxRaids">Số trận tối đa trước khi nghỉ.</param>
    /// <param name="minDurationMin">Thời lượng nghỉ tối thiểu (phút).</param>
    /// <param name="maxDurationMin">Thời lượng nghỉ tối đa (phút).</param>
    /// <param name="seed">Seed khởi tạo bộ sinh số ngẫu nhiên.</param>
    public MicroBreakScheduler(ITimeProvider time, IBotLogger logger, int minRaids, int maxRaids, int minDurationMin, int maxDurationMin, int seed)
        : this(time, logger, minRaids, maxRaids, minDurationMin, maxDurationMin, new Random(seed)) { }

    /// <summary>Khởi tạo đầy đủ với cấu hình tuỳ chỉnh và nguồn ngẫu nhiên do caller cung cấp.</summary>
    /// <param name="time">Nguồn thời gian dùng cho việc chờ.</param>
    /// <param name="logger">Logger ghi nhận sự kiện nghỉ.</param>
    /// <param name="minRaids">Số trận tối thiểu trước khi nghỉ.</param>
    /// <param name="maxRaids">Số trận tối đa trước khi nghỉ.</param>
    /// <param name="minDurationMin">Thời lượng nghỉ tối thiểu (phút).</param>
    /// <param name="maxDurationMin">Thời lượng nghỉ tối đa (phút).</param>
    /// <param name="rng">Bộ sinh số ngẫu nhiên.</param>
    public MicroBreakScheduler(ITimeProvider time, IBotLogger logger, int minRaids, int maxRaids, int minDurationMin, int maxDurationMin, Random rng)
    {
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));

        if (minRaids < 1 || maxRaids < minRaids)
        {
            throw new ArgumentException("raids range", nameof(minRaids));
        }

        if (minDurationMin < 1 || maxDurationMin < minDurationMin)
        {
            throw new ArgumentException("duration range", nameof(minDurationMin));
        }

        _minRaids = minRaids;
        _maxRaids = maxRaids;
        _minDurationMin = minDurationMin;
        _maxDurationMin = maxDurationMin;

        _raidCountSinceBreak = 0;
        _onBreak = false;
        _nextBreakAtRaid = _rng.Next(_minRaids, _maxRaids + 1);
    }

    /// <summary>Số trận đã đánh kể từ lần nghỉ gần nhất.</summary>
    public int RaidCountSinceBreak => _raidCountSinceBreak;

    /// <summary>Số trận mà tại đó lần nghỉ tiếp theo sẽ được kích hoạt.</summary>
    public int NextBreakAtRaid => _nextBreakAtRaid;

    /// <summary>Cho biết bot có đang trong trạng thái nghỉ hay không.</summary>
    public bool IsOnBreak => _onBreak;

    /// <summary>Kiểm tra xem đã đến lúc nghỉ giải lao hay chưa.</summary>
    /// <returns>True nếu đang nghỉ hoặc đã đạt số trận ngưỡng.</returns>
    public bool ShouldBreak() => _onBreak || _raidCountSinceBreak >= _nextBreakAtRaid;

    /// <summary>Ghi nhận đã hoàn thành thêm một trận đánh.</summary>
    public void RecordRaid()
    {
        _raidCountSinceBreak++;
        if (_raidCountSinceBreak >= _nextBreakAtRaid)
        {
            _onBreak = true;
            _logger.Info($"Raid #{_raidCountSinceBreak} recorded, break pending");
        }
    }

    /// <summary>
    /// Thực hiện nghỉ giải lao trong khoảng thời lượng ngẫu nhiên đã cấu hình,
    /// sau đó đặt lại bộ đếm trận và lên lịch nghỉ kế tiếp.
    /// </summary>
    /// <param name="ct">Token huỷ tác vụ.</param>
    /// <returns>Task hoàn tất khi kết thúc thời gian nghỉ.</returns>
    /// <exception cref="InvalidOperationException">Ném ra nếu chưa đến lúc nghỉ.</exception>
    public async Task TakeBreakAsync(CancellationToken ct = default)
    {
        if (!ShouldBreak())
        {
            throw new InvalidOperationException("No break is scheduled or pending");
        }

        if (_isBreaking)
        {
            return;
        }

        _isBreaking = true;
        try
        {
            int durationMin = _rng.Next(_minDurationMin, _maxDurationMin + 1);
            _logger.Info($"MicroBreak start ({_raidCountSinceBreak} raids, resting {durationMin} min)");
            await _time.DelayAsync(durationMin * 60_000, ct);

            _raidCountSinceBreak = 0;
            _onBreak = false;
            _nextBreakAtRaid = _rng.Next(_minRaids, _maxRaids + 1);
        }
        finally
        {
            _isBreaking = false;
        }
    }
}
