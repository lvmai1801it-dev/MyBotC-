using System;
using System.Runtime.InteropServices;
using MyBot.Core.Abstractions;
using MyBot.Core.Utils;

namespace MyBot.Core.Input;

/// <summary>
/// Backend input trên Windows gửi thông điệp chuột (WM_LBUTTONDOWN/UP, WM_MOUSEMOVE)
/// trực tiếp tới cửa sổ giả lập qua <c>PostMessage</c> mà không chiếm chuột thật của người dùng.
/// </summary>
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

    /// <summary>Khởi tạo với nguồn thời gian dùng cho khoảng nghỉ giữa các sự kiện chuột.</summary>
    /// <param name="time">Nguồn thời gian (có thể mock trong test).</param>
    public Win32PostMessageInputBackend(ITimeProvider time)
    {
        _time = time;
    }

    /// <summary>Khởi tạo với nguồn thời gian hệ thống mặc định.</summary>
    public Win32PostMessageInputBackend() : this(new SystemTimeProvider())
    {
    }

    /// <summary>Gắn backend với cửa sổ đích theo <c>target.WindowHandle</c>.</summary>
    /// <param name="target">Thông tin thiết bị đích (bắt buộc có WindowHandle).</param>
    /// <returns>True nếu handle cửa sổ hợp lệ.</returns>
    public bool Initialize(EmulatorTarget target)
    {
        if (target.WindowHandle == null || target.WindowHandle == IntPtr.Zero)
            throw new ArgumentException("Win32PostMessageInputBackend yêu cầu target.WindowHandle");

        _hWnd = target.WindowHandle.Value;
        return _hWnd != IntPtr.Zero;
    }

    /// <summary>Gửi thông điệp chuột nhấn và nhả tại một toạ độ.</summary>
    /// <param name="x">Toạ độ X.</param>
    /// <param name="y">Toạ độ Y.</param>
    public void Tap(int x, int y)
    {
        if (_hWnd == IntPtr.Zero) return;
        IntPtr lParam = MakeLParam(x, y);
        PostMessage(_hWnd, WM_LBUTTONDOWN, (IntPtr)MK_LBUTTON, lParam);
        _time.DelayAsync(30).GetAwaiter().GetResult();
        PostMessage(_hWnd, WM_LBUTTONUP, IntPtr.Zero, lParam);
    }

    /// <summary>Gửi chuỗi sự kiện chuột để mô phỏng thao tác vuốt theo nhiều bước.</summary>
    /// <param name="x1">Toạ độ X điểm bắt đầu.</param>
    /// <param name="y1">Toạ độ Y điểm bắt đầu.</param>
    /// <param name="x2">Toạ độ X điểm kết thúc.</param>
    /// <param name="y2">Toạ độ Y điểm kết thúc.</param>
    /// <param name="durationMs">Tổng thời gian vuốt (ms).</param>
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

    /// <summary>Đóng gói toạ độ (x, y) thành lParam cho thông điệp chuột của Win32.</summary>
    private static IntPtr MakeLParam(int x, int y)
    {
        return (IntPtr)((y << 16) | (x & 0xFFFF));
    }

    /// <summary>Giải phóng handle cửa sổ đang giữ.</summary>
    public void Dispose()
    {
        _hWnd = IntPtr.Zero;
    }
}
