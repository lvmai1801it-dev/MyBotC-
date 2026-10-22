using System;
using System.Runtime.InteropServices;
using MyBot.Core.Abstractions;
using MyBot.Core.Utils;

namespace MyBot.Core.Input;

public class Win32PostMessageInputBackend : IInputBackend
{
    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const uint WM_LBUTTONDOWN = 0x0201;
    private const uint WM_LBUTTONUP = 0x0202;
    private const uint WM_MOUSEMOVE = 0x0200;
    private const int MK_LBUTTON = 0x0001;

    private readonly ITimeProvider _time;
    private IntPtr _hWnd = IntPtr.Zero;

    public Win32PostMessageInputBackend(ITimeProvider time)
    {
        _time = time;
    }

    public Win32PostMessageInputBackend() : this(new SystemTimeProvider())
    {
    }

    public bool Initialize(EmulatorTarget target)
    {
        if (target.WindowHandle == null || target.WindowHandle == IntPtr.Zero)
            throw new ArgumentException("Win32PostMessageInputBackend yêu cầu target.WindowHandle");

        _hWnd = target.WindowHandle.Value;
        return _hWnd != IntPtr.Zero;
    }

    public void Tap(int x, int y)
    {
        if (_hWnd == IntPtr.Zero) return;
        IntPtr lParam = MakeLParam(x, y);
        PostMessage(_hWnd, WM_LBUTTONDOWN, (IntPtr)MK_LBUTTON, lParam);
        _time.DelayAsync(30).GetAwaiter().GetResult();
        PostMessage(_hWnd, WM_LBUTTONUP, IntPtr.Zero, lParam);
    }

    public void Swipe(int x1, int y1, int x2, int y2, int durationMs)
    {
        if (_hWnd == IntPtr.Zero) return;
        IntPtr startLParam = MakeLParam(x1, y1);
        IntPtr endLParam = MakeLParam(x2, y2);

        PostMessage(_hWnd, WM_LBUTTONDOWN, (IntPtr)MK_LBUTTON, startLParam);
        
        int steps = Math.Max(5, durationMs / 20);
        int stepDelay = durationMs / steps;

        for (int i = 1; i <= steps; i++)
        {
            int curX = x1 + (x2 - x1) * i / steps;
            int curY = y1 + (y2 - y1) * i / steps;
            PostMessage(_hWnd, WM_MOUSEMOVE, (IntPtr)MK_LBUTTON, MakeLParam(curX, curY));
            _time.DelayAsync(stepDelay).GetAwaiter().GetResult();
        }

        PostMessage(_hWnd, WM_LBUTTONUP, IntPtr.Zero, endLParam);
    }

    private static IntPtr MakeLParam(int x, int y)
    {
        return (IntPtr)((y << 16) | (x & 0xFFFF));
    }

    public void Dispose()
    {
        _hWnd = IntPtr.Zero;
    }
}
