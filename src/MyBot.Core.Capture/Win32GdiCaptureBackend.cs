using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using OpenCvSharp;
using MyBot.Core.Abstractions;

namespace MyBot.Core.Capture;

public class Win32GdiCaptureBackend : ICaptureBackend
{
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
    
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hDC);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int nWidth, int nHeight);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr hObject, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hObjectSource, int nXSrc, int nYSrc, uint dwRop);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hDC);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    private const uint SRCCOPY = 0x00CC0020;

    private IntPtr _hWnd = IntPtr.Zero;
    private int _width;
    private int _height;
    private bool _isRunning;

    public bool IsRunning => _isRunning;

    public bool Initialize(EmulatorTarget target)
    {
        if (target.WindowHandle == null || target.WindowHandle == IntPtr.Zero)
            throw new ArgumentException("Win32GdiCaptureBackend yêu cầu target.WindowHandle");

        _hWnd = target.WindowHandle.Value;

        // Tự động lấy kích thước thực tế của vùng Client Area (không có title bar)
        if (!GetClientRect(_hWnd, out RECT rect)) return false;
        
        _width = rect.Right - rect.Left;
        _height = rect.Bottom - rect.Top;

        if (_width <= 0 || _height <= 0) return false;

        _isRunning = true;
        return true;
    }

    public Mat GrabFrame()
    {
        if (!_isRunning || _hWnd == IntPtr.Zero) 
            return new Mat();

        // Lấy DC của vùng Client Area (chỉ lấy phần game, không lấy viền)
        IntPtr hWndDC = GetDC(_hWnd);
        if (hWndDC == IntPtr.Zero) return new Mat();

        IntPtr hMemDC = CreateCompatibleDC(hWndDC);
        IntPtr hBitmap = CreateCompatibleBitmap(hWndDC, _width, _height);
        IntPtr hOld = SelectObject(hMemDC, hBitmap);

        try
        {
            BitBlt(hMemDC, 0, 0, _width, _height, hWndDC, 0, 0, SRCCOPY);

            // Chuyển sang Mat của OpenCV
            using var tempBitmap = Image.FromHbitmap(hBitmap);
            using var mat = OpenCvSharp.Extensions.BitmapConverter.ToMat(tempBitmap);
            return mat.Clone();
        }
        catch
        {
            return new Mat();
        }
        finally
        {
            SelectObject(hMemDC, hOld);
            DeleteObject(hBitmap);
            DeleteDC(hMemDC);
            ReleaseDC(_hWnd, hWndDC);
        }
    }

    public void Dispose() => _isRunning = false;
}
