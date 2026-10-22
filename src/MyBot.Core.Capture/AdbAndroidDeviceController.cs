using System;
using MyBot.Core.Abstractions;

namespace MyBot.Core.Capture;

/// <summary>
/// Điều khiển ứng dụng/thiết bị Android qua ADB: force-stop + mở lại Clash of Clans,
/// hoặc reboot giả lập. Dùng bởi <c>MainScreenRecovery</c> khi bot không thể đưa game
/// về màn hình làng chính.
/// </summary>
public sealed class AdbAndroidDeviceController : IAndroidDeviceController
{
    private const int QuickTimeoutMs = 5000;
    private const int LaunchTimeoutMs = 10000;

    private readonly string _adbPath;
    private readonly IProcessRunner _processRunner;
    private readonly string _adbSerial;

    /// <summary>Khởi tạo controller cho một thiết bị ADB cụ thể.</summary>
    /// <param name="adbPath">Đường dẫn tới file thực thi adb.</param>
    /// <param name="processRunner">Bộ chạy tiến trình dùng để gọi adb.</param>
    /// <param name="adbSerial">Serial thiết bị, ví dụ <c>127.0.0.1:21503</c>.</param>
    public AdbAndroidDeviceController(string adbPath, IProcessRunner processRunner, string adbSerial)
    {
        if (string.IsNullOrWhiteSpace(adbSerial))
        {
            throw new ArgumentException("AdbAndroidDeviceController yêu cầu adbSerial", nameof(adbSerial));
        }

        _adbPath = adbPath;
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _adbSerial = adbSerial;
    }

    /// <summary>
    /// Force-stop rồi mở lại ứng dụng qua launcher (monkey), tương đương
    /// <c>CloseCoC()</c> + <c>OpenCoC()</c> của bản AutoIt.
    /// </summary>
    /// <param name="packageName">Tên package Android cần mở lại.</param>
    /// <returns>True nếu lệnh launch trả về thành công.</returns>
    public bool RestartApp(string packageName)
    {
        if (string.IsNullOrWhiteSpace(packageName))
        {
            return false;
        }

        Run($"shell am force-stop {packageName}", QuickTimeoutMs);

        return Run(
            $"shell monkey -p {packageName} -c android.intent.category.LAUNCHER 1",
            LaunchTimeoutMs);
    }

    /// <summary>Gửi lệnh <c>adb reboot</c> tới thiết bị.</summary>
    /// <returns>True nếu adb chấp nhận lệnh.</returns>
    public bool RebootDevice()
    {
        return Run("reboot", QuickTimeoutMs);
    }

    private bool Run(string arguments, int timeoutMs)
    {
        try
        {
            return _processRunner.Run(_adbPath, $"-s {_adbSerial} {arguments}", timeoutMs).IsSuccess;
        }
        catch
        {
            // ADB mất kết nối/timeout: caller sẽ thử lại hoặc reboot.
            return false;
        }
    }
}
