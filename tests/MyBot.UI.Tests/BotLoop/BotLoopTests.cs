using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyBot.Core.Abstractions;
using MyBot.Core.AntiBan;
using MyBot.Core.FSM.Maintenance;
using MyBot.Game.CoC.Village;
using MyBot.UI.BotRuntime;
using OpenCvSharp;

namespace MyBot.UI.Tests.BotRuntime;

public sealed class BotLoopTests
{
    [Fact]
    public async Task RunAsync_RunStateFalse_DoesNothing()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        BotLoopContext ctx = BotLoopTestKit.Context(
            time, logger, new MaintenanceScheduler(time, logger, 42), _ => Task.FromResult(BotLoopTestKit.Frame()));
        ctx.RunState = false;

        await new BotLoop(ctx).RunAsync(CancellationToken.None);

        Assert.Empty(logger.Infos);
        Assert.Empty(time.Delays);
    }

    [Fact]
    public async Task RunAsync_RestartRunBot_DoesNothing()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        BotLoopContext ctx = BotLoopTestKit.Context(
            time, logger, new MaintenanceScheduler(time, logger, 42), _ => Task.FromResult(BotLoopTestKit.Frame()));
        ctx.RestartRunBot = true;

        await new BotLoop(ctx).RunAsync(CancellationToken.None);

        Assert.Empty(logger.Infos);
    }

    [Fact]
    public async Task RunInitialFlowAsync_NotMainScreenWithoutRecovery_ReturnsFalse()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        BotLoopContext ctx = BotLoopTestKit.Context(
            time,
            logger,
            new MaintenanceScheduler(time, logger, 42),
            _ => Task.FromResult(BotLoopTestKit.Frame()),
            isMainScreen: _ => false);

        bool ok = await new BotLoop(ctx).RunInitialFlowAsync(CancellationToken.None);

        Assert.False(ok);
        Assert.Contains(logger.Infos, m => m.Contains("SwitchCoCAcc", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunInitialFlowAsync_RecoveryFails_ReturnsFalse_AndCallsRecoveryOnce()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        int recoveryCalls = 0;
        BotLoopContext ctx = BotLoopTestKit.Context(
            time,
            logger,
            new MaintenanceScheduler(time, logger, 42),
            _ => Task.FromResult(BotLoopTestKit.Frame()),
            isMainScreen: _ => false,
            ensureMainScreen: (_, _) =>
            {
                recoveryCalls++;
                return Task.FromResult(false);
            });

        bool ok = await new BotLoop(ctx).RunInitialFlowAsync(CancellationToken.None);

        Assert.False(ok);
        Assert.Equal(1, recoveryCalls);
    }

    [Fact]
    public async Task RunInitialFlowAsync_Success_ResetsMaintenanceGroups()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        var scheduler = new MaintenanceScheduler(time, logger, 42);
        BotLoopContext ctx = BotLoopTestKit.Context(
            time, logger, scheduler, _ => Task.FromResult(BotLoopTestKit.Frame()));

        bool ok = await new BotLoop(ctx).RunInitialFlowAsync(CancellationToken.None);

        Assert.True(ok);
        Assert.False(scheduler.IsDue(MaintenanceAction.Laboratory, MaintenanceData.StatusMin, MaintenanceData.StatusMax));
        Assert.False(scheduler.IsDue(MaintenanceAction.HelperHut, MaintenanceData.StatusMin, MaintenanceData.StatusMax));
        Assert.False(scheduler.IsDue(MaintenanceAction.HeroStatus, MaintenanceData.StatusMin, MaintenanceData.StatusMax));
        Assert.False(scheduler.IsDue(MaintenanceAction.CollectCCGold, MaintenanceData.FastMin, MaintenanceData.FastMax));
        Assert.True(scheduler.IsDue(MaintenanceAction.Collect, MaintenanceData.FastMin, MaintenanceData.FastMax));
    }

    [Fact]
    public async Task RunAsync_InitFails_DoesNotEnterMainLoop()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        BotLoopContext ctx = BotLoopTestKit.Context(
            time,
            logger,
            new MaintenanceScheduler(time, logger, 42),
            _ => Task.FromResult(BotLoopTestKit.Frame()),
            isMainScreen: _ => false);

        await new BotLoop(ctx).RunAsync(CancellationToken.None);

        Assert.DoesNotContain(logger.Infos, m => m.Contains("RushAttackCycle", StringComparison.Ordinal));
        Assert.Empty(logger.Errors);
    }

    [Fact]
    public async Task RunAsync_EmptyFrameDuringInit_LogsWarningAndStops()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        BotLoopContext ctx = BotLoopTestKit.Context(
            time,
            logger,
            new MaintenanceScheduler(time, logger, 42),
            _ => Task.FromResult(new Mat()));

        await new BotLoop(ctx).RunAsync(CancellationToken.None);

        Assert.Contains("[BotLoop] Không chụp được khung hình khi khởi động.", logger.Warnings);
        Assert.DoesNotContain(logger.Infos, m => m.Contains("RushAttackCycle", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_MainLoop_StopsWhenRunStateTurnsOff()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        BotLoopContext? ctx = null;
        int frames = 0;
        ctx = BotLoopTestKit.Context(
            time,
            logger,
            new MaintenanceScheduler(time, logger, 42),
            _ =>
            {
                frames++;
                if (frames >= 2)
                {
                    ctx!.RunState = false;
                }

                return Task.FromResult(BotLoopTestKit.Frame());
            });

        await new BotLoop(ctx).RunAsync(CancellationToken.None);

        Assert.Equal(2, frames);
        Assert.Contains(50, time.Delays);
        Assert.Empty(logger.Errors);
    }

    [Fact]
    public async Task RunAsync_NotMainScreenInMainLoop_CallsEnsureMainScreen()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        BotLoopContext? ctx = null;
        int mainScreenChecks = 0;
        int recoveryCalls = 0;
        ctx = BotLoopTestKit.Context(
            time,
            logger,
            new MaintenanceScheduler(time, logger, 42),
            _ => Task.FromResult(BotLoopTestKit.Frame()),
            isMainScreen: _ => mainScreenChecks++ == 0,
            ensureMainScreen: (_, _) =>
            {
                recoveryCalls++;
                ctx!.RunState = false;
                return Task.FromResult(true);
            });

        await new BotLoop(ctx).RunAsync(CancellationToken.None);

        Assert.Equal(1, recoveryCalls);
        Assert.Empty(logger.Errors);
    }

    [Fact]
    public async Task RunAsync_FirstMainScreen_CallsOnMainScreenSeenOnce()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        BotLoopContext? ctx = null;
        int frames = 0;
        int seenCalls = 0;
        ctx = BotLoopTestKit.Context(
            time,
            logger,
            new MaintenanceScheduler(time, logger, 42),
            _ =>
            {
                frames++;
                if (frames >= 4)
                {
                    ctx!.RunState = false;
                }

                return Task.FromResult(BotLoopTestKit.Frame());
            },
            onMainScreenSeen: _ =>
            {
                seenCalls++;
                return Task.FromResult(true);
            });

        await new BotLoop(ctx).RunAsync(CancellationToken.None);

        Assert.Equal(1, seenCalls);
    }

    [Fact]
    public async Task RunAsync_CollectDue_RunsOnceThenMarksRun()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        BotLoopContext? ctx = null;
        int frames = 0;
        ctx = BotLoopTestKit.Context(
            time,
            logger,
            new MaintenanceScheduler(time, logger, 42),
            _ =>
            {
                frames++;
                if (frames >= 5)
                {
                    ctx!.RunState = false;
                }

                return Task.FromResult(BotLoopTestKit.Frame());
            });

        await new BotLoop(ctx).RunAsync(CancellationToken.None);

        Assert.Single(logger.Warnings, w => w.Contains("[Collect]", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_CollectWithHarvester_TapsMines()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        var matcher = new FakeMatcher(new[] { new MatchResult(100, 100, 20, 20, 0.9) });
        var input = new FakeInput();
        using var gold = new Mat(4, 4, MatType.CV_8UC1, Scalar.All(0));

        BotLoopContext? ctx = null;
        int frames = 0;
        ctx = BotLoopTestKit.Context(
            time,
            logger,
            new MaintenanceScheduler(time, logger, 42),
            _ =>
            {
                frames++;
                if (frames >= 5)
                {
                    ctx!.RunState = false;
                }

                return Task.FromResult(BotLoopTestKit.Frame());
            },
            matcher: matcher,
            input: input,
            collector: new CollectorHarvester(matcher, input, new HumanDelay(time, 42), gold, null, null, null));

        await new BotLoop(ctx).RunAsync(CancellationToken.None);

        Assert.Single(input.Taps);
        Assert.Contains(logger.Infos, m => m.Contains("[Collect] Tap 1 mỏ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_CancelledToken_ThrowsOperationCanceled()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        BotLoopContext ctx = BotLoopTestKit.Context(
            time, logger, new MaintenanceScheduler(time, logger, 42), _ => Task.FromResult(BotLoopTestKit.Frame()));

        using var cts = new CancellationTokenSource();
        time.BeforeDelay = _ => cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new BotLoop(ctx).RunAsync(cts.Token));

        Assert.Empty(logger.Errors);
    }

    [Fact]
    public async Task RunAsync_Paused_ReturnsBeforeMainLoop()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        BotLoopContext ctx = BotLoopTestKit.Context(
            time, logger, new MaintenanceScheduler(time, logger, 42), _ => Task.FromResult(BotLoopTestKit.Frame()));
        ctx.Paused = true;

        await new BotLoop(ctx).RunAsync(CancellationToken.None);

        Assert.DoesNotContain(logger.Infos, m => m.Contains("RushAttackCycle", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_RestartRunBotDuringLoop_ExitsBeforeAttack()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        BotLoopContext? ctx = null;
        ctx = BotLoopTestKit.Context(
            time,
            logger,
            new MaintenanceScheduler(time, logger, 42),
            _ => Task.FromResult(BotLoopTestKit.Frame()),
            updateResources: (_, _) =>
            {
                ctx!.RestartRunBot = true;
                return Task.CompletedTask;
            });

        await new BotLoop(ctx).RunAsync(CancellationToken.None);

        Assert.DoesNotContain(logger.Infos, m => m.Contains("AttackMain", StringComparison.Ordinal));
        Assert.Empty(logger.Errors);
    }

    [Fact]
    public async Task RunAsync_ResourceRead_RespectsInterval()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        BotLoopContext? ctx = null;
        int frames = 0;
        int reads = 0;
        ctx = BotLoopTestKit.Context(
            time,
            logger,
            new MaintenanceScheduler(time, logger, 42),
            _ =>
            {
                frames++;
                if (frames >= 5)
                {
                    ctx!.RunState = false;
                }

                return Task.FromResult(BotLoopTestKit.Frame());
            },
            updateResources: (_, _) =>
            {
                reads++;
                return Task.CompletedTask;
            },
            resourceReadInterval: TimeSpan.FromSeconds(10));

        await new BotLoop(ctx).RunAsync(CancellationToken.None);

        Assert.Equal(1, reads);
    }

    [Fact]
    public async Task RunAsync_StubSteps_LogInAutoItOrder()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        BotLoopContext? ctx = null;
        int frames = 0;
        ctx = BotLoopTestKit.Context(
            time,
            logger,
            new MaintenanceScheduler(time, logger, 42),
            _ =>
            {
                frames++;
                if (frames >= 3)
                {
                    ctx!.RunState = false;
                }

                return Task.FromResult(BotLoopTestKit.Frame());
            });

        await new BotLoop(ctx).RunAsync(CancellationToken.None);

        Assert.Equal("[STUB] SwitchCoCAcc — M3/M4 chưa port", logger.Infos[0]);
        Assert.Equal("[STUB] RushAttack — M3/M4 chưa port", logger.Infos[1]);
        int cycle = logger.Infos.FindIndex(m => m.Contains("RushAttackCycle", StringComparison.Ordinal));
        int obstacles = logger.Infos.FindIndex(m => m.Contains("checkObstacles", StringComparison.Ordinal));
        Assert.True(cycle > 1);
        Assert.True(obstacles > cycle);
    }

    [Fact]
    public async Task RunAsync_SameSeed_ProducesIdenticalTrace()
    {
        (List<int> Delays, List<string> Infos, List<string> Debugs) first = await RunFixedTraceAsync();
        (List<int> Delays, List<string> Infos, List<string> Debugs) second = await RunFixedTraceAsync();

        Assert.Equal(first.Delays, second.Delays);
        Assert.Equal(first.Infos, second.Infos);
        Assert.Equal(first.Debugs, second.Debugs);
    }

    private static async Task<(List<int> Delays, List<string> Infos, List<string> Debugs)> RunFixedTraceAsync()
    {
        var time = new FakeTimeProvider();
        var logger = new FakeLogger();
        BotLoopContext? ctx = null;
        int frames = 0;
        ctx = BotLoopTestKit.Context(
            time,
            logger,
            new MaintenanceScheduler(time, logger, 42),
            _ =>
            {
                frames++;
                if (frames >= 5)
                {
                    ctx!.RunState = false;
                }

                return Task.FromResult(BotLoopTestKit.Frame());
            },
            seed: 42);

        await new BotLoop(ctx).RunAsync(CancellationToken.None);
        return (time.Delays.ToList(), logger.Infos.ToList(), logger.Debugs.ToList());
    }
}
