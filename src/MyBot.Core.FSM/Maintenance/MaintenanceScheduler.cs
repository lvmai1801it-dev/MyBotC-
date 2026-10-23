using MyBot.Core.Abstractions;

namespace MyBot.Core.FSM.Maintenance;

/// <summary>
/// Bộ hẹn giờ bảo trì: mỗi action có timer + cooldown ngẫu nhiên riêng, chạy theo nhóm
/// với thứ tự xáo trộn.
///
/// parity: <c>__MaintenanceDue</c> @ MyBot.run.au3:735, <c>__RunMaintenanceAction</c> @
/// MyBot.run.au3:783, <c>__RunMaintenanceActions</c> @ MyBot.run.au3:801,
/// <c>__MaintenanceActionsDue</c> @ MyBot.run.au3:813,
/// <c>__ResetMaintenanceActions</c> @ MyBot.run.au3:821.
///
/// AutoIt giữ timer theo (profile, action); bản port dùng một scheduler cho mỗi tài khoản.
/// Không lock: scheduler chạy đơn luồng trong vòng bot. Mọi mốc thời gian lấy từ
/// <see cref="ITimeProvider.UtcNow"/>.
/// </summary>
public sealed class MaintenanceScheduler
{
    private readonly ITimeProvider _time;
    private readonly IBotLogger _logger;
    private readonly Random _random;

    private readonly DateTime?[] _timers = new DateTime?[MaintenanceData.Count];
    private readonly int[] _cooldownsMs = new int[MaintenanceData.Count];
    private readonly int[] _minMinutes = new int[MaintenanceData.Count];
    private readonly int[] _maxMinutes = new int[MaintenanceData.Count];

    /// <summary>Tạo scheduler; <paramref name="seed"/> cố định để test lặp lại được.</summary>
    /// <param name="time">Nguồn thời gian.</param>
    /// <param name="logger">Logger của bot.</param>
    /// <param name="seed">Seed cho cooldown và thứ tự xáo trộn.</param>
    public MaintenanceScheduler(ITimeProvider time, IBotLogger logger, int? seed = null)
    {
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>Action đã tới hạn chưa (đọc trạng thái, không đặt lại timer).</summary>
    /// <param name="action">Action cần kiểm tra.</param>
    /// <param name="minMinutes">Cận dưới cooldown (phút).</param>
    /// <param name="maxMinutes">Cận trên cooldown (phút).</param>
    /// <returns>True nếu chưa chạy lần nào, đổi dải, hoặc đã hết cooldown.</returns>
    public bool IsDue(MaintenanceAction action, int minMinutes, int maxMinutes)
    {
        int index = (int)action;
        if (index < 0 || index >= MaintenanceData.Count)
        {
            // parity: __MaintenanceDue trả True với ID ngoài dải.
            return true;
        }

        DateTime? timer = _timers[index];
        if (timer is null)
        {
            return true;
        }

        if (_minMinutes[index] != minMinutes || _maxMinutes[index] != maxMinutes)
        {
            return true;
        }

        return (_time.UtcNow - timer.Value).TotalMilliseconds >= _cooldownsMs[index];
    }

    /// <summary>Đánh dấu action vừa chạy: đặt timer = now, bốc cooldown mới trong [min,max].</summary>
    /// <param name="action">Action vừa chạy.</param>
    /// <param name="minMinutes">Cận dưới cooldown (phút).</param>
    /// <param name="maxMinutes">Cận trên cooldown (phút).</param>
    public void MarkRun(MaintenanceAction action, int minMinutes, int maxMinutes)
    {
        int index = (int)action;
        if (index < 0 || index >= MaintenanceData.Count)
        {
            return;
        }

        // parity: __MaintenanceDue($bReset=True) — Random(min, max, 1) * 60000.
        _timers[index] = _time.UtcNow;
        _cooldownsMs[index] = _random.Next(minMinutes, maxMinutes + 1) * 60_000;
        _minMinutes[index] = minMinutes;
        _maxMinutes[index] = maxMinutes;
    }

    /// <summary>Có ít nhất một action trong nhóm tới hạn.</summary>
    /// <param name="group">Nhóm action.</param>
    /// <param name="minMinutes">Cận dưới cooldown (phút).</param>
    /// <param name="maxMinutes">Cận trên cooldown (phút).</param>
    /// <returns>True nếu có action tới hạn.</returns>
    public bool AnyDue(IReadOnlyList<MaintenanceAction> group, int minMinutes, int maxMinutes)
    {
        ArgumentNullException.ThrowIfNull(group);
        for (int i = 0; i < group.Count; i++)
        {
            if (IsDue(group[i], minMinutes, maxMinutes))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Đặt lại timer cả nhóm như vừa chạy (dùng sau khởi động / đổi cấu hình).</summary>
    /// <param name="group">Nhóm action.</param>
    /// <param name="minMinutes">Cận dưới cooldown (phút).</param>
    /// <param name="maxMinutes">Cận trên cooldown (phút).</param>
    public void ResetGroup(IReadOnlyList<MaintenanceAction> group, int minMinutes, int maxMinutes)
    {
        ArgumentNullException.ThrowIfNull(group);
        for (int i = 0; i < group.Count; i++)
        {
            MarkRun(group[i], minMinutes, maxMinutes);
        }
    }

    /// <summary>Xáo trộn thứ tự chạy trên một bản copy (Fisher–Yates), có log Debug.</summary>
    /// <param name="group">Nhóm action gốc.</param>
    /// <returns>Thứ tự chạy mới.</returns>
    public IReadOnlyList<MaintenanceAction> ShuffleOrder(IReadOnlyList<MaintenanceAction> group)
    {
        ArgumentNullException.ThrowIfNull(group);

        // parity: _ArrayShuffle trên $aRunOrder (bản copy) @ MyBot.run.au3:801.
        var order = new MaintenanceAction[group.Count];
        for (int i = 0; i < group.Count; i++)
        {
            order[i] = group[i];
        }

        for (int i = order.Length - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        _logger.Debug("[Maintenance] Thứ tự: " + string.Join(", ", Array.ConvertAll(order, MaintenanceData.NameOf)));
        return order;
    }

    /// <summary>
    /// Chạy một action theo đúng thứ tự kiểm tra của AutoIt.
    ///
    /// parity: <c>__RunMaintenanceAction</c> @ MyBot.run.au3:783.
    /// </summary>
    /// <param name="action">Action cần chạy.</param>
    /// <param name="minMinutes">Cận dưới cooldown (phút).</param>
    /// <param name="maxMinutes">Cận trên cooldown (phút).</param>
    /// <param name="execute">Thân action; trả false khi action thất bại.</param>
    /// <param name="canContinue">Kiểm tra bot có được phép tiếp tục; null = luôn tiếp tục.</param>
    /// <param name="checkReboot">Có kiểm tra điều kiện reboot sau action không.</param>
    /// <param name="rebootCheck">Kiểm tra reboot; null = coi như không cần reboot.</param>
    /// <param name="force">Chạy dù chưa tới hạn.</param>
    /// <returns>False khi action lỗi hoặc bị dừng; True khi chạy xong hoặc được bỏ qua.</returns>
    public bool RunAction(
        MaintenanceAction action,
        int minMinutes,
        int maxMinutes,
        Func<MaintenanceAction, bool> execute,
        Func<bool>? canContinue = null,
        bool checkReboot = false,
        Func<bool>? rebootCheck = null,
        bool force = false)
    {
        ArgumentNullException.ThrowIfNull(execute);

        int index = (int)action;
        if (index < 0 || index >= MaintenanceData.Count)
        {
            _logger.Error("[Maintenance] Action không hợp lệ: " + index);
            return false;
        }

        if (!force && !IsDue(action, minMinutes, maxMinutes))
        {
            return true;
        }

        if (canContinue is not null && !canContinue())
        {
            return false;
        }

        if (!execute(action))
        {
            return false;
        }

        if (canContinue is not null && !canContinue())
        {
            return false;
        }

        if (checkReboot && rebootCheck is not null && rebootCheck())
        {
            return false;
        }

        if (canContinue is not null && !canContinue())
        {
            return false;
        }

        MarkRun(action, minMinutes, maxMinutes);
        return true;
    }

    /// <summary>
    /// Chạy cả nhóm theo thứ tự xáo trộn, dừng ở action đầu tiên trả false.
    ///
    /// parity: <c>__RunMaintenanceActions</c> @ MyBot.run.au3:801.
    /// </summary>
    /// <param name="group">Nhóm action.</param>
    /// <param name="minMinutes">Cận dưới cooldown (phút).</param>
    /// <param name="maxMinutes">Cận trên cooldown (phút).</param>
    /// <param name="execute">Thân action; trả false khi action thất bại.</param>
    /// <param name="canContinue">Kiểm tra bot có được phép tiếp tục; null = luôn tiếp tục.</param>
    /// <param name="checkReboot">Có kiểm tra điều kiện reboot sau mỗi action không.</param>
    /// <param name="rebootCheck">Kiểm tra reboot; null = coi như không cần reboot.</param>
    /// <returns>False nếu có action thất bại.</returns>
    public bool RunGroup(
        IReadOnlyList<MaintenanceAction> group,
        int minMinutes,
        int maxMinutes,
        Func<MaintenanceAction, bool> execute,
        Func<bool>? canContinue = null,
        bool checkReboot = false,
        Func<bool>? rebootCheck = null)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(execute);

        IReadOnlyList<MaintenanceAction> order = ShuffleOrder(group);
        for (int i = 0; i < order.Count; i++)
        {
            bool ok = RunAction(order[i], minMinutes, maxMinutes, execute,
                canContinue, checkReboot, rebootCheck);
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }
}
