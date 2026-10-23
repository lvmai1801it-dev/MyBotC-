using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyBot.Core.Abstractions;
using MyBot.Core.FSM.Maintenance;

namespace MyBot.UI.BotRuntime.Steps;

/// <summary>
/// Cầu nối giữa <see cref="MaintenanceScheduler"/> (đồng bộ) và các step bất đồng bộ.
///
/// parity: <c>__RunMaintenanceActions</c> @ MyBot.run.au3:801 và
/// <c>__RunMaintenanceAction</c> @ MyBot.run.au3:783.
/// </summary>
public sealed class MaintenanceStep
{
    private readonly MaintenanceScheduler _scheduler;
    private readonly IBotLogger _logger;

    /// <summary>Tạo bước bảo trì.</summary>
    /// <param name="scheduler">Bộ hẹn giờ bảo trì.</param>
    /// <param name="logger">Logger của bot.</param>
    public MaintenanceStep(MaintenanceScheduler scheduler, IBotLogger logger)
    {
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Có action nào trong nhóm tới hạn chưa.</summary>
    /// <param name="group">Nhóm action.</param>
    /// <param name="minMinutes">Cận dưới cooldown (phút).</param>
    /// <param name="maxMinutes">Cận trên cooldown (phút).</param>
    /// <returns>True nếu có action tới hạn.</returns>
    public bool IsDue(IReadOnlyList<MaintenanceAction> group, int minMinutes, int maxMinutes)
        => _scheduler.AnyDue(group, minMinutes, maxMinutes);

    /// <summary>Chạy cả nhóm theo thứ tự xáo trộn, dừng ở action đầu tiên thất bại.</summary>
    /// <param name="ctx">Ngữ cảnh vòng bot.</param>
    /// <param name="group">Nhóm action.</param>
    /// <param name="minMinutes">Cận dưới cooldown (phút).</param>
    /// <param name="maxMinutes">Cận trên cooldown (phút).</param>
    /// <param name="ct">Token huỷ.</param>
    /// <returns>False khi action lỗi hoặc bot bị dừng.</returns>
    public Task<bool> RunGroupAsync(
        BotLoopContext ctx,
        IReadOnlyList<MaintenanceAction> group,
        int minMinutes,
        int maxMinutes,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(group);

        // SAFE: scheduler chỉ đồng bộ. Vòng bot chạy trong Task.Run (MainViewModel.StartBot)
        // nên không có SynchronizationContext — bridge này không thể deadlock.
        bool ok = _scheduler.RunGroup(
            group,
            minMinutes,
            maxMinutes,
            action => ExecuteAsync(ctx, action, ct).GetAwaiter().GetResult(),
            () => ctx.CanContinue);

        return Task.FromResult(ok);
    }

    /// <summary>Chạy một action đơn lẻ.</summary>
    /// <param name="ctx">Ngữ cảnh vòng bot.</param>
    /// <param name="action">Action cần chạy.</param>
    /// <param name="minMinutes">Cận dưới cooldown (phút).</param>
    /// <param name="maxMinutes">Cận trên cooldown (phút).</param>
    /// <param name="ct">Token huỷ.</param>
    /// <param name="force">Chạy dù chưa tới hạn.</param>
    /// <returns>False khi action lỗi hoặc bot bị dừng.</returns>
    public Task<bool> RunActionAsync(
        BotLoopContext ctx,
        MaintenanceAction action,
        int minMinutes,
        int maxMinutes,
        CancellationToken ct = default,
        bool force = false)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        // SAFE: như trên — scheduler đồng bộ, không có SynchronizationContext.
        bool ok = _scheduler.RunAction(
            action,
            minMinutes,
            maxMinutes,
            item => ExecuteAsync(ctx, item, ct).GetAwaiter().GetResult(),
            () => ctx.CanContinue,
            force: force);

        return Task.FromResult(ok);
    }

    private async Task<bool> ExecuteAsync(BotLoopContext ctx, MaintenanceAction action, CancellationToken ct)
    {
        if (action == MaintenanceAction.Collect)
        {
            return await new CollectStep(_logger).ExecuteAsync(ctx, ct).ConfigureAwait(false);
        }

        // CollectCCGold và các action còn lại thuộc M3/M4/V-* — chưa port.
        return await new StubStep(MaintenanceData.NameOf(action), _logger).ExecuteAsync(ct).ConfigureAwait(false);
    }
}
