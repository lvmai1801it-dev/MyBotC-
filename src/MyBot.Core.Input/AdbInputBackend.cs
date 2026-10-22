using System;
using MyBot.Core.Abstractions;
using MyBot.Core.Utils;

namespace MyBot.Core.Input;

/// <summary>
/// Gửi lệnh touch/swipe qua ADB shell input thông qua IProcessRunner.
/// Hoạt động bất chấp giả lập có cửa sổ hay headless.
/// </summary>
public class AdbInputBackend : IInputBackend
{
    private readonly string _adbPath;
    private readonly IProcessRunner _processRunner;
    private string _adbSerial = string.Empty;
    private bool _isReady;

    /// <summary>Khởi tạo với đường dẫn adb và bộ chạy tiến trình do caller cung cấp.</summary>
    /// <param name="adbPath">Đường dẫn tới file thực thi adb.</param>
    /// <param name="processRunner">Bộ chạy tiến trình dùng để gọi adb.</param>
    public AdbInputBackend(string adbPath, IProcessRunner processRunner)
    {
        _adbPath = adbPath;
        _processRunner = processRunner;
    }

    /// <summary>Khởi tạo với đường dẫn adb (mặc định "adb" trong PATH).</summary>
    /// <param name="adbPath">Đường dẫn tới file thực thi adb.</param>
    public AdbInputBackend(string adbPath = "adb")
        : this(adbPath, new ProcessRunner())
    {
    }

    /// <summary>Kiểm tra kết nối adb tới thiết bị theo <c>target.AdbSerial</c>.</summary>
    /// <param name="target">Thông tin thiết bị đích (bắt buộc có AdbSerial).</param>
    /// <returns>True nếu thiết bị ở trạng thái "device".</returns>
    public bool Initialize(EmulatorTarget target)
    {
        if (string.IsNullOrWhiteSpace(target.AdbSerial))
            throw new ArgumentException("AdbInputBackend yêu cầu target.AdbSerial");

        _adbSerial = target.AdbSerial;
        try
        {
            var result = _processRunner.Run(_adbPath, $"-s {_adbSerial} get-state", timeoutMs: 2000);
            _isReady = result.IsSuccess && result.StdOut.Trim() == "device";
        }
        catch
        {
            _isReady = false;
        }

        return _isReady;
    }

    /// <summary>Gửi một lần chạm tại toạ độ chỉ định.</summary>
    /// <param name="x">Toạ độ X.</param>
    /// <param name="y">Toạ độ Y.</param>
    public void Tap(int x, int y)
    {
        if (!_isReady)
        {
            throw new InvalidOperationException("AdbInputBackend chưa được khởi tạo (Initialize).");
        }

        RunAdbShell($"input tap {x} {y}");
    }

    /// <summary>Gửi thao tác vuốt từ điểm đầu tới điểm cuối trong khoảng thời gian chỉ định.</summary>
    /// <param name="x1">Toạ độ X điểm bắt đầu.</param>
    /// <param name="y1">Toạ độ Y điểm bắt đầu.</param>
    /// <param name="x2">Toạ độ X điểm kết thúc.</param>
    /// <param name="y2">Toạ độ Y điểm kết thúc.</param>
    /// <param name="durationMs">Thời gian vuốt (ms).</param>
    public void Swipe(int x1, int y1, int x2, int y2, int durationMs)
    {
        if (!_isReady)
        {
            throw new InvalidOperationException("AdbInputBackend chưa được khởi tạo (Initialize).");
        }

        RunAdbShell($"input swipe {x1} {y1} {x2} {y2} {durationMs}");
    }

    /// <summary>
    /// Gửi thao tác con lăn qua <c>input roll</c> (Android 11+). Đây là cách duy nhất
    /// ADB hỗ trợ zoom; máy Android cũ sẽ trả false để caller biết zoom không khả dụng.
    /// </summary>
    /// <param name="x">Toạ độ X (không dùng — roll là lệnh toàn cục).</param>
    /// <param name="y">Toạ độ Y (không dùng — roll là lệnh toàn cục).</param>
    /// <param name="delta">Độ lớn con lăn; âm = lăn xuống (zoom out).</param>
    /// <returns>True nếu thiết bị chấp nhận lệnh.</returns>
    public bool Scroll(int x, int y, int delta)
    {
        if (!_isReady)
        {
            return false;
        }

        return RunAdbShell($"input roll 0 {delta}");
    }

    private bool RunAdbShell(string shellCommand)
    {
        try
        {
            return _processRunner.Run(_adbPath, $"-s {_adbSerial} shell {shellCommand}", timeoutMs: 2000).IsSuccess;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Đánh dấu backend ngừng hoạt động.</summary>
    public void Dispose()
    {
        _isReady = false;
    }
}
