using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyBot.Core.FSM.Maintenance;
using MyBot.UI.BotRuntime.Steps;
using OpenCvSharp;

namespace MyBot.UI.BotRuntime;

/// <summary>
/// Vòng chạy chính của bot: khởi động một lần rồi lặp bảo trì / thu hoạch / idle.
///
/// parity: <c>runBot</c> @ MyBot.run.au3:930, vòng init và
/// <c>__RunBotInitialFlow</c> @ MyBot.run.au3:842, <c>__RunBotCanContinue</c> @ MyBot.run.au3:832,
/// <c>__RunBotStepReady</c> @ MyBot.run.au3:836.
/// Các module M2–M4 (train/attack/donate/upgrade) mới là <see cref="StubStep"/>.
/// </summary>
public sealed class BotLoop
{
    private readonly BotLoopContext _ctx;
    private readonly MaintenanceStep _maintenance;
    private readonly IdleStep _idle;
    private readonly Random _random;

    private DateTime? _lastResourceRead;
    private bool _onMainScreen;
    private bool _builderBaseLogged;

    /// <summary>Tạo vòng chạy từ ngữ cảnh đã dựng.</summary>
    /// <param name="context">Ngữ cảnh vòng bot.</param>
    public BotLoop(BotLoopContext context)
    {
        _ctx = context ?? throw new ArgumentNullException(nameof(context));
        _maintenance = new MaintenanceStep(_ctx.Scheduler, _ctx.Logger);
        _idle = new IdleStep(_ctx.Logger, _ctx.Seed);
        _random = _ctx.Seed.HasValue ? new Random(_ctx.Seed.Value) : new Random();
    }

    /// <summary>Chạy vòng init rồi vòng chính cho tới khi bị dừng.</summary>
    /// <param name="ct">Token huỷ.</param>
    /// <returns>Task hoàn tất khi vòng bot kết thúc.</returns>
    public async Task RunAsync(CancellationToken ct)
    {
        // parity: guard đầu runBot @ MyBot.run.au3:935.
        if (!_ctx.RunState || _ctx.RestartRunBot)
        {
            return;
        }

        try
        {
            if (!await RunInitialFlowAsync(ct).ConfigureAwait(false))
            {
                _ctx.Logger.Debug("[BotLoop] Init flow không hoàn tất — dừng vòng bot.");
                return;
            }

            await RunMainLoopAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // parity: lỗi trong action làm runBot thoát; AutoIt ghi log rồi dừng.
            _ctx.Logger.Error("[BotLoop] Lỗi nghiêm trọng — dừng vòng bot.", ex);
        }
    }

    /// <summary>Vòng init: chạy lại <c>__RunBotInitialFlow</c> khi <see cref="BotLoopContext.Restart"/> bật.</summary>
    /// <param name="ct">Token huỷ.</param>
    /// <returns>True khi init xong và được phép vào vòng chính.</returns>
    public async Task<bool> RunInitialFlowAsync(CancellationToken ct)
    {
        // parity: vòng init của runBot @ MyBot.run.au3:940-952.
        while (_ctx.RunState && !ct.IsCancellationRequested)
        {
            _ctx.Restart = false;

            if (await RunInitialFlowOnceAsync(ct).ConfigureAwait(false))
            {
                return true;
            }

            if (!_ctx.RunState || _ctx.RestartRunBot || !_ctx.Restart)
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>Vòng chính — mỗi vòng một bước bảo trì theo đúng thứ tự AutoIt.</summary>
    /// <param name="ct">Token huỷ.</param>
    /// <returns>Task hoàn tất khi <see cref="BotLoopContext.RunState"/> tắt.</returns>
    public async Task RunMainLoopAsync(CancellationToken ct)
    {
        while (_ctx.RunState)
        {
            if (_ctx.RestartRunBot || _ctx.Paused)
            {
                return;
            }

            if (_ctx.IsPaused is not null && _ctx.IsPaused())
            {
                await _ctx.Time.DelayAsync(_ctx.PauseIntervalMs, ct).ConfigureAwait(false);
                continue;
            }

            // parity: RushAttackCycle → auto-restart → PrepareDonateCC @ MyBot.run.au3:955-975.
            await new StubStep("RushAttackCycle", _ctx.Logger).ExecuteAsync(ct).ConfigureAwait(false);
            await new StubStep("CheckAutoRestart", _ctx.Logger).ExecuteAsync(ct).ConfigureAwait(false);
            await new StubStep("PrepareDonateCC", _ctx.Logger).ExecuteAsync(ct).ConfigureAwait(false);

            // parity: reset $g_bRestart mỗi vòng @ MyBot.run.au3:975.
            _ctx.Restart = false;

            // parity: __RunBotStepReady(_Sleep(50)) @ MyBot.run.au3:836.
            if (!await StepReadyAsync(ct).ConfigureAwait(false))
            {
                if (_ctx.Restart)
                {
                    continue;
                }

                return;
            }

            using (Mat frame = await _ctx.GrabFrameAsync(ct).ConfigureAwait(false))
            {
                if (frame.Empty())
                {
                    _ctx.Logger.Warning("[BotLoop] Không chụp được khung hình.");
                    await _ctx.Time.DelayAsync(50, ct).ConfigureAwait(false);
                    continue;
                }

                await UpdateResourcesIfDueAsync(frame, ct).ConfigureAwait(false);

                // parity: checkObstacles() @ MyBot.run.au3:984.
                await new StubStep("checkObstacles", _ctx.Logger).ExecuteAsync(ct).ConfigureAwait(false);

                if (_ctx.IsBuilderBase is not null && _ctx.IsBuilderBase(frame))
                {
                    _onMainScreen = false;

                    if (!_builderBaseLogged)
                    {
                        _ctx.Logger.Info("[BotLoop] Đang ở Builder Base — chưa hỗ trợ chuyển về Main Village.");
                        _builderBaseLogged = true;
                    }

                    await _ctx.Time.DelayAsync(_ctx.PauseIntervalMs, ct).ConfigureAwait(false);
                    continue;
                }

                _builderBaseLogged = false;

                // parity: checkMainScreen(False) @ MyBot.run.au3:986.
                bool mainScreen = _ctx.IsMainScreen(frame);

                if (!mainScreen)
                {
                    _onMainScreen = false;

                    if (_ctx.EnsureMainScreenAsync is not null
                        && !await _ctx.EnsureMainScreenAsync(frame, ct).ConfigureAwait(false))
                    {
                        return;
                    }

                    continue;
                }
            }

            if (!_onMainScreen)
            {
                _onMainScreen = true;

                if (_ctx.OnMainScreenSeenAsync is not null
                    && !await _ctx.OnMainScreenSeenAsync(ct).ConfigureAwait(false))
                {
                    return;
                }

                continue;
            }

            // parity: CheckAndroidReboot / CheckBotRestart / VillageReport / BotCommand @ MyBot.run.au3:988-1010.
            await new StubStep("CheckAndroidReboot", _ctx.Logger).ExecuteAsync(ct).ConfigureAwait(false);
            await new StubStep("CheckBotRestart", _ctx.Logger).ExecuteAsync(ct).ConfigureAwait(false);
            await new StubStep("VillageReport", _ctx.Logger).ExecuteAsync(ct).ConfigureAwait(false);
            await new StubStep("BotCommand", _ctx.Logger).ExecuteAsync(ct).ConfigureAwait(false);

            await RunGroupIfDueAsync(
                MaintenanceData.Collect, MaintenanceData.FastMin, MaintenanceData.FastMax, ct).ConfigureAwait(false);

            if (!await StepReadyAsync(ct).ConfigureAwait(false))
            {
                if (_ctx.Restart)
                {
                    continue;
                }

                return;
            }

            await RunGroupIfDueAsync(
                MaintenanceData.Cleanup, MaintenanceData.CleanupMin, MaintenanceData.CleanupMax, ct).ConfigureAwait(false);

            if (!await StepReadyAsync(ct).ConfigureAwait(false))
            {
                if (_ctx.Restart)
                {
                    continue;
                }

                return;
            }

            // parity: AddIdleTime @ COCBot/functions/Village/AddIdleTime.au3:16.
            await AddIdleTimeAsync(ct).ConfigureAwait(false);

            // parity: IsSearchAttackEnabled @ MyBot.run.au3:1043.
            if (_ctx.IsSearchAttackEnabled)
            {
                // parity: Communication group (Fast) @ MyBot.run.au3:1046.
                await RunGroupIfDueAsync(
                    MaintenanceData.Communication, MaintenanceData.FastMin, MaintenanceData.FastMax, ct).ConfigureAwait(false);

                await new StubStep("DonateCC", _ctx.Logger).ExecuteAsync(ct).ConfigureAwait(false);
                await new StubStep("Train", _ctx.Logger).ExecuteAsync(ct).ConfigureAwait(false);
                await new StubStep("AttackCycle", _ctx.Logger).ExecuteAsync(ct).ConfigureAwait(false);
            }
            else
            {
                await new StubStep("DonateCC", _ctx.Logger).ExecuteAsync(ct).ConfigureAwait(false);
                await new StubStep("Train", _ctx.Logger).ExecuteAsync(ct).ConfigureAwait(false);
            }

            if (!await StepReadyAsync(ct).ConfigureAwait(false))
            {
                if (_ctx.Restart)
                {
                    continue;
                }

                return;
            }

            // parity: Status group + HeroStatus + HelperHut (checkReboot) @ MyBot.run.au3:1064-1080.
            await RunGroupIfDueAsync(
                MaintenanceData.Status, MaintenanceData.StatusMin, MaintenanceData.StatusMax, ct).ConfigureAwait(false);
            await _maintenance.RunActionAsync(
                _ctx, MaintenanceAction.HeroStatus,
                MaintenanceData.StatusMin, MaintenanceData.StatusMax, ct).ConfigureAwait(false);
            await _maintenance.RunActionAsync(
                _ctx, MaintenanceAction.HelperHut,
                MaintenanceData.StatusMin, MaintenanceData.StatusMax, ct).ConfigureAwait(false);

            // parity: khối AllUpgrades @ MyBot.run.au3:1085-1110.
            if (_maintenance.IsDue(MaintenanceData.AllUpgrades, MaintenanceData.UpgradeMin, MaintenanceData.UpgradeMax))
            {
                await RunActionIfDueAsync(
                    MaintenanceAction.UpgradeBuilding,
                    MaintenanceData.UpgradeMin, MaintenanceData.UpgradeMax, ct).ConfigureAwait(false);
                await RunActionIfDueAsync(
                    MaintenanceAction.UpgradeHeroes,
                    MaintenanceData.UpgradeMin, MaintenanceData.UpgradeMax, ct).ConfigureAwait(false);
                await RunGroupIfDueAsync(
                    MaintenanceData.Upgrade,
                    MaintenanceData.UpgradeMin, MaintenanceData.UpgradeMax, ct).ConfigureAwait(false);
                await RunActionIfDueAsync(
                    MaintenanceAction.AutoUpgrade,
                    MaintenanceData.UpgradeMin, MaintenanceData.UpgradeMax, ct).ConfigureAwait(false);
                await RunActionIfDueAsync(
                    MaintenanceAction.AutoUpgradeBB,
                    MaintenanceData.BuilderBaseMin, MaintenanceData.BuilderBaseMax, ct).ConfigureAwait(false);
            }

            // parity: BuilderBase + Clan Games @ MyBot.run.au3:1115-1125.
            await RunActionIfDueAsync(
                MaintenanceAction.BuilderBase,
                MaintenanceData.BuilderBaseMin, MaintenanceData.BuilderBaseMax, ct).ConfigureAwait(false);
            await RunActionIfDueAsync(
                MaintenanceAction.ClanGames,
                MaintenanceData.ClanGamesMin, MaintenanceData.ClanGamesMax, ct).ConfigureAwait(false);

            // parity: UpgradeWall chạy cuối @ MyBot.run.au3:1130.
            await RunActionIfDueAsync(
                MaintenanceAction.UpgradeWall,
                MaintenanceData.UpgradeMin, MaintenanceData.UpgradeMax, ct).ConfigureAwait(false);

            if (!await StepReadyAsync(ct).ConfigureAwait(false))
            {
                if (_ctx.Restart)
                {
                    continue;
                }

                return;
            }

            _ctx.FirstStart = false;

            // parity: ProfileSwitchAccount @ MyBot.run.au3:1140.
            await new StubStep("SwitchAccount", _ctx.Logger).ExecuteAsync(ct).ConfigureAwait(false);

            // parity: Idle (chưa đầy quân) / AttackMain @ MyBot.run.au3:1150-1165.
            if (_ctx.IsSearchAttackEnabled)
            {
                await new StubStep("AttackMain", _ctx.Logger).ExecuteAsync(ct).ConfigureAwait(false);
            }
            else
            {
                await _idle.ExecuteAsync(_ctx, ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Chạy <c>__RunBotInitialFlow</c> một lần.</summary>
    /// <param name="ct">Token huỷ.</param>
    /// <returns>True khi init hoàn tất.</returns>
    private async Task<bool> RunInitialFlowOnceAsync(CancellationToken ct)
    {
        // parity: rematch tài khoản + chờ màn hình làng @ MyBot.run.au3:848-860.
        await new StubStep("SwitchCoCAcc", _ctx.Logger).ExecuteAsync(ct).ConfigureAwait(false);
        await new StubStep("RushAttack", _ctx.Logger).ExecuteAsync(ct).ConfigureAwait(false);

        using (Mat frame = await _ctx.GrabFrameAsync(ct).ConfigureAwait(false))
        {
            if (frame.Empty())
            {
                _ctx.Logger.Warning("[BotLoop] Không chụp được khung hình khi khởi động.");
                return false;
            }

            if (!_ctx.IsMainScreen(frame))
            {
                if (_ctx.EnsureMainScreenAsync is null)
                {
                    return false;
                }

                if (!await _ctx.EnsureMainScreenAsync(frame, ct).ConfigureAwait(false))
                {
                    return false;
                }
            }
        }

        // parity: khối first-start @ MyBot.run.au3:866-925 — M3/M4 chưa port.
        string[] firstStartSteps =
        {
            "VillageReport",
            "RunClanGamesIfDue",
            "Laboratory",
            "PetHouse",
            "Blacksmith",
            "UpgradeBuilding",
            "AutoUpgrade",
            "UpgradeHeroes",
            "HelperHut",
            "CollectAchievements",
            "CollectFreeMagicItems",
            "CollectCCGold",
            "ForgeClanCapitalGold",
            "FirstCheck"
        };

        foreach (string step in firstStartSteps)
        {
            await new StubStep(step, _ctx.Logger).ExecuteAsync(ct).ConfigureAwait(false);

            if (!await StepReadyAsync(ct).ConfigureAwait(false))
            {
                return false;
            }
        }

        // parity: __ResetMaintenanceActions(Initial*) @ MyBot.run.au3:821 và
        // __MaintenanceDue(HeroStatus/CollectCCGold, ..., True) @ MyBot.run.au3:912-928.
        _ctx.Scheduler.ResetGroup(
            MaintenanceData.InitialStatus, MaintenanceData.StatusMin, MaintenanceData.StatusMax);
        _ctx.Scheduler.ResetGroup(
            MaintenanceData.InitialCleanup, MaintenanceData.CleanupMin, MaintenanceData.CleanupMax);
        _ctx.Scheduler.ResetGroup(
            MaintenanceData.InitialUpgrade, MaintenanceData.UpgradeMin, MaintenanceData.UpgradeMax);
        _ctx.Scheduler.MarkRun(
            MaintenanceAction.HeroStatus, MaintenanceData.StatusMin, MaintenanceData.StatusMax);
        _ctx.Scheduler.MarkRun(
            MaintenanceAction.CollectCCGold, MaintenanceData.FastMin, MaintenanceData.FastMax);

        return true;
    }

    /// <summary>Chạy cả nhóm nếu có ít nhất một action tới hạn.</summary>
    /// <param name="group">Nhóm action.</param>
    /// <param name="minMinutes">Cận dưới cooldown (phút).</param>
    /// <param name="maxMinutes">Cận trên cooldown (phút).</param>
    /// <param name="ct">Token huỷ.</param>
    /// <returns>Task hoàn tất khi nhóm chạy xong hoặc bị bỏ qua.</returns>
    private async Task RunGroupIfDueAsync(
        IReadOnlyList<MaintenanceAction> group,
        int minMinutes,
        int maxMinutes,
        CancellationToken ct)
    {
        if (!_maintenance.IsDue(group, minMinutes, maxMinutes))
        {
            return;
        }

        await _maintenance.RunGroupAsync(_ctx, group, minMinutes, maxMinutes, ct).ConfigureAwait(false);
    }

    /// <summary>Chạy một action nếu tới hạn (scheduler tự kiểm tra).</summary>
    /// <param name="action">Action cần chạy.</param>
    /// <param name="minMinutes">Cận dưới cooldown (phút).</param>
    /// <param name="maxMinutes">Cận trên cooldown (phút).</param>
    /// <param name="ct">Token huỷ.</param>
    /// <returns>Task hoàn tất khi action chạy xong hoặc bị bỏ qua.</returns>
    private async Task RunActionIfDueAsync(
        MaintenanceAction action,
        int minMinutes,
        int maxMinutes,
        CancellationToken ct)
    {
        await _maintenance.RunActionAsync(_ctx, action, minMinutes, maxMinutes, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Cổng giữa hai step: <see cref="BotLoopContext.CanContinue"/> → <c>_Sleep(50)</c> → kiểm tra lại.
    ///
    /// parity: <c>__RunBotStepReady</c> @ MyBot.run.au3:836.
    /// </summary>
    /// <param name="ct">Token huỷ.</param>
    /// <returns>True nếu bot vẫn được phép chạy sau khi chờ.</returns>
    private async Task<bool> StepReadyAsync(CancellationToken ct)
    {
        if (!_ctx.CanContinue)
        {
            return false;
        }

        await _ctx.Time.DelayAsync(50, ct).ConfigureAwait(false);
        return _ctx.CanContinue;
    }

    /// <summary>Đọc tài nguyên theo nhịp <see cref="BotLoopContext.ResourceReadInterval"/>.</summary>
    /// <param name="frame">Khung hình hiện tại.</param>
    /// <param name="ct">Token huỷ.</param>
    /// <returns>Task hoàn tất khi đọc xong hoặc bỏ qua.</returns>
    private async Task UpdateResourcesIfDueAsync(Mat frame, CancellationToken ct)
    {
        if (_ctx.UpdateResourcesAsync is null)
        {
            return;
        }

        DateTime now = _ctx.Time.UtcNow;
        if (_lastResourceRead is not null && now - _lastResourceRead.Value < _ctx.ResourceReadInterval)
        {
            return;
        }

        _lastResourceRead = now;
        await _ctx.UpdateResourcesAsync(frame, ct).ConfigureAwait(false);
    }

    /// <summary>Thêm thời gian idle ngẫu nhiên nếu cấu hình bật.</summary>
    /// <param name="ct">Token huỷ.</param>
    /// <returns>Task hoàn tất sau khi chờ xong.</returns>
    private async Task AddIdleTimeAsync(CancellationToken ct)
    {
        int min = _ctx.AddIdleDelayMinSeconds;
        int max = _ctx.AddIdleDelayMaxSeconds;

        if (max <= 0)
        {
            return;
        }

        if (min < 0)
        {
            min = 0;
        }

        if (max < min)
        {
            max = min;
        }

        int seconds = min == max ? min : _random.Next(min, max + 1);
        if (seconds <= 0)
        {
            return;
        }

        await _ctx.Time.DelayAsync(seconds * 1000, ct).ConfigureAwait(false);
    }
}
