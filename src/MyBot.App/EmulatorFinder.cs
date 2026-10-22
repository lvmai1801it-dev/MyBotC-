using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace MyBot.App;

public static class EmulatorFinder
{
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string? lpszClass, string? lpszWindow);

    public record EmulatorWindow(string EmulatorType, string ClassName, string Title, IntPtr Handle);

    public static List<EmulatorWindow> FindAll(bool debugPrintAll = false)
    {
        var results = new List<EmulatorWindow>();

        EnumWindows((hWnd, lParam) =>
        {
            var sbClass = new StringBuilder(256);
            GetClassName(hWnd, sbClass, 256);
            string className = sbClass.ToString();

            var sbTitle = new StringBuilder(256);
            GetWindowText(hWnd, sbTitle, 256);
            string title = sbTitle.ToString();

            if (debugPrintAll && !string.IsNullOrWhiteSpace(title))
            {
                Console.WriteLine($"[DEBUG] Handle: 0x{hWnd.ToInt64():X} | Class: {className} | Title: {title}");
            }

            // Quét dựa trên cả ClassName và Title (Không phân biệt hoa thường)
            string combined = (className + " " + title).ToLower();

            string? emuType = null;
            if (combined.Contains("memu")) emuType = "MEmu";
            else if (combined.Contains("ldplayer")) emuType = "LDPlayer";
            else if (combined.Contains("bluestacks")) emuType = "BlueStacks";
            else if (combined.Contains("nox")) emuType = "Nox";

            if (emuType != null)
            {
                results.Add(new EmulatorWindow(emuType, className, title, hWnd));
            }

            return true;
        }, IntPtr.Zero);

        return results;
    }

    public static IntPtr FindRenderChild(IntPtr mainHandle, string emulatorType)
    {
        // MEmu: Cửa sổ chính là MEmuConsole, cửa sổ render game là RenderWindow
        string? renderClassName = emulatorType switch
        {
            "LDPlayer" => "RenderWindow",
            "MEmu" => "RenderWindow",
            "BlueStacks" => "subWin",
            "Nox" => "subWin",
            _ => null
        };

        if (renderClassName == null) return mainHandle;

        IntPtr child = FindWindowEx(mainHandle, IntPtr.Zero, renderClassName, null);
        return child != IntPtr.Zero ? child : mainHandle;
    }
}
