using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyBot.Core.Abstractions;

namespace MyBot.Core.Capture;

/// <summary>
/// Tuỳ chọn cho quá trình kiểm tra/khởi động giả lập.
/// </summary>
public sealed class EmulatorStartupOptions
{
    /// <summary>Thời gian tối đa chờ ADB nhận thiết bị (ms).</summary>
    public int AdbReadyTimeoutMs { get; init; } = 60_000;

    /// <summary>Thời gian tối đa chờ Android khởi động xong (ms).</summary>
    public int BootTimeoutMs { get; init; } = 120_000;

    /// <summary>Khoảng nghỉ giữa hai lần kiểm tra (ms).</summary>
    public int PollIntervalMs { get; init; } = 1_000;

    /// <summary>Package Clash of Clans cần mở sau khi giả lập sẵn sàng.</summary>
    public string CoCPackage { get; init; } = "com.supercell.clashofclans";
}

/// <summary>
/// Kết quả của <see cref="EmulatorStartup.EnsureRunningAsync"/>.
/// </summary>
/// <param name="Ready">True nếu ADB (và game, khi vừa khởi động) đã sẵn sàng.</param>
/// <param name="Launched">True nếu lần này phải khởi động giả lập.</param>
/// <param name="EmulatorType">Loại giả lập được chọn (để lưu lại profile).</param>
/// <param name="InstanceName">Instance được chọn (để lưu lại profile).</param>
/// <param name="AdbPort">Cổng ADB hiệu lực; 0 nếu không xác định được.</param>
/// <param name="Reason">Lý do thất bại; null khi thành công.</param>
public sealed record EmulatorStartupResult(
    bool Ready,
    bool Launched,
    string? EmulatorType,
    string? InstanceName,
    int AdbPort,
    string? Reason)
{
    /// <summary>Tạo kết quả thành công.</summary>
    /// <param name="launched">Có phải vừa khởi động giả lập hay không.</param>
    /// <param name="emulatorType">Loại giả lập.</param>
    /// <param name="instanceName">Tên instance.</param>
    /// <param name="adbPort">Cổng ADB hiệu lực.</param>
    /// <returns>Kết quả thành công.</returns>
    public static EmulatorStartupResult Success(
        bool launched,
        string? emulatorType,
        string? instanceName,
        int adbPort) =>
        new(true, launched, emulatorType, instanceName, adbPort, null);

    /// <summary>Tạo kết quả thất bại.</summary>
    /// <param name="reason">Lý do thất bại.</param>
    /// <returns>Kết quả thất bại.</returns>
    public static EmulatorStartupResult Failed(string reason) =>
        new(false, false, null, null, 0, reason);
}

/// <summary>
/// Kiểm tra giả lập đang chạy hay chưa, khởi động khi cần và chờ ADB/Android sẵn sàng.
/// Tương đương phần đầu của <c>BotStart()</c>: <c>WinGetAndroidHandle()</c> →
/// <c>OpenAndroid()</c> → <c>InitAndroid()</c> → <c>StartAndroidCoC()</c>.
/// </summary>
/// <remarks>
/// Việc chờ màn hình làng và zoom out vẫn do vòng lặp bot đảm nhận
/// (<c>MainScreenRecovery</c> + <c>CameraZoomer</c>).
/// </remarks>
public sealed class EmulatorStartup
{
    private readonly IEmulatorDriver _driver;
    private readonly IProcessRunner _processRunner;
    private readonly IBotLogger _logger;
    private readonly ITimeProvider _time;
    private readonly Func<string, IAndroidDeviceController>? _deviceFactory;
    private readonly EmulatorStartupOptions _options;

    /// <summary>Khởi tạo dịch vụ khởi động giả lập.</summary>
    /// <param name="driver">Driver dò/khởi động giả lập.</param>
    /// <param name="processRunner">Bộ chạy tiến trình dùng cho các lệnh ADB.</param>
    /// <param name="logger">Logger của bot.</param>
    /// <param name="time">Nguồn thời gian (mock được trong test).</param>
    /// <param name="deviceFactory">
    /// Hàm tạo controller Android từ serial hiệu lực; dùng để mở Clash of Clans.
    /// Null = không mở game.
    /// </param>
    /// <param name="options">Tuỳ chọn timeout; null = mặc định.</param>
    public EmulatorStartup(
        IEmulatorDriver driver,
        IProcessRunner processRunner,
        IBotLogger logger,
        ITimeProvider time,
        Func<string, IAndroidDeviceController>? deviceFactory = null,
        EmulatorStartupOptions? options = null)
    {
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _deviceFactory = deviceFactory;
        _options = options ?? new EmulatorStartupOptions();
    }

    /// <summary>
    /// Bảo đảm giả lập đang chạy và ADB sẵn sàng.
    /// </summary>
    /// <param name="adbPath">Đường dẫn adb.exe.</param>
    /// <param name="adbSerial">Serial ADB, ví dụ <c>127.0.0.1:21503</c>.</param>
    /// <param name="emulatorType">Loại giả lập đã lưu trong profile; rỗng = tự dò lần đầu.</param>
    /// <param name="instanceName">Instance đã lưu trong profile; rỗng = tự dò lần đầu.</param>
    /// <param name="cancellationToken">Token huỷ.</param>
    /// <returns>Kết quả cho biết giả lập đã sẵn sàng hay chưa.</returns>
    public async Task<EmulatorStartupResult> EnsureRunningAsync(
        string adbPath,
        string adbSerial,
        string? emulatorType,
        string? instanceName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adbPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(adbSerial);
        cancellationToken.ThrowIfCancellationRequested();

        EmulatorInstance? running = SelectRunning(
            _driver.DetectRunningInstances(),
            emulatorType,
            instanceName);

        bool launched = false;
        EmulatorInstallation? installation = null;

        if (running is null)
        {
            installation = SelectInstallation(
                _driver.DetectInstalled(adbPath),
                emulatorType,
                instanceName);

            if (installation is null)
            {
                return EmulatorStartupResult.Failed(
                    "Không tìm thấy giả lập đang chạy hoặc đã cài. " +
                    "Kiểm tra Emulator Type/Instance trong tab Android.");
            }

            _logger.Info($"Khởi động {installation.EmulatorType} ({installation.InstanceName})...");
            if (!_driver.Launch(installation.InstanceName, adbPath))
            {
                return EmulatorStartupResult.Failed(
                    $"Không khởi động được {installation.EmulatorType} tại {installation.ExecutablePath}.");
            }

            launched = true;
        }
        else
        {
            _logger.Info($"Dùng lại {running.Name} (handle 0x{running.WindowHandle.ToInt64():X}).");
        }

        string effectiveSerial = ResolveSerial(adbSerial, running, installation, emulatorType);

        if (!await WaitForAdbAsync(adbPath, effectiveSerial, cancellationToken).ConfigureAwait(false))
        {
            return EmulatorStartupResult.Failed($"ADB không sẵn sàng tại {effectiveSerial}.");
        }

        if (launched)
        {
            if (!await WaitForBootCompletedAsync(adbPath, effectiveSerial, cancellationToken).ConfigureAwait(false))
            {
                return EmulatorStartupResult.Failed("Giả lập chưa khởi động xong (sys.boot_completed).");
            }

            if (_deviceFactory is null)
            {
                _logger.Warning("Không có Android device controller — bỏ qua bước mở Clash of Clans.");
            }
            else if (!_deviceFactory(effectiveSerial).RestartApp(_options.CoCPackage))
            {
                return EmulatorStartupResult.Failed("Không mở được Clash of Clans.");
            }
        }

        string? selectedType = running?.EmulatorType ?? installation?.EmulatorType;
        string? selectedInstance = running?.EmulatorType is { Length: > 0 } type
            ? ExtractInstanceName(running.Name, type)
            : installation?.InstanceName;

        return EmulatorStartupResult.Success(
            launched,
            selectedType,
            selectedInstance,
            ParsePort(effectiveSerial));
    }

    /// <summary>
    /// Chọn instance đang chạy khớp profile. Profile có cấu hình thì không dùng
    /// instance của giả lập khác; profile rỗng (lần đầu) thì lấy instance đầu tiên.
    /// </summary>
    private static EmulatorInstance? SelectRunning(
        IReadOnlyList<EmulatorInstance> running,
        string? emulatorType,
        string? instanceName)
    {
        if (running.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(instanceName))
        {
            EmulatorInstance? byInstance = running.FirstOrDefault(candidate =>
                candidate.Name.Contains(instanceName, StringComparison.OrdinalIgnoreCase));
            if (byInstance is not null)
            {
                return byInstance;
            }

            // Profile đã chỉ định instance: không dùng instance của giả lập khác.
            return string.IsNullOrWhiteSpace(emulatorType)
                ? null
                : running.FirstOrDefault(candidate =>
                    string.Equals(candidate.EmulatorType, emulatorType, StringComparison.OrdinalIgnoreCase));
        }

        // Profile chưa có instance (lần đầu): ưu tiên loại đang chọn, nếu không có thì dò.
        if (!string.IsNullOrWhiteSpace(emulatorType))
        {
            EmulatorInstance? byType = running.FirstOrDefault(candidate =>
                string.Equals(candidate.EmulatorType, emulatorType, StringComparison.OrdinalIgnoreCase));
            if (byType is not null)
            {
                return byType;
            }
        }

        return running[0];
    }

    /// <summary>Chọn bản cài khớp profile; profile rỗng thì lấy bản cài đầu tiên.</summary>
    private static EmulatorInstallation? SelectInstallation(
        IReadOnlyList<EmulatorInstallation> installed,
        string? emulatorType,
        string? instanceName)
    {
        if (installed.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(instanceName))
        {
            EmulatorInstallation? byInstance = installed.FirstOrDefault(candidate =>
                string.Equals(candidate.InstanceName, instanceName, StringComparison.OrdinalIgnoreCase));
            if (byInstance is not null)
            {
                return byInstance;
            }

            // Profile đã chỉ định instance: chỉ chấp nhận đúng loại giả lập đó.
            return string.IsNullOrWhiteSpace(emulatorType)
                ? null
                : installed.FirstOrDefault(candidate =>
                    string.Equals(candidate.EmulatorType, emulatorType, StringComparison.OrdinalIgnoreCase));
        }

        // Profile chưa có instance (lần đầu): ưu tiên loại đang chọn, nếu không có thì dò.
        if (!string.IsNullOrWhiteSpace(emulatorType))
        {
            EmulatorInstallation? byType = installed.FirstOrDefault(candidate =>
                string.Equals(candidate.EmulatorType, emulatorType, StringComparison.OrdinalIgnoreCase));
            if (byType is not null)
            {
                return byType;
            }
        }

        return installed[0];
    }

    /// <summary>Chờ <c>adb get-state</c> trả về <c>device</c>.</summary>
    private async Task<bool> WaitForAdbAsync(
        string adbPath,
        string adbSerial,
        CancellationToken cancellationToken)
    {
        // Với giả lập kết nối qua TCP, chủ động connect trước khi kiểm tra.
        if (adbSerial.Contains(':'))
        {
            _ = RunAdb(adbPath, $"connect {adbSerial}", 5000);
        }

        DateTime deadline = _time.UtcNow.AddMilliseconds(_options.AdbReadyTimeoutMs);
        while (_time.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ProcessResult? result = RunAdb(adbPath, $"-s {adbSerial} get-state");
            if (result is { IsSuccess: true } && result.StdOut.Trim() == "device")
            {
                return true;
            }

            await _time.DelayAsync(_options.PollIntervalMs, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    /// <summary>Chờ Android báo <c>sys.boot_completed=1</c>.</summary>
    private async Task<bool> WaitForBootCompletedAsync(
        string adbPath,
        string adbSerial,
        CancellationToken cancellationToken)
    {
        DateTime deadline = _time.UtcNow.AddMilliseconds(_options.BootTimeoutMs);
        while (_time.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ProcessResult? result = RunAdb(
                adbPath,
                $"-s {adbSerial} shell getprop sys.boot_completed");
            if (result is { IsSuccess: true } && result.StdOut.Trim() == "1")
            {
                return true;
            }

            await _time.DelayAsync(_options.PollIntervalMs, cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    /// <summary>Chạy lệnh adb, nuốt exception để vòng chờ không chết vì lỗi tạm thời.</summary>
    private ProcessResult? RunAdb(string adbPath, string arguments, int timeoutMs = 2000)
    {
        try
        {
            return _processRunner.Run(adbPath, arguments, timeoutMs);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Xác định serial ADB hiệu lực. Khi loại giả lập vừa dò khác loại trong profile
    /// (hoặc profile chưa có loại), dùng cổng mặc định của giả lập đó.
    /// </summary>
    private static string ResolveSerial(
        string adbSerial,
        EmulatorInstance? running,
        EmulatorInstallation? installation,
        string? emulatorType)
    {
        int detectedPort = running?.AdbPort ?? installation?.AdbPort ?? 0;
        if (detectedPort <= 0)
        {
            return adbSerial;
        }

        string? selectedType = running?.EmulatorType ?? installation?.EmulatorType;
        bool typeChanged = !string.IsNullOrWhiteSpace(emulatorType)
            && !string.Equals(selectedType, emulatorType, StringComparison.OrdinalIgnoreCase);

        // Cùng loại giả lập thì giữ cổng người dùng đã cấu hình (instance/port riêng).
        return typeChanged || string.IsNullOrWhiteSpace(emulatorType)
            ? $"127.0.0.1:{detectedPort}"
            : adbSerial;
    }

    /// <summary>Lấy cổng từ serial dạng <c>127.0.0.1:21503</c>; 0 nếu không có.</summary>
    private static int ParsePort(string adbSerial)
    {
        int separator = adbSerial.LastIndexOf(':');
        return separator >= 0 && int.TryParse(adbSerial[(separator + 1)..], out int port)
            ? port
            : 0;
    }

    /// <summary>Tách tên instance khỏi tên hiển thị dạng <c>{type} - {title}</c>.</summary>
    private static string ExtractInstanceName(string displayName, string emulatorType)
    {
        string prefix = emulatorType + " - ";
        return displayName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? displayName[prefix.Length..]
            : displayName;
    }
}
