using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MyBot.Localization;

namespace MyBot.UI.Icons;

/// <summary>
/// Nạp icon từ <c>lib/MBRBOT.dll</c> — bản port của <c>_GUICtrlCreateIcon()</c> trong AutoIt.
///
/// Bản gốc nạp DLL bằng <c>_WinAPI_LoadLibraryEx(LOAD_LIBRARY_AS_DATAFILE)</c>, lấy icon
/// bằng <c>_WinAPI_LoadImage(IMAGE_ICON, w, h)</c> rồi chuyển sang HBITMAP 32-bit và gắn
/// vào một <c>GUICtrlCreatePic</c>. Ở đây làm tương đương nhưng ngắn hơn: DLL được giữ
/// trong bộ nhớ suốt vòng đời tiến trình, mỗi icon được tạo <see cref="BitmapSource"/>
/// đã freeze và cache theo (id, kích thước) nên không phát sinh GDI handle rò rỉ.
/// </summary>
public sealed class MbrBotIconProvider : IDisposable
{
    private const uint LoadLibraryAsDataFile = 0x00000002;
    private const uint ImageIcon = 1;
    private const uint LrDefaultColor = 0x00000000;

    private readonly Dictionary<(int Id, int Size), BitmapSource> _cache = new();
    private readonly object _sync = new();

    private IntPtr _module;
    private bool _disposed;

    /// <summary>
    /// Khởi tạo provider trỏ tới file icon DLL.
    /// </summary>
    /// <param name="dllPath">Đường dẫn tới <c>MBRBOT.dll</c>.</param>
    public MbrBotIconProvider(string dllPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dllPath);
        DllPath = dllPath;
    }

    /// <summary>Đường dẫn tới DLL icon (thường là <c>lib/MBRBOT.dll</c> cạnh file thực thi).</summary>
    public string DllPath { get; }

    /// <summary>true nếu DLL đã nạp được và có thể lấy icon.</summary>
    public bool IsAvailable
    {
        get
        {
            if (_disposed)
            {
                return false;
            }

            EnsureModule();
            return _module != IntPtr.Zero;
        }
    }

    /// <summary>
    /// Lấy icon theo chỉ số <see cref="IconId"/> ở kích thước cho trước.
    /// Trả về <c>null</c> khi DLL không nạp được hoặc tài nguyên không tồn tại.
    /// </summary>
    public ImageSource? GetIcon(IconId id, int size)
    {
        if (_disposed || size <= 0)
        {
            return null;
        }

        lock (_sync)
        {
            if (_cache.TryGetValue(((int)id, size), out BitmapSource? cached))
            {
                return cached;
            }

            BitmapSource? loaded = LoadIcon((int)id, size);
            if (loaded is null)
            {
                return null;
            }

            _cache[((int)id, size)] = loaded;
            return loaded;
        }
    }

    private void EnsureModule()
    {
        if (_module != IntPtr.Zero || !File.Exists(DllPath))
        {
            return;
        }

        _module = NativeMethods.LoadLibraryEx(DllPath, IntPtr.Zero, LoadLibraryAsDataFile);
    }

    private BitmapSource? LoadIcon(int resourceId, int size)
    {
        EnsureModule();
        if (_module == IntPtr.Zero)
        {
            return null;
        }

        // LoadImage với LR_DEFAULTCOLOR trả về HICON đã scale đúng kích thước yêu cầu.
        // Tên tài nguyên dạng số phải truyền qua MAKEINTRESOURCE (IntPtr của chỉ số).
        IntPtr handle = NativeMethods.LoadImage(_module, (IntPtr)resourceId, ImageIcon, size, size, LrDefaultColor);
        if (handle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            BitmapSource source = Imaging.CreateBitmapSourceFromHIcon(
                handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        lock (_sync)
        {
            _cache.Clear();
            if (_module != IntPtr.Zero)
            {
                NativeMethods.FreeLibrary(_module);
                _module = IntPtr.Zero;
            }
        }
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", EntryPoint = "LoadLibraryExW", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr LoadLibraryEx(string fileName, IntPtr file, uint flags);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FreeLibrary(IntPtr module);

        [DllImport("user32.dll", EntryPoint = "LoadImageW", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr LoadImage(IntPtr instance, IntPtr name, uint type, int cx, int cy, uint load);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool DestroyIcon(IntPtr icon);
    }
}
