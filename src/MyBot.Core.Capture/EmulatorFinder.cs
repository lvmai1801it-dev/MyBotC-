using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace MyBot.Core.Capture;

/// <summary>
/// Dò cửa sổ giả lập Android đang chạy bằng Win32.
/// Lớp cửa sổ lấy theo bảng cấu hình của bản AutoIt:
/// MEmu dùng <c>subWin</c>, BlueStacks 5 dùng <c>BlueStacksApp</c>.
/// </summary>
public static class EmulatorFinder
{
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string? lpszClass, string? lpszWindow);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    /// <summary>Kích thước khung hình chuẩn của bot (860x732).</summary>
    private const int RenderWidth = 860;
    private const int RenderHeight = 732;

    /// <summary>Tiêu đề cửa sổ render của MEmu 9 (Qt).</summary>
    private const string MEmuRenderTitle = "RenderWindowWindow";

    /// <summary>Một cửa sổ giả lập tìm thấy.</summary>
    /// <param name="EmulatorType">Loại giả lập, ví dụ <c>MEmu</c>.</param>
    /// <param name="ClassName">Tên lớp Win32 của cửa sổ.</param>
    /// <param name="Title">Tiêu đề cửa sổ.</param>
    /// <param name="Handle">Handle cửa sổ render dùng để chụp/input.</param>
    public record EmulatorWindow(string EmulatorType, string ClassName, string Title, IntPtr Handle);

    private sealed record WindowSpec(string EmulatorType, string ClassName, string TitleContains);

    // PHMOD chỉ hỗ trợ MEmu và BlueStacks5 (xem $g_avAndroidAppConfig của bản AutoIt).
    private static readonly WindowSpec[] Specs =
    {
        new("MEmu", "subWin", "MEmu"),
        new("BlueStacks5", "BlueStacksApp", "BlueStacks"),
    };

    /// <summary>Liệt kê các cửa sổ giả lập đang chạy.</summary>
    /// <param name="debugPrintAll">True để in mọi cửa sổ có tiêu đề (chẩn đoán).</param>
    /// <returns>Danh sách cửa sổ khớp cấu hình giả lập được hỗ trợ.</returns>
    public static List<EmulatorWindow> FindAll(bool debugPrintAll = false)
    {
        var results = new List<EmulatorWindow>();

        EnumWindows((hWnd, lParam) =>
        {
            string className = ReadClassName(hWnd);
            string title = ReadTitle(hWnd);

            if (debugPrintAll && !string.IsNullOrWhiteSpace(title))
            {
                Console.WriteLine($"[DEBUG] Handle: 0x{hWnd.ToInt64():X} | Class: {className} | Title: {title}");
            }

            // MEmu/BlueStacks5 phải khớp đúng lớp cửa sổ render; tránh nhận nhầm
            // các cửa sổ phụ như "MEmuPowerNotifyClass".
            string? emulatorType = MatchEmulator(className, title);
            IntPtr handle = hWnd;

            if (emulatorType == "MEmu" && IsQtClass(className))
            {
                // MEmu 9 dùng cửa sổ Qt; lớp "subWin" chỉ còn ở bản cũ.
                // Cửa sổ render 860x732 là con của cửa sổ Qt, không phải top-level.
                handle = FindMEmuRenderChild(hWnd);
                if (handle == IntPtr.Zero)
                {
                    // Cửa sổ Qt không có khung render (ví dụ cửa sổ quản lý ẩn) — bỏ qua.
                    return true;
                }
            }
            else if (emulatorType is null)
            {
                emulatorType = MatchLegacy(className, title);
                if (emulatorType is not null)
                {
                    handle = FindRenderChild(hWnd, emulatorType);
                }
            }

            if (emulatorType is not null)
            {
                results.Add(new EmulatorWindow(emulatorType, className, title, handle));
            }

            return true;
        }, IntPtr.Zero);

        return results;
    }

    /// <summary>Tìm cửa sổ render con của một giả lập (dùng cho LDPlayer/Nox).</summary>
    /// <param name="mainHandle">Handle cửa sổ chính.</param>
    /// <param name="emulatorType">Loại giả lập.</param>
    /// <returns>Handle cửa sổ render, hoặc <paramref name="mainHandle"/> nếu không tìm thấy.</returns>
    public static IntPtr FindRenderChild(IntPtr mainHandle, string emulatorType)
    {
        string? renderClassName = emulatorType switch
        {
            "LDPlayer" => "RenderWindow",
            "Nox" => "subWin",
            _ => null
        };

        if (renderClassName == null)
        {
            return mainHandle;
        }

        IntPtr child = FindWindowEx(mainHandle, IntPtr.Zero, renderClassName, null);
        return child != IntPtr.Zero ? child : mainHandle;
    }

    /// <summary>
    /// Xác định loại giả lập từ lớp và tiêu đề cửa sổ (hàm thuần, dùng được trong test).
    /// Hỗ trợ cả MEmu bản cũ (lớp <c>subWin</c>) và MEmu 9 (lớp Qt, tiêu đề <c>MEmu</c>).
    /// </summary>
    /// <param name="className">Tên lớp Win32.</param>
    /// <param name="title">Tiêu đề cửa sổ.</param>
    /// <returns>Loại giả lập hoặc null nếu không khớp.</returns>
    public static string? MatchEmulator(string className, string title)
    {
        foreach (WindowSpec spec in Specs)
        {
            if (!string.Equals(className, spec.ClassName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (spec.EmulatorType == "BlueStacks5"
                || title.Contains(spec.TitleContains, StringComparison.OrdinalIgnoreCase))
            {
                return spec.EmulatorType;
            }
        }

        // MEmu 9 dùng Qt5: top-level có lớp "Qt5152QWindowIcon" và tiêu đề "MEmu".
        if (IsQtClass(className)
            && title.Contains("MEmu", StringComparison.OrdinalIgnoreCase))
        {
            return "MEmu";
        }

        return null;
    }

    /// <summary>True nếu lớp cửa sổ thuộc bộ Qt (ví dụ <c>Qt5152QWindowIcon</c>).</summary>
    /// <param name="className">Tên lớp Win32.</param>
    /// <returns>True nếu bắt đầu bằng "Qt".</returns>
    public static bool IsQtClass(string className)
    {
        return className.StartsWith("Qt", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Tìm cửa sổ render 860x732 của MEmu 9: ưu tiên tiêu đề <c>RenderWindowWindow</c>,
    /// sau đó tới cửa sổ con đang hiển thị có đúng kích thước chuẩn.
    /// </summary>
    /// <param name="mainHandle">Cửa sổ Qt top-level.</param>
    /// <returns>Handle cửa sổ render, hoặc <see cref="IntPtr.Zero"/> nếu không có.</returns>
    public static IntPtr FindMEmuRenderChild(IntPtr mainHandle)
    {
        IntPtr byTitle = FindChildByTitle(mainHandle, MEmuRenderTitle);
        return byTitle != IntPtr.Zero
            ? byTitle
            : FindChildByClientSize(mainHandle, RenderWidth, RenderHeight);
    }

    private static IntPtr FindChildByTitle(IntPtr parent, string title)
    {
        IntPtr found = IntPtr.Zero;

        EnumChildWindows(parent, (hWnd, lParam) =>
        {
            if (string.Equals(ReadTitle(hWnd), title, StringComparison.OrdinalIgnoreCase))
            {
                found = hWnd;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        return found;
    }

    private static IntPtr FindChildByClientSize(IntPtr parent, int width, int height)
    {
        IntPtr visible = IntPtr.Zero;
        IntPtr any = IntPtr.Zero;

        EnumChildWindows(parent, (hWnd, lParam) =>
        {
            if (!GetClientRect(hWnd, out RECT rect)
                || rect.Right - rect.Left != width
                || rect.Bottom - rect.Top != height)
            {
                return true;
            }

            if (IsWindowVisible(hWnd))
            {
                visible = hWnd;
                return false;
            }

            if (any == IntPtr.Zero)
            {
                any = hWnd;
            }

            return true;
        }, IntPtr.Zero);

        return visible != IntPtr.Zero ? visible : any;
    }

    private static string? MatchLegacy(string className, string title)
    {
        string combined = (className + " " + title).ToLowerInvariant();

        if (combined.Contains("ldplayer"))
        {
            return "LDPlayer";
        }

        if (combined.Contains("nox"))
        {
            return "Nox";
        }

        return null;
    }

    private static string ReadClassName(IntPtr hWnd)
    {
        var buffer = new StringBuilder(256);
        GetClassName(hWnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private static string ReadTitle(IntPtr hWnd)
    {
        var buffer = new StringBuilder(256);
        GetWindowText(hWnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }
}
