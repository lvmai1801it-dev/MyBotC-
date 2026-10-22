using System;
using System.Threading;
using System.Threading.Tasks;
using MyBot.Core.Abstractions;
using OpenCvSharp;

namespace MyBot.Game.CoC.State;

/// <summary>
/// Tham số cho <see cref="MainScreenRecovery"/>. Giá trị mặc định port từ bản AutoIt
/// (<c>checkMainScreen.au3</c> + <c>waitMainScreen.au3</c>).
/// </summary>
public sealed class MainScreenRecoveryOptions
{
    /// <summary>Khoảng nghỉ giữa hai lần kiểm tra màn hình (ms). AutoIt: 1000ms.</summary>
    public int CheckIntervalMs { get; init; } = 1000;

    /// <summary>Số lần thất bại liên tiếp trước khi khởi động lại CoC. AutoIt: 20.</summary>
    public int MaxChecksBeforeRestart { get; init; } = 20;

    /// <summary>Số lần thất bại liên tiếp trước khi chạy chu trình phục hồi sâu. AutoIt: 24.</summary>
    public int MaxChecksBeforeGiveUp { get; init; } = 24;

    /// <summary>Số chu trình phục hồi sâu tối đa trước khi bỏ cuộc.</summary>
    public int MaxRecoveryCycles { get; init; } = 3;

    /// <summary>Thời gian tối đa chờ mạng hồi phục trước khi restart CoC (ms). AutoIt: 60000ms.</summary>
    public int ReconnectTimeoutMs { get; init; } = 60_000;

    /// <summary>Số lần restart CoC do mất mạng trước khi reboot Android.</summary>
    public int MaxReconnectRestarts { get; init; } = 3;

    /// <summary>Số lần kiểm tra trong giai đoạn chờ màn hình làng. AutoIt: 105.</summary>
    public int WaitAttempts { get; init; } = 105;

    /// <summary>Khoảng nghỉ giữa các lần chờ màn hình làng (ms). AutoIt: 3000ms.</summary>
    public int WaitIntervalMs { get; init; } = 3000;

    /// <summary>Số lần reboot Android tối đa khi chờ màn hình làng thất bại. AutoIt: 2.</summary>
    public int MaxAndroidReboots { get; init; } = 2;

    /// <summary>Thời gian chờ sau lệnh reboot trước khi kiểm tra lại (ms).</summary>
    public int RebootDelayMs { get; init; } = 30_000;
}

/// <summary>
/// Kết quả của <see cref="MainScreenRecovery.LocateAsync"/>.
/// </summary>
/// <param name="Located">True nếu đã tìm thấy màn hình làng chính.</param>
/// <param name="Cancelled">True nếu bị huỷ qua token.</param>
/// <param name="Reason">Lý do thất bại (null khi thành công).</param>
public sealed record MainScreenLocationResult(bool Located, bool Cancelled, string? Reason)
{
    /// <summary>Kết quả thành công.</summary>
    public static MainScreenLocationResult Success { get; } = new(true, false, null);

    /// <summary>Kết quả bị huỷ.</summary>
    public static MainScreenLocationResult CancelledResult { get; } = new(false, true, "Đã huỷ");

    /// <summary>Tạo kết quả thất bại với lý do cụ thể.</summary>
    /// <param name="reason">Lý do thất bại.</param>
    /// <returns>Kết quả thất bại.</returns>
    public static MainScreenLocationResult Failed(string reason) => new(false, false, reason);
}

/// <summary>
/// Đưa game về màn hình làng chính, port từ vòng lặp <c>_checkMainScreen()</c> của AutoIt:
/// kiểm tra pixel mốc, phát hiện overlay "reconnecting", restart CoC sau nhiều lần thất bại,
/// chờ màn hình làng (<c>waitMainScreen</c>) và reboot Android nếu cần.
/// </summary>
public sealed class MainScreenRecovery
{
    /// <summary>Package Android của Clash of Clans.</summary>
    public const string CoCPackage = "com.supercell.clashofclans";

    private readonly MainScreenGuard _guard;
    private readonly ITimeProvider _time;
    private readonly IBotLogger _logger;
    private readonly IAndroidDeviceController? _device;
    private readonly MainScreenRecoveryOptions _options;

    /// <summary>Khởi tạo bộ phục hồi màn hình làng chính.</summary>
    /// <param name="guard">Guard dùng để kiểm tra pixel/overlay trên frame.</param>
    /// <param name="time">Nguồn thời gian cho delay và đo thời gian mất mạng.</param>
    /// <param name="logger">Logger ghi nhận quá trình phục hồi.</param>
    /// <param name="device">
    /// Controller ADB để restart CoC/reboot Android; null nếu chỉ chụp qua cửa sổ (GDI).
    /// </param>
    /// <param name="options">Tham số phục hồi; null dùng giá trị mặc định.</param>
    public MainScreenRecovery(
        MainScreenGuard guard,
        ITimeProvider time,
        IBotLogger logger,
        IAndroidDeviceController? device = null,
        MainScreenRecoveryOptions? options = null)
    {
        _guard = guard ?? throw new ArgumentNullException(nameof(guard));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _device = device;
        _options = options ?? new MainScreenRecoveryOptions();
    }

    /// <summary>
    /// Chờ tới khi frame là màn hình làng chính, tự phục hồi khi thất bại.
    /// </summary>
    /// <param name="capture">Backend chụp màn hình đang dùng.</param>
    /// <param name="builderBase">True nếu đang chờ Builder Base thay vì Main Village.</param>
    /// <param name="cancellationToken">Token huỷ.</param>
    /// <returns>Kết quả định vị màn hình.</returns>
    public async Task<MainScreenLocationResult> LocateAsync(
        ICaptureBackend capture,
        bool builderBase = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capture);

        int consecutiveFailures = 0;
        int recoveryCycles = 0;
        int reconnectRestarts = 0;
        DateTime? reconnectSince = null;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using Mat frame = Grab(capture);

                if (!frame.Empty() && _guard.IsCurrentScreen(frame, builderBase))
                {
                    _logger.Info("Đã nhận diện màn hình làng chính.");
                    return MainScreenLocationResult.Success;
                }

                // 5a. Overlay "reconnecting": chờ mạng, restart CoC nếu quá lâu.
                if (!frame.Empty() && _guard.IsReconnecting(frame))
                {
                    reconnectSince ??= _time.UtcNow;
                    TimeSpan elapsed = _time.UtcNow - reconnectSince.Value;

                    if (elapsed.TotalMilliseconds >= _options.ReconnectTimeoutMs)
                    {
                        reconnectSince = null;
                        reconnectRestarts++;

                        if (reconnectRestarts <= _options.MaxReconnectRestarts)
                        {
                            _logger.Error(
                                $"Mất kết nối mạng > {_options.ReconnectTimeoutMs}ms — " +
                                $"khởi động lại CoC ({reconnectRestarts}/{_options.MaxReconnectRestarts}).");
                            RestartCoC();
                        }
                        else
                        {
                            _logger.Error("Mạng vẫn mất sau nhiều lần restart CoC — reboot Android.");
                            reconnectRestarts = 0;
                            RebootDevice();
                        }
                    }
                    else
                    {
                        _logger.Debug($"Đang chờ mạng hồi phục ({elapsed.TotalSeconds:F0}s)...");
                    }

                    await DelayAsync(_options.CheckIntervalMs, cancellationToken);
                    continue;
                }

                reconnectSince = null;
                consecutiveFailures++;

                // 5b. Thất bại quá lâu → chạy chu trình phục hồi sâu.
                if (consecutiveFailures >= _options.MaxChecksBeforeGiveUp)
                {
                    recoveryCycles++;
                    if (recoveryCycles > _options.MaxRecoveryCycles)
                    {
                        _logger.Error("Không thể đưa game về màn hình làng chính sau nhiều lần phục hồi.");
                        return MainScreenLocationResult.Failed(
                            "Không thể đưa game về màn hình làng chính");
                    }

                    _logger.Error("Nhận diện màn hình thất bại — khởi động lại CoC và thử phục hồi.");
                    RestartCoC();

                    if (await WaitForMainScreenAsync(capture, builderBase, cancellationToken))
                    {
                        return MainScreenLocationResult.Success;
                    }

                    if (!await RebootAndWaitAsync(capture, builderBase, cancellationToken))
                    {
                        _logger.Warning("Reboot Android không giúp tìm thấy màn hình làng chính.");
                    }

                    consecutiveFailures = 0;
                    continue;
                }

                // 5c. Chạm ngưỡng restart CoC (20 lần) → restart rồi chờ màn hình làng.
                if (consecutiveFailures == _options.MaxChecksBeforeRestart)
                {
                    _logger.Error("Nhận diện màn hình thất bại — khởi động lại CoC.");
                    RestartCoC();

                    if (await WaitForMainScreenAsync(capture, builderBase, cancellationToken))
                    {
                        return MainScreenLocationResult.Success;
                    }

                    // Chờ thất bại: chuyển thẳng sang ngưỡng phục hồi sâu ở vòng sau.
                    consecutiveFailures = _options.MaxChecksBeforeGiveUp;
                    continue;
                }

                await DelayAsync(_options.CheckIntervalMs, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            return MainScreenLocationResult.CancelledResult;
        }

        return MainScreenLocationResult.CancelledResult;
    }

    /// <summary>
    /// Chờ màn hình làng chính xuất hiện lại, không restart gì thêm.
    /// Tương đương <c>waitMainScreen()</c> nhưng không tự đóng/mở emulator.
    /// </summary>
    /// <param name="capture">Backend chụp màn hình.</param>
    /// <param name="builderBase">True nếu đang chờ Builder Base.</param>
    /// <param name="cancellationToken">Token huỷ.</param>
    /// <returns>True nếu tìm thấy màn hình trong số lần chờ cho phép.</returns>
    public async Task<bool> WaitForMainScreenAsync(
        ICaptureBackend capture,
        bool builderBase,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(capture);

        for (int attempt = 1; attempt <= _options.WaitAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using Mat frame = Grab(capture);
            if (!frame.Empty() && _guard.IsCurrentScreen(frame, builderBase))
            {
                _logger.Info("Đã tìm thấy màn hình làng chính.");
                return true;
            }

            if (attempt % 10 == 0)
            {
                _logger.Debug($"Đang chờ màn hình làng chính: {attempt}/{_options.WaitAttempts}.");
            }

            await DelayAsync(_options.WaitIntervalMs, cancellationToken);
        }

        return false;
    }

    private async Task<bool> RebootAndWaitAsync(
        ICaptureBackend capture,
        bool builderBase,
        CancellationToken cancellationToken)
    {
        if (_device is null)
        {
            _logger.Warning("Không có kết nối ADB — bỏ qua bước reboot Android.");
            return false;
        }

        for (int attempt = 1; attempt <= _options.MaxAndroidReboots; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _logger.Warning(
                $"Không tìm thấy màn hình làng chính — reboot Android " +
                $"({attempt}/{_options.MaxAndroidReboots}).");
            RebootDevice();

            await DelayAsync(_options.RebootDelayMs, cancellationToken);

            if (await WaitForMainScreenAsync(capture, builderBase, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    private void RestartCoC()
    {
        if (_device is null)
        {
            _logger.Warning("Không có kết nối ADB — không thể khởi động lại CoC.");
            return;
        }

        if (_device.RestartApp(CoCPackage))
        {
            _logger.Info("Đã gửi lệnh khởi động lại Clash of Clans.");
        }
        else
        {
            _logger.Warning("Khởi động lại Clash of Clans qua ADB thất bại.");
        }
    }

    private void RebootDevice()
    {
        if (_device is null)
        {
            return;
        }

        if (_device.RebootDevice())
        {
            _logger.Warning("Đã gửi lệnh reboot Android.");
        }
        else
        {
            _logger.Warning("Gửi lệnh reboot Android thất bại.");
        }
    }

    private Mat Grab(ICaptureBackend capture)
    {
        try
        {
            return capture.GrabFrame();
        }
        catch (Exception ex)
        {
            _logger.Warning($"Chụp màn hình thất bại: {ex.Message}");
            return new Mat();
        }
    }

    private Task DelayAsync(int milliseconds, CancellationToken cancellationToken)
    {
        return _time.DelayAsync(milliseconds, cancellationToken);
    }
}
