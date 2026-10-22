using System;
using System.Runtime.InteropServices;

namespace MyBot.Core.Capture;

/// <summary>
/// Ép cửa sổ giả lập về đúng kích thước khung hình mà OCR/vision đã hiệu chỉnh.
/// </summary>
public static class Win32WindowNormalizer
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;

    /// <summary>
    /// Ép cửa sổ về kích thước đích mà không thay đổi vị trí hay thứ tự Z-order.
    /// </summary>
    /// <param name="hWnd">
    /// Handle cửa sổ cần ép kích thước. Với MEmu nên truyền handle của cửa sổ render
    /// (RenderWindow) vì cửa sổ này không có viền, kích thước đặt trực tiếp chính là
    /// vùng client dùng để chụp màn hình.
    /// </param>
    /// <param name="targetWidth">Chiều rộng đích (mặc định 860).</param>
    /// <param name="targetHeight">Chiều cao đích (mặc định 732).</param>
    public static void EnsureWindowSize(IntPtr hWnd, int targetWidth = 860, int targetHeight = 732)
    {
        if (hWnd == IntPtr.Zero || targetWidth <= 0 || targetHeight <= 0)
        {
            return;
        }

        // Tạm thời ép cứng kích thước client; nếu sau này dùng cửa sổ top-level có viền
        // thì cần cộng thêm phần border/title bar trước khi gọi.
        SetWindowPos(hWnd, IntPtr.Zero, 0, 0, targetWidth, targetHeight, SWP_NOMOVE | SWP_NOZORDER);
    }
}
