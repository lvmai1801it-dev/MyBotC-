using System;
using OpenCvSharp;
using MyBot.Core.Abstractions;
using MyBot.Core.Utils;

namespace MyBot.Core.Capture;

/// <summary>
/// Backend chụp màn hình giả lập Android qua lệnh <c>adb exec-out screencap -p</c>,
/// hoạt động cả khi giả lập chạy headless (không cần cửa sổ).
/// </summary>
public class AdbScreencapCaptureBackend : ICaptureBackend
{
    private readonly string _adbPath;
    private readonly IProcessRunner _processRunner;
    private string _adbSerial = string.Empty;
    private bool _isRunning;

    /// <summary>Khởi tạo với đường dẫn adb và bộ chạy tiến trình do caller cung cấp.</summary>
    /// <param name="adbPath">Đường dẫn tới file thực thi adb.</param>
    /// <param name="processRunner">Bộ chạy tiến trình dùng để gọi adb.</param>
    public AdbScreencapCaptureBackend(string adbPath, IProcessRunner processRunner)
    {
        _adbPath = adbPath;
        _processRunner = processRunner;
    }

    /// <summary>Khởi tạo với đường dẫn adb (mặc định "adb" trong PATH).</summary>
    /// <param name="adbPath">Đường dẫn tới file thực thi adb.</param>
    public AdbScreencapCaptureBackend(string adbPath = "adb")
        : this(adbPath, new ProcessRunner())
    {
    }

    /// <summary>Cho biết backend đã kết nối được với thiết bị hay chưa.</summary>
    public bool IsRunning => _isRunning;

    /// <summary>Kiểm tra kết nối adb tới thiết bị theo <c>target.AdbSerial</c>.</summary>
    /// <param name="target">Thông tin thiết bị đích (bắt buộc có AdbSerial).</param>
    /// <returns>True nếu thiết bị ở trạng thái "device".</returns>
    public bool Initialize(EmulatorTarget target)
    {
        if (string.IsNullOrWhiteSpace(target.AdbSerial))
            throw new ArgumentException("AdbScreencapCaptureBackend yêu cầu target.AdbSerial");

        _adbSerial = target.AdbSerial;
        try
        {
            var result = _processRunner.Run(_adbPath, $"-s {_adbSerial} get-state", timeoutMs: 2000);
            _isRunning = result.IsSuccess && result.StdOut.Trim() == "device";
        }
        catch
        {
            _isRunning = false;
        }

        return _isRunning;
    }

    /// <summary>Chụp một khung hình từ thiết bị và giải mã thành <see cref="Mat"/>.</summary>
    /// <returns>Khung hình hiện tại, hoặc Mat rỗng nếu chưa sẵn sàng/lỗi.</returns>
    public Mat GrabFrame()
    {
        if (!_isRunning) return new Mat();

        try
        {
            var result = _processRunner.Run(_adbPath, $"-s {_adbSerial} exec-out screencap -p", timeoutMs: 3000);
            if (!result.IsSuccess || result.RawStdOut.Length == 0)
            {
                return new Mat();
            }

            return Cv2.ImDecode(result.RawStdOut, ImreadModes.Color);
        }
        catch
        {
            return new Mat();
        }
    }

    /// <summary>Đánh dấu backend ngừng hoạt động và giải phóng tài nguyên.</summary>
    public void Dispose()
    {
        _isRunning = false;
    }
}
