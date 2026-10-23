using System.Linq;
using System.Threading.Tasks;
using MyBot.Core.FSM.Maintenance;
using MyBot.UI.BotRuntime;
using MyBot.UI.BotRuntime.Steps;
using Xunit;

namespace MyBot.UI.Tests.BotRuntime.Steps;

public sealed class IdleStepTests
{
    [Fact]
    public async Task ExecuteAsync_SameSeed_ProducesSameOrder()
    {
        var timeA = new FakeTimeProvider();
        var loggerA = new FakeLogger();
        BotLoopContext ctxA = BotLoopTestKit.Context(
            timeA, loggerA, new MaintenanceScheduler(timeA, loggerA, 42), _ => Task.FromResult(BotLoopTestKit.Frame()));

        var timeB = new FakeTimeProvider();
        var loggerB = new FakeLogger();
        BotLoopContext ctxB = BotLoopTestKit.Context(
            timeB, loggerB, new MaintenanceScheduler(timeB, loggerB, 42), _ => Task.FromResult(BotLoopTestKit.Frame()));

        await new IdleStep(loggerA, 7).ExecuteAsync(ctxA);
        await new IdleStep(loggerB, 7).ExecuteAsync(ctxB);

        Assert.Equal(loggerA.Debugs[0], loggerB.Debugs[0]);
    }

    [Fact]
    public async Task ExecuteAsync_RunsAllFiveActions_WithFourStubs()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        BotLoopContext ctx = BotLoopTestKit.Context(
            time, logger, new MaintenanceScheduler(time, logger, 42), _ => Task.FromResult(BotLoopTestKit.Frame()));

        bool ok = await new IdleStep(logger, 7).ExecuteAsync(ctx);

        Assert.True(ok);
        string order = Assert.Single(logger.Debugs);
        Assert.Contains("Collect", order, System.StringComparison.Ordinal);
        Assert.Contains("CheckTombs", order, System.StringComparison.Ordinal);
        Assert.Contains("RequestCC", order, System.StringComparison.Ordinal);
        Assert.Contains("DonateCC", order, System.StringComparison.Ordinal);
        Assert.Contains("CleanYard", order, System.StringComparison.Ordinal);
        Assert.Equal(4, logger.Infos.Count(m => m.StartsWith("[STUB]", System.StringComparison.Ordinal)));
        Assert.Single(logger.Warnings);
    }

    [Fact]
    public async Task ExecuteAsync_RunStateFalse_ReturnsFalseWithoutActions()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        BotLoopContext ctx = BotLoopTestKit.Context(
            time, logger, new MaintenanceScheduler(time, logger, 42), _ => Task.FromResult(BotLoopTestKit.Frame()));
        ctx.RunState = false;

        bool ok = await new IdleStep(logger, 7).ExecuteAsync(ctx);

        Assert.False(ok);
        Assert.Empty(logger.Infos);
        Assert.Empty(logger.Warnings);
    }
}
