using OpenCvSharp;

namespace MyBot.Core.Abstractions;

public interface ICaptureBackend : IDisposable
{
    bool Initialize(IntPtr windowHandle, string adbSerial);
    Mat GrabFrame();
    bool IsRunning { get; }
}
