using System.Runtime.InteropServices;

namespace MyBot.Core.CV.Native;

/// <summary>
/// Loads the original MyBot.run.dll and exposes its OCR exports.
/// The module is a 32-bit mixed-mode assembly, so the host process must be x86.
/// </summary>
internal sealed class MyBotRunNative : IDisposable
{
    /// <summary>Async sentinel returned by DoOCR until the result is ready.</summary>
    public const string AsyncResultMarker = "<GetAsyncResult>";

    private const string DllFileName = "MyBot.run.dll";

    private readonly object _syncRoot = new();
    private IntPtr _module;
    private OcrDelegate? _ocr;
    private DoOcrDelegate? _doOcr;
    private GetAsyncResultDelegate? _getAsyncResult;
    private bool _disposed;

    /// <summary>Gets a value indicating whether the native module is loaded.</summary>
    public bool IsLoaded => _module != IntPtr.Zero;

    /// <summary>
    /// Loads <c>MyBot.run.dll</c> from the given directory and resolves the OCR exports.
    /// </summary>
    /// <param name="libDirectory">Directory containing the DLL and its <c>listSymbols_*.xml</c> models.</param>
    /// <returns><see langword="true"/> when the module and all required exports are available.</returns>
    /// <exception cref="PlatformNotSupportedException">The host process is not 32-bit.</exception>
    public bool Load(string libDirectory)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        if (RuntimeInformation.ProcessArchitecture != Architecture.X86)
        {
            throw new PlatformNotSupportedException(
                "MyBot.run.dll is a 32-bit module. Set <PlatformTarget>x86</PlatformTarget> for the host process.");
        }

        string dllPath = Path.Combine(libDirectory, DllFileName);
        if (!File.Exists(dllPath))
        {
            return false;
        }

        // Let Windows resolve the DLL's own dependencies from the same folder.
        _ = SetDllDirectory(libDirectory);

        _module = LoadLibraryW(dllPath);
        if (_module == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            _ocr = Resolve<OcrDelegate>("ocr");
            _doOcr = Resolve<DoOcrDelegate>("DoOCR");
            _getAsyncResult = Resolve<GetAsyncResultDelegate>("GetAsyncResult");
            return true;
        }
        catch (EntryPointNotFoundException)
        {
            Dispose();
            return false;
        }
    }

    /// <summary>Runs the synchronous glyph OCR export.</summary>
    /// <param name="hBitmap">Handle of the image to recognize.</param>
    /// <param name="model">OCR model name, for example <c>coc-v-g</c> or <c>coc-ms</c>.</param>
    /// <param name="debug">Non-zero to let the native engine write debug images.</param>
    /// <returns>The recognized text, or an empty string.</returns>
    public string Ocr(IntPtr hBitmap, string model, int debug)
    {
        if (_ocr is null)
        {
            throw new InvalidOperationException("MyBot.run.dll has not been loaded.");
        }

        lock (_syncRoot)
        {
            return PointerToString(_ocr(hBitmap, model, debug));
        }
    }

    /// <summary>Starts the asynchronous ImgLoc OCR export.</summary>
    /// <param name="hBitmap">Handle of the image to recognize.</param>
    /// <param name="bundle">Bundle name, for example <c>chinese-bundle</c>.</param>
    /// <returns>The text, or <see cref="AsyncResultMarker"/> while the result is pending.</returns>
    public string DoOcr(IntPtr hBitmap, string bundle)
    {
        if (_doOcr is null)
        {
            throw new InvalidOperationException("MyBot.run.dll has not been loaded.");
        }

        lock (_syncRoot)
        {
            return PointerToString(_doOcr(hBitmap, bundle));
        }
    }

    /// <summary>Polls the pending asynchronous OCR result.</summary>
    /// <returns>The text, or <see cref="AsyncResultMarker"/> when it is still pending.</returns>
    public string GetAsyncResult()
    {
        if (_getAsyncResult is null)
        {
            throw new InvalidOperationException("MyBot.run.dll has not been loaded.");
        }

        lock (_syncRoot)
        {
            return PointerToString(_getAsyncResult());
        }
    }

    /// <summary>Frees the native module.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_module != IntPtr.Zero)
        {
            _ = FreeLibrary(_module);
            _module = IntPtr.Zero;
        }

        _ocr = null;
        _doOcr = null;
        _getAsyncResult = null;
    }

    private T Resolve<T>(string exportName)
        where T : Delegate
    {
        IntPtr address = NativeLibrary.GetExport(_module, exportName);
        return Marshal.GetDelegateForFunctionPointer<T>(address);
    }

    private static string PointerToString(IntPtr pointer)
    {
        return pointer == IntPtr.Zero ? string.Empty : Marshal.PtrToStringAnsi(pointer) ?? string.Empty;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibraryW(string fileName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeLibrary(IntPtr module);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetDllDirectory(string pathName);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private delegate IntPtr OcrDelegate(IntPtr hBitmap, string model, int debug);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private delegate IntPtr DoOcrDelegate(IntPtr hBitmap, string bundle);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private delegate IntPtr GetAsyncResultDelegate();
}
