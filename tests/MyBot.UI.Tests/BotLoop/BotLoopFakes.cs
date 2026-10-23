using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyBot.Core.Abstractions;
using MyBot.Core.FSM.Maintenance;
using MyBot.Game.CoC.Village;
using MyBot.UI.BotRuntime;
using OpenCvSharp;

namespace MyBot.UI.Tests.BotRuntime;

/// <summary>Double dùng chung cho test BotLoop: thời gian, logger, matcher, input và dựng context.</summary>
internal static class BotLoopTestKit
{
    public static Mat Frame() => new(400, 400, MatType.CV_8UC3, Scalar.All(0));

    public static BotLoopContext Context(
        FakeTimeProvider time,
        FakeLogger logger,
        MaintenanceScheduler scheduler,
        Func<CancellationToken, Task<Mat>> grabFrame,
        ITemplateMatcher? matcher = null,
        Func<Mat, bool>? isMainScreen = null,
        IInputBackend? input = null,
        CollectorHarvester? collector = null,
        Func<Mat, CancellationToken, Task>? updateResources = null,
        Func<Mat, CancellationToken, Task<bool>>? ensureMainScreen = null,
        Func<CancellationToken, Task<bool>>? onMainScreenSeen = null,
        TimeSpan? resourceReadInterval = null,
        int? seed = null,
        bool isSearchAttackEnabled = true)
        => new()
        {
            Time = time,
            Logger = logger,
            Scheduler = scheduler,
            Matcher = matcher ?? new FakeMatcher(),
            Input = input,
            Collector = collector,
            GrabFrameAsync = grabFrame,
            IsMainScreen = isMainScreen ?? (_ => true),
            EnsureMainScreenAsync = ensureMainScreen,
            OnMainScreenSeenAsync = onMainScreenSeen,
            UpdateResourcesAsync = updateResources,
            ResourceReadInterval = resourceReadInterval ?? TimeSpan.FromSeconds(2),
            Seed = seed,
            IsSearchAttackEnabled = isSearchAttackEnabled,
        };
}

internal sealed class FakeTimeProvider : ITimeProvider
{
    public DateTime UtcNow { get; private set; } = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public List<int> Delays { get; } = new();

    /// <summary>Hook chạy trước khi delay hoàn tất (dùng để huỷ token giữa delay).</summary>
    public Action<int>? BeforeDelay { get; set; }

    public Task DelayAsync(int milliseconds, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        BeforeDelay?.Invoke(milliseconds);
        cancellationToken.ThrowIfCancellationRequested();
        Delays.Add(milliseconds);
        UtcNow = UtcNow.AddMilliseconds(milliseconds);
        return Task.CompletedTask;
    }
}

internal sealed class FakeLogger : IBotLogger
{
    public List<string> Debugs { get; } = new();

    public List<string> Infos { get; } = new();

    public List<string> Warnings { get; } = new();

    public List<string> Errors { get; } = new();

    public void Debug(string message) => Debugs.Add(message);

    public void Info(string message) => Infos.Add(message);

    public void Warning(string message) => Warnings.Add(message);

    public void Error(string message, Exception? ex = null)
        => Errors.Add(ex is null ? message : message + " | " + ex.Message);
}

internal sealed class FakeMatcher : ITemplateMatcher
{
    private readonly Queue<MatchResult[]> _batches;

    public FakeMatcher(params MatchResult[][] batches)
    {
        _batches = new Queue<MatchResult[]>(batches);
    }

    public MatchResult? Find(Mat haystack, Mat needle, double threshold = 0.8) => null;

    public IEnumerable<MatchResult> FindAll(Mat haystack, Mat needle, double threshold = 0.8)
        => _batches.Count > 0 ? _batches.Dequeue() : Array.Empty<MatchResult>();
}

internal sealed class FakeInput : IInputBackend
{
    public List<(int X, int Y)> Taps { get; } = new();

    public bool Initialize(EmulatorTarget target) => true;

    public void Tap(int x, int y) => Taps.Add((x, y));

    public void Swipe(int x1, int y1, int x2, int y2, int durationMs)
    {
    }

    public bool Scroll(int x, int y, int delta) => false;

    public void Dispose()
    {
    }
}
