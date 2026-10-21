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

    public AdbInputBackend(string adbPath, IProcessRunner processRunner)
    {
        _adbPath = adbPath;
        _processRunner = processRunner;
    }

    public AdbInputBackend(string adbPath = "adb")
        : this(adbPath, new ProcessRunner())
    {
    }

    public bool Initialize(IntPtr windowHandle, string adbSerial)
    {
        _adbSerial = adbSerial;
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

    public void Tap(int x, int y)
    {
        if (!_isReady)
        {
            throw new InvalidOperationException("AdbInputBackend chưa được khởi tạo (Initialize).");
        }

        RunAdbShell($"input tap {x} {y}");
    }

    public void Swipe(int x1, int y1, int x2, int y2, int durationMs)
    {
        if (!_isReady)
        {
            throw new InvalidOperationException("AdbInputBackend chưa được khởi tạo (Initialize).");
        }

        RunAdbShell($"input swipe {x1} {y1} {x2} {y2} {durationMs}");
    }

    private void RunAdbShell(string shellCommand)
    {
        try
        {
            _processRunner.Run(_adbPath, $"-s {_adbSerial} shell {shellCommand}", timeoutMs: 2000);
        }
        catch
        {
            // Swallow exceptions hoặc log
        }
    }

    public void Dispose()
    {
        _isReady = false;
    }
}
