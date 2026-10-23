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

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    private const uint WM_LBUTTONDOWN = 0x0201;
    private const uint WM_LBUTTONUP = 0x0202;
    private const uint WM_MOUSEMOVE = 0x0200;
    private const uint WM_MOUSEWHEEL = 0x020A;
    private const uint WM_KEYDOWN = 0x0100;
    private const uint WM_KEYUP = 0x0101;
    private const int MK_LBUTTON = 0x0001;
    private const int MK_CONTROL = 0x0008;
    private const int VK_CONTROL = 0x11;

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

    /// <summary>
    /// Gửi một nấc con lăn chuột tới cửa sổ game. Dùng để zoom out camera
    /// (<c>ZoomOut()</c> của bản AutoIt) mà không chiếm chuột thật.
    /// </summary>
    /// <param name="x">Toạ độ X theo vùng client của cửa sổ.</param>
    /// <param name="y">Toạ độ Y theo vùng client của cửa sổ.</param>
    /// <param name="delta">Độ lớn con lăn; âm = lăn xuống (zoom out).</param>
    /// <returns>True nếu thông điệp được gửi thành công.</returns>
    public bool Scroll(int x, int y, int delta)
    {
        return Scroll(x, y, delta, control: false);
    }

    /// <summary>
    /// Gửi một nấc con lăn, có thể kèm Ctrl. MEmu coi Ctrl + lăn xuống là lệnh
    /// zoom out dự phòng (tương đương <c>ZoomOutCtrlWheelScroll()</c> của AutoIt).
    /// </summary>
    /// <param name="x">Toạ độ X theo vùng client của cửa sổ.</param>
    /// <param name="y">Toạ độ Y theo vùng client của cửa sổ.</param>
    /// <param name="delta">Độ lớn con lăn; âm = lăn xuống (zoom out).</param>
    /// <param name="control">True để giữ Ctrl trong lúc lăn.</param>
    /// <returns>True nếu thông điệp con lăn được gửi thành công.</returns>
    public bool Scroll(int x, int y, int delta, bool control)
    {
        if (_hWnd == IntPtr.Zero) return false;

        // WM_MOUSEWHEEL yêu cầu toạ độ màn hình trong lParam và trạng thái phím
        // ở word thấp của wParam.
        var point = new POINT { X = x, Y = y };
        ClientToScreen(_hWnd, ref point);

        IntPtr lParam = MakeLParam(point.X, point.Y);
        IntPtr wParam = (IntPtr)((delta << 16) | (control ? MK_CONTROL : 0));

        if (!control)
        {
            return PostMessage(_hWnd, WM_MOUSEWHEEL, wParam, lParam);
        }

        // Gửi kèm WM_KEYDOWN/WM_KEYUP để Qt/MEmu thấy Ctrl đang được giữ,
        // không cần chiếm bàn phím thật của người dùng.
        PostMessage(_hWnd, WM_KEYDOWN, (IntPtr)VK_CONTROL, (IntPtr)0x001D0001);
        bool posted = PostMessage(_hWnd, WM_MOUSEWHEEL, wParam, lParam);
        PostMessage(_hWnd, WM_KEYUP, (IntPtr)VK_CONTROL, unchecked((IntPtr)0xC01D0001));
        return posted;
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
