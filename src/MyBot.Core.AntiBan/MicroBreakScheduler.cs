using System;
using System.Threading;
using System.Threading.Tasks;
using MyBot.Core.Abstractions;

namespace MyBot.Core.AntiBan;

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

    public MicroBreakScheduler(ITimeProvider time, IBotLogger logger)
        : this(time, logger, 15, 25, 5, 15, new Random()) { }

    public MicroBreakScheduler(ITimeProvider time, IBotLogger logger, int seed)
        : this(time, logger, 15, 25, 5, 15, new Random(seed)) { }

    public MicroBreakScheduler(ITimeProvider time, IBotLogger logger, int minRaids, int maxRaids, int minDurationMin, int maxDurationMin, int seed)
        : this(time, logger, minRaids, maxRaids, minDurationMin, maxDurationMin, new Random(seed)) { }

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

    public int RaidCountSinceBreak => _raidCountSinceBreak;
    public int NextBreakAtRaid => _nextBreakAtRaid;
    public bool IsOnBreak => _onBreak;

    public bool ShouldBreak() => _onBreak || _raidCountSinceBreak >= _nextBreakAtRaid;

    public void RecordRaid()
    {
        _raidCountSinceBreak++;
        if (_raidCountSinceBreak >= _nextBreakAtRaid)
        {
            _onBreak = true;
            _logger.Info($"Raid #{_raidCountSinceBreak} recorded, break pending");
        }
    }

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
