using System.Threading.Tasks;
using MyBot.Core.FSM.Maintenance;
using MyBot.UI.BotRuntime;
using MyBot.UI.BotRuntime.Steps;
using Xunit;

namespace MyBot.UI.Tests.BotRuntime.Steps;

public sealed class MaintenanceStepTests
{
    [Fact]
    public async Task RunGroupAsync_Collect_MarksRunAndLogsMissingTemplate()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        var scheduler = new MaintenanceScheduler(time, logger, 42);
        BotLoopContext ctx = BotLoopTestKit.Context(
            time, logger, scheduler, _ => Task.FromResult(BotLoopTestKit.Frame()));
        var step = new MaintenanceStep(scheduler, logger);

        bool ok = await step.RunGroupAsync(
            ctx, MaintenanceData.Collect, MaintenanceData.FastMin, MaintenanceData.FastMax);

        Assert.True(ok);
        Assert.False(scheduler.IsDue(MaintenanceAction.Collect, MaintenanceData.FastMin, MaintenanceData.FastMax));
        Assert.Contains(logger.Warnings, m => m.Contains("[Collect]", System.StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunGroupAsync_StubActions_LogAutoItNames()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        var scheduler = new MaintenanceScheduler(time, logger, 42);
        BotLoopContext ctx = BotLoopTestKit.Context(
            time, logger, scheduler, _ => Task.FromResult(BotLoopTestKit.Frame()));
        var step = new MaintenanceStep(scheduler, logger);
        MaintenanceAction[] group = { MaintenanceAction.CheckTombs, MaintenanceAction.CleanYard };

        bool ok = await step.RunGroupAsync(
            ctx, group, MaintenanceData.CleanupMin, MaintenanceData.CleanupMax);

        Assert.True(ok);
        Assert.Contains("[STUB] CheckTombs — M3/M4 chưa port", logger.Infos);
        Assert.Contains("[STUB] CleanYard — M3/M4 chưa port", logger.Infos);
        Assert.False(scheduler.IsDue(MaintenanceAction.CheckTombs, MaintenanceData.CleanupMin, MaintenanceData.CleanupMax));
    }

    [Fact]
    public async Task RunGroupAsync_CanContinueFalse_ReturnsFalseWithoutExecuting()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        var scheduler = new MaintenanceScheduler(time, logger, 42);
        BotLoopContext ctx = BotLoopTestKit.Context(
            time, logger, scheduler, _ => Task.FromResult(BotLoopTestKit.Frame()));
        ctx.RunState = false;
        var step = new MaintenanceStep(scheduler, logger);

        bool ok = await step.RunGroupAsync(
            ctx, MaintenanceData.Collect, MaintenanceData.FastMin, MaintenanceData.FastMax);

        Assert.False(ok);
        Assert.Empty(logger.Infos);
        Assert.Empty(logger.Warnings);
    }

    [Fact]
    public async Task RunActionAsync_NotDue_SkipsWithoutExecuting()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        var scheduler = new MaintenanceScheduler(time, logger, 42);
        scheduler.MarkRun(MaintenanceAction.Collect, MaintenanceData.FastMin, MaintenanceData.FastMax);
        BotLoopContext ctx = BotLoopTestKit.Context(
            time, logger, scheduler, _ => Task.FromResult(BotLoopTestKit.Frame()));
        var step = new MaintenanceStep(scheduler, logger);

        bool ok = await step.RunActionAsync(
            ctx, MaintenanceAction.Collect, MaintenanceData.FastMin, MaintenanceData.FastMax);

        Assert.True(ok);
        Assert.Empty(logger.Infos);
        Assert.Empty(logger.Warnings);
    }

    [Fact]
    public async Task RunActionAsync_Force_RunsEvenWhenNotDue()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        var scheduler = new MaintenanceScheduler(time, logger, 42);
        scheduler.MarkRun(MaintenanceAction.Collect, MaintenanceData.FastMin, MaintenanceData.FastMax);
        BotLoopContext ctx = BotLoopTestKit.Context(
            time, logger, scheduler, _ => Task.FromResult(BotLoopTestKit.Frame()));
        var step = new MaintenanceStep(scheduler, logger);

        bool ok = await step.RunActionAsync(
            ctx, MaintenanceAction.Collect, MaintenanceData.FastMin, MaintenanceData.FastMax, force: true);

        Assert.True(ok);
        Assert.Contains(logger.Warnings, m => m.Contains("[Collect]", System.StringComparison.Ordinal));
    }

    [Fact]
    public void IsDue_DelegatesToScheduler()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        var scheduler = new MaintenanceScheduler(time, logger, 42);
        BotLoopContext ctx = BotLoopTestKit.Context(
            time, logger, scheduler, _ => Task.FromResult(BotLoopTestKit.Frame()));
        var step = new MaintenanceStep(scheduler, logger);

        Assert.True(step.IsDue(MaintenanceData.Collect, MaintenanceData.FastMin, MaintenanceData.FastMax));

        scheduler.MarkRun(MaintenanceAction.Collect, MaintenanceData.FastMin, MaintenanceData.FastMax);
        scheduler.MarkRun(MaintenanceAction.CollectCCGold, MaintenanceData.FastMin, MaintenanceData.FastMax);

        Assert.False(step.IsDue(MaintenanceData.Collect, MaintenanceData.FastMin, MaintenanceData.FastMax));
    }
}
