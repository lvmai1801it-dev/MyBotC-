using System.Threading;
using System.Threading.Tasks;
using MyBot.Core.Abstractions;
using MyBot.Core.AntiBan;
using MyBot.Core.FSM.Maintenance;
using MyBot.Game.CoC.Village;
using MyBot.UI.BotRuntime;
using MyBot.UI.BotRuntime.Steps;
using OpenCvSharp;
using Xunit;

namespace MyBot.UI.Tests.BotRuntime.Steps;

public sealed class CollectStepTests
{
    [Fact]
    public async Task ExecuteAsync_MissingCollector_LogsWarningAndReturnsTrue()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        BotLoopContext ctx = BotLoopTestKit.Context(
            time, logger, new MaintenanceScheduler(time, logger, 42), _ => Task.FromResult(BotLoopTestKit.Frame()));

        bool ok = await new CollectStep(logger).ExecuteAsync(ctx);

        Assert.True(ok);
        Assert.Contains("[Collect] Thiếu template mỏ hoặc input — bỏ qua thu hoạch.", logger.Warnings);
    }

    [Fact]
    public async Task ExecuteAsync_MissingInput_LogsWarningAndReturnsTrue()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        var matcher = new FakeMatcher();
        using var gold = new Mat(4, 4, MatType.CV_8UC1, Scalar.All(0));
        BotLoopContext ctx = BotLoopTestKit.Context(
            time,
            logger,
            new MaintenanceScheduler(time, logger, 42),
            _ => Task.FromResult(BotLoopTestKit.Frame()),
            matcher: matcher,
            collector: new CollectorHarvester(matcher, new FakeInput(), new HumanDelay(time, 42), gold, null, null, null));

        bool ok = await new CollectStep(logger).ExecuteAsync(ctx);

        Assert.True(ok);
        Assert.Contains("[Collect] Thiếu template mỏ hoặc input — bỏ qua thu hoạch.", logger.Warnings);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyFrame_LogsWarningAndReturnsTrue()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        var matcher = new FakeMatcher();
        var input = new FakeInput();
        using var gold = new Mat(4, 4, MatType.CV_8UC1, Scalar.All(0));
        BotLoopContext ctx = BotLoopTestKit.Context(
            time,
            logger,
            new MaintenanceScheduler(time, logger, 42),
            _ => Task.FromResult(new Mat()),
            matcher: matcher,
            input: input,
            collector: new CollectorHarvester(matcher, input, new HumanDelay(time, 42), gold, null, null, null));

        bool ok = await new CollectStep(logger).ExecuteAsync(ctx);

        Assert.True(ok);
        Assert.Contains("[Collect] Không chụp được khung hình — bỏ qua thu hoạch.", logger.Warnings);
        Assert.Empty(input.Taps);
    }

    [Fact]
    public async Task ExecuteAsync_WithCollector_TapsAndLogsResult()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        var matcher = new FakeMatcher(new[] { new MatchResult(100, 100, 20, 20, 0.9) });
        var input = new FakeInput();
        using var gold = new Mat(4, 4, MatType.CV_8UC1, Scalar.All(0));
        BotLoopContext ctx = BotLoopTestKit.Context(
            time,
            logger,
            new MaintenanceScheduler(time, logger, 42),
            _ => Task.FromResult(BotLoopTestKit.Frame()),
            matcher: matcher,
            input: input,
            collector: new CollectorHarvester(matcher, input, new HumanDelay(time, 42), gold, null, null, null));

        bool ok = await new CollectStep(logger).ExecuteAsync(ctx);

        Assert.True(ok);
        Assert.Single(input.Taps);
        Assert.Contains(logger.Infos, m => m.Contains("[Collect] Tap 1 mỏ", System.StringComparison.Ordinal));
    }
}
