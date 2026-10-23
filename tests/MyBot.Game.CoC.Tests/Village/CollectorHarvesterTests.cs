using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyBot.Core.Abstractions;
using MyBot.Core.AntiBan;
using MyBot.Game.CoC.Village;
using OpenCvSharp;

namespace MyBot.Game.CoC.Tests.Village;

public class CollectorHarvesterTests
{
    private static CollectorHarvester CreateHarvester(
        ITemplateMatcher matcher,
        FakeInput input,
        FakeTimeProvider time,
        Mat? gold = null,
        Mat? elixir = null,
        Mat? dark = null,
        Mat? storage = null)
        => new(matcher, input, new HumanDelay(time, seed: 42), gold, elixir, dark, storage);

    private static Mat Frame()
        => new(400, 400, MatType.CV_8UC3, Scalar.All(0));

    private static Mat Template()
        => new(4, 4, MatType.CV_8UC1);

    [Fact]
    public async Task EmptyFrame_TappedBangKhong_KhongNem()
    {
        using var frame = new Mat();
        using var gold = Template();
        var matcher = new FakeMatcher(Array.Empty<MatchResult>());
        var input = new FakeInput();
        var time = new FakeTimeProvider();
        CollectorHarvester harvester = CreateHarvester(matcher, input, time, gold);

        CollectorResult result = await harvester.HarvestAllAsync(frame, matcher, input);

        Assert.Equal(0, result.Tapped);
        Assert.Empty(input.Taps);
    }

    [Fact]
    public async Task MotMo_TapDungTam()
    {
        using var frame = Frame();
        using var gold = Template();
        var matcher = new FakeMatcher(new[] { new MatchResult(100, 100, 20, 20, 0.9) });
        var input = new FakeInput();
        var time = new FakeTimeProvider();
        CollectorHarvester harvester = CreateHarvester(matcher, input, time, gold);

        CollectorResult result = await harvester.HarvestAllAsync(frame, matcher, input);

        Assert.Equal(1, result.Tapped);
        Assert.Equal(0, result.SkippedFull);
        Assert.Equal(0, result.SkippedDuplicate);
        Assert.Equal((110, 110), Assert.Single(input.Taps));
        Assert.Equal(0.6, matcher.Thresholds[0], 6);
        Assert.Contains(800, time.Delays);
    }

    [Fact]
    public async Task BaMoCungLoai_HaiMoGanNhau_DedupeMot()
    {
        using var frame = Frame();
        using var gold = Template();
        var matcher = new FakeMatcher(new[]
        {
            new MatchResult(100, 100, 20, 20, 0.9),
            new MatchResult(105, 102, 20, 20, 0.9),
            new MatchResult(300, 300, 20, 20, 0.9)
        });
        var input = new FakeInput();
        var time = new FakeTimeProvider();
        CollectorHarvester harvester = CreateHarvester(matcher, input, time, gold);

        CollectorResult result = await harvester.HarvestAllAsync(frame, matcher, input);

        Assert.Equal(2, result.Tapped);
        Assert.Equal(1, result.SkippedDuplicate);
        Assert.Equal(new[] { (110, 110), (310, 310) }, input.Taps);
    }

    [Fact]
    public async Task KhoDay_KhongTap_TangSkippedFull()
    {
        using var frame = Frame();
        using var gold = Template();
        using var storage = Template();
        var matcher = new FakeMatcher(new[]
        {
            new MatchResult(100, 100, 20, 20, 0.9),
            new MatchResult(300, 300, 20, 20, 0.9)
        })
        {
            FindResult = new MatchResult(0, 0, 10, 10, 0.99)
        };
        var input = new FakeInput();
        var time = new FakeTimeProvider();
        CollectorHarvester harvester = CreateHarvester(matcher, input, time, gold, storage: storage);

        CollectorResult result = await harvester.HarvestAllAsync(frame, matcher, input);

        Assert.Equal(0, result.Tapped);
        Assert.Equal(2, result.SkippedFull);
        Assert.Empty(input.Taps);
    }

    [Fact]
    public async Task NhieuLoaiMo_TapTheoThuTuVangElixirDark()
    {
        using var frame = Frame();
        using var gold = Template();
        using var elixir = Template();
        using var dark = Template();
        var matcher = new FakeMatcher(
            new[] { new MatchResult(10, 10, 4, 4, 0.9) },
            new[] { new MatchResult(100, 100, 4, 4, 0.9) },
            new[] { new MatchResult(200, 200, 4, 4, 0.9) });
        var input = new FakeInput();
        var time = new FakeTimeProvider();
        CollectorHarvester harvester = CreateHarvester(matcher, input, time, gold, elixir, dark);

        CollectorResult result = await harvester.HarvestAllAsync(frame, matcher, input);

        Assert.Equal(3, result.Tapped);
        Assert.Equal(new[] { (12, 12), (102, 102), (202, 202) }, input.Taps);
    }

    [Fact]
    public async Task HuyGiuaCycle_DungKhongLeak()
    {
        using var frame = Frame();
        using var gold = Template();
        var matcher = new FakeMatcher(new[]
        {
            new MatchResult(10, 10, 4, 4, 0.9),
            new MatchResult(100, 100, 4, 4, 0.9),
            new MatchResult(200, 200, 4, 4, 0.9)
        });
        var input = new FakeInput();
        var time = new FakeTimeProvider();
        CollectorHarvester harvester = CreateHarvester(matcher, input, time, gold);
        using var cts = new CancellationTokenSource();
        input.OnTap = (_, _) => cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => harvester.HarvestAllAsync(frame, matcher, input, cts.Token));

        Assert.Single(input.Taps);
    }

    [Fact]
    public async Task HaiLanLienTiep_DeuChoKetQuaGiongNhau()
    {
        using var frame = Frame();
        using var gold = Template();
        var input = new FakeInput();
        var time = new FakeTimeProvider();
        CollectorHarvester harvester = CreateHarvester(matcher: new FakeMatcher(), input, time, gold);

        var matcher1 = new FakeMatcher(new[] { new MatchResult(100, 100, 20, 20, 0.9) });
        CollectorResult first = await harvester.HarvestAllAsync(frame, matcher1, input);

        var matcher2 = new FakeMatcher(new[] { new MatchResult(100, 100, 20, 20, 0.9) });
        CollectorResult second = await harvester.HarvestAllAsync(frame, matcher2, input);

        Assert.Equal(first.Tapped, second.Tapped);
        Assert.Equal(first.SkippedDuplicate, second.SkippedDuplicate);
    }

    private sealed class FakeTimeProvider : ITimeProvider
    {
        public DateTime UtcNow { get; private set; } = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public List<int> Delays { get; } = new();

        public Task DelayAsync(int milliseconds, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Delays.Add(milliseconds);
            UtcNow = UtcNow.AddMilliseconds(milliseconds);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeMatcher : ITemplateMatcher
    {
        private readonly Queue<MatchResult[]> _batches;

        public FakeMatcher(params MatchResult[][] batches)
        {
            _batches = new Queue<MatchResult[]>(batches);
        }

        public MatchResult? FindResult { get; set; }

        public List<double> Thresholds { get; } = new();

        public MatchResult? Find(Mat haystack, Mat needle, double threshold = 0.8)
        {
            Thresholds.Add(threshold);
            return FindResult;
        }

        public IEnumerable<MatchResult> FindAll(Mat haystack, Mat needle, double threshold = 0.8)
        {
            Thresholds.Add(threshold);
            return _batches.Count > 0 ? _batches.Dequeue() : Array.Empty<MatchResult>();
        }
    }

    private sealed class FakeInput : IInputBackend
    {
        public List<(int X, int Y)> Taps { get; } = new();

        public Action<int, int>? OnTap { get; set; }

        public bool Initialize(EmulatorTarget target) => true;

        public void Tap(int x, int y)
        {
            Taps.Add((x, y));
            OnTap?.Invoke(x, y);
        }

        public void Swipe(int x1, int y1, int x2, int y2, int durationMs)
        {
        }

        public bool Scroll(int x, int y, int delta) => false;

        public void Dispose()
        {
        }
    }
}
