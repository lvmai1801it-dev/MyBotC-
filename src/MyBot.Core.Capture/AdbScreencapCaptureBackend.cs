using System;
using OpenCvSharp;
using MyBot.Core.Abstractions;
using MyBot.Core.Utils;

namespace MyBot.Core.Capture;

public class AdbScreencapCaptureBackend : ICaptureBackend
{
    private readonly string _adbPath;
    private readonly IProcessRunner _processRunner;
    private string _adbSerial = string.Empty;
    private bool _isRunning;

    public AdbScreencapCaptureBackend(string adbPath, IProcessRunner processRunner)
    {
        _adbPath = adbPath;
        _processRunner = processRunner;
    }

    public AdbScreencapCaptureBackend(string adbPath = "adb")
        : this(adbPath, new ProcessRunner())
    {
    }

    public bool IsRunning => _isRunning;

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

    public void Dispose()
    {
        _isRunning = false;
    }
}
