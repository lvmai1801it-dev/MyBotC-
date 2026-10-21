using System;
using System.Collections.Generic;
using MyBot.Core.Abstractions;

namespace MyBot.Core.Input;

public class MockInputBackend : IInputBackend
{
    public record RecordedAction(string Type, int X1, int Y1, int X2 = 0, int Y2 = 0, int DurationMs = 0, DateTime Timestamp = default);

    public List<RecordedAction> RecordedActions { get; } = new();

    public bool Initialize(IntPtr windowHandle, string adbSerial = "")
    {
        return true; // Mock luôn sẵn sàng
    }

    public void Tap(int x, int y)
    {
        RecordedActions.Add(new RecordedAction("Tap", x, y, Timestamp: DateTime.UtcNow));
    }

    public void Swipe(int x1, int y1, int x2, int y2, int durationMs)
    {
        RecordedActions.Add(new RecordedAction("Swipe", x1, y1, x2, y2, durationMs, DateTime.UtcNow));
    }

    public void Clear()
    {
        RecordedActions.Clear();
    }

    public void Dispose() { }
}
