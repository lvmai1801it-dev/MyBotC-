namespace MyBot.Core.Abstractions;

public interface IInputBackend : IDisposable
{
    bool Initialize(IntPtr windowHandle, string adbSerial);
    void Tap(int x, int y);
    void Swipe(int x1, int y1, int x2, int y2, int durationMs);
}
