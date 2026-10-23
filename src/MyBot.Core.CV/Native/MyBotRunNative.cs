using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace MyBot.Core.CV.Native;

/// <summary>
/// Loads the original MyBot.run.dll and exposes its exports (OCR, ImgLoc tile
/// search and village position conversion).
/// The module is a 32-bit mixed-mode assembly, so the host process must be x86.
/// </summary>
internal sealed class MyBotRunNative : IDisposable
{
    /// <summary>Async sentinel returned by DoOCR until the result is ready.</summary>
    public const string AsyncResultMarker = "<GetAsyncResult>";

    /// <summary>How long to keep polling <c>GetAsyncResult</c> for a tile search.</summary>
    private const int AsyncResultTimeoutMs = 30_000;

    private const string DllFileName = "MyBot.run.dll";

    private readonly object _syncRoot = new();
    private IntPtr _module;
    private OcrDelegate? _ocr;
    private DoOcrDelegate? _doOcr;
    private GetAsyncResultDelegate? _getAsyncResult;
    private FindTileDelegate? _findTile;
    private SearchMultipleTilesDelegate? _searchMultipleTiles;
    private GetPropertyDelegate? _getProperty;
    private SetVillageOffsetDelegate? _setVillageOffset;
    private ConvertVillagePosDelegate? _convertVillagePos;
    private ConvertToVillagePosDelegate? _convertToVillagePos;
    private ConvertFromVillagePosDelegate? _convertFromVillagePos;
    private bool _disposed;

    /// <summary>Gets a value indicating whether the native module is loaded.</summary>
    public bool IsLoaded => _module != IntPtr.Zero;

    /// <summary>Gets a value indicating whether the ImgLoc tile-search exports are available.</summary>
    public bool HasTileSearch => _findTile is not null && _searchMultipleTiles is not null && _getProperty is not null;

    /// <summary>Gets a value indicating whether the village position exports are available.</summary>
    public bool HasVillagePosition => _setVillageOffset is not null && _convertVillagePos is not null;

    /// <summary>
    /// Gets a value indicating whether the two-way coordinate conversion exports
    /// (<c>ConvertToVillagePos</c> / <c>ConvertFromVillagePos</c>) are available.
    /// </summary>
    public bool HasCoordinateConversion => _convertToVillagePos is not null && _convertFromVillagePos is not null;

    /// <summary>
    /// Loads <c>MyBot.run.dll</c> from the given directory and resolves the exports.
    /// OCR and ImgLoc exports are optional; missing ones simply leave the matching
    /// <c>Has*</c> property false.
    /// </summary>
    /// <param name="libDirectory">Directory containing the DLL and its <c>listSymbols_*.xml</c> models.</param>
    /// <returns><see langword="true"/> when the module was loaded.</returns>
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

        _ocr = TryResolve<OcrDelegate>("ocr");
        _doOcr = TryResolve<DoOcrDelegate>("DoOCR");
        _getAsyncResult = TryResolve<GetAsyncResultDelegate>("GetAsyncResult");
        _findTile = TryResolve<FindTileDelegate>("FindTile");
        _searchMultipleTiles = TryResolve<SearchMultipleTilesDelegate>("SearchMultipleTilesBetweenLevels");
        _getProperty = TryResolve<GetPropertyDelegate>("GetProperty");
        _setVillageOffset = TryResolve<SetVillageOffsetDelegate>("setVillageOffset");
        _convertVillagePos = TryResolve<ConvertVillagePosDelegate>("ConvertVillagePos");
        _convertToVillagePos = TryResolve<ConvertToVillagePosDelegate>("ConvertToVillagePos");
        _convertFromVillagePos = TryResolve<ConvertFromVillagePosDelegate>("ConvertFromVillagePos");
        return true;
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
            throw new InvalidOperationException("MyBot.run.dll does not expose the ocr export.");
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
            throw new InvalidOperationException("MyBot.run.dll does not expose the DoOCR export.");
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
            throw new InvalidOperationException("MyBot.run.dll does not expose the GetAsyncResult export.");
        }

        lock (_syncRoot)
        {
            return PointerToString(_getAsyncResult());
        }
    }

    /// <summary>
    /// Runs the ImgLoc <c>FindTile</c> export.
    /// </summary>
    /// <param name="hBitmap">Handle of the image to search.</param>
    /// <param name="tilePath">Path of the <c>.xml</c> template (plain Windows path).</param>
    /// <param name="area">Diamond search area in <c>x1,y1|x2,y1|x2,y2|x1,y2</c> form.</param>
    /// <param name="maxPoints">Maximum number of returned points.</param>
    /// <returns>Raw result <c>count|x,y|...</c>.</returns>
    public string FindTile(IntPtr hBitmap, string tilePath, string area, int maxPoints)
    {
        if (_findTile is null)
        {
            throw new InvalidOperationException("MyBot.run.dll does not expose the FindTile export.");
        }

        string encoded = EncodePath(tilePath);
        lock (_syncRoot)
        {
            // ImgLoc search is asynchronous: the DLL returns "<GetAsyncResult>"
            // and the real result comes from GetAsyncResult (see DllCallMyBot).
            return AwaitIfPending(PointerToString(_findTile(hBitmap, encoded, area, maxPoints)));
        }
    }

    /// <summary>
    /// Runs the ImgLoc <c>SearchMultipleTilesBetweenLevels</c> export over a directory.
    /// </summary>
    /// <param name="hBitmap">Handle of the image to search.</param>
    /// <param name="directory">Directory containing the templates.</param>
    /// <param name="diamond">Diamond search area.</param>
    /// <param name="maxReturnPoints">Maximum number of returned objects; 0 means unlimited.</param>
    /// <param name="redLines">Optional red-line definition.</param>
    /// <param name="minLevel">Minimum template level.</param>
    /// <param name="maxLevel">Maximum template level.</param>
    /// <returns>Raw result containing object ids separated by <c>|</c>.</returns>
    public string SearchMultipleTiles(
        IntPtr hBitmap,
        string directory,
        string diamond,
        int maxReturnPoints,
        string redLines,
        int minLevel,
        int maxLevel)
    {
        if (_searchMultipleTiles is null)
        {
            throw new InvalidOperationException(
                "MyBot.run.dll does not expose the SearchMultipleTilesBetweenLevels export.");
        }

        string encoded = EncodePath(directory);
        lock (_syncRoot)
        {
            return AwaitIfPending(PointerToString(_searchMultipleTiles(
                hBitmap, encoded, diamond, maxReturnPoints, redLines, minLevel, maxLevel)));
        }
    }

    /// <summary>Reads a property of an ImgLoc result object.</summary>
    /// <param name="key">Object id returned by <see cref="SearchMultipleTiles"/>.</param>
    /// <param name="property">Property name, for example <c>objectname</c> or <c>objectpoints</c>.</param>
    /// <returns>Property value, or an empty string.</returns>
    public string GetProperty(string key, string property)
    {
        if (_getProperty is null)
        {
            throw new InvalidOperationException("MyBot.run.dll does not expose the GetProperty export.");
        }

        lock (_syncRoot)
        {
            return AwaitIfPending(PointerToString(_getProperty(key, property)));
        }
    }

    /// <summary>
    /// Polls <c>GetAsyncResult</c> while the DLL reports a pending result,
    /// mirroring the loop in AutoIt's <c>DllCallMyBot</c>.
    /// </summary>
    /// <param name="result">Value returned by the export.</param>
    /// <returns>The final result, or empty string on timeout.</returns>
    private string AwaitIfPending(string result)
    {
        if (_getAsyncResult is null || !IsPending(result))
        {
            return result;
        }

        long deadline = Environment.TickCount64 + AsyncResultTimeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            Thread.Sleep(100);
            result = PointerToString(_getAsyncResult());
            if (!IsPending(result))
            {
                return result;
            }
        }

        return string.Empty;
    }

    private static bool IsPending(string result)
    {
        return string.Equals(result.Trim(), AsyncResultMarker, StringComparison.Ordinal);
    }

    /// <summary>Updates the village offset kept inside the native engine.</summary>
    /// <param name="x">Horizontal offset in game pixels.</param>
    /// <param name="y">Vertical offset in game pixels.</param>
    /// <param name="zoom">Zoom factor relative to the reference village.</param>
    public void SetVillageOffset(int x, int y, float zoom)
    {
        if (_setVillageOffset is null)
        {
            throw new InvalidOperationException("MyBot.run.dll does not expose the setVillageOffset export.");
        }

        lock (_syncRoot)
        {
            _ = _setVillageOffset(x, y, zoom);
        }
    }

    /// <summary>
    /// Converts a reference village position to the current screen position using
    /// the offset last passed to <see cref="SetVillageOffset"/>.
    /// </summary>
    /// <param name="x">Reference X.</param>
    /// <param name="y">Reference Y.</param>
    /// <param name="zoom">Zoom factor.</param>
    /// <returns>Converted position, or the input position when parsing fails.</returns>
    public (int X, int Y) ConvertVillagePos(int x, int y, float zoom)
    {
        if (_convertVillagePos is null)
        {
            throw new InvalidOperationException("MyBot.run.dll does not expose the ConvertVillagePos export.");
        }

        string raw;
        lock (_syncRoot)
        {
            raw = PointerToString(_convertVillagePos(x, y, zoom));
        }

        string[] parts = raw.Split('|');
        if (parts.Length >= 3
            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int resultX)
            && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int resultY))
        {
            return (resultX, resultY);
        }

        return (x, y);
    }

    /// <summary>
    /// Converts reference village coordinates to the current screen coordinates
    /// (<c>ConvertToVillagePos</c>). Unlike <see cref="ConvertVillagePos"/> this export
    /// takes no explicit zoom factor: 0 makes the native engine use the zoom stored by
    /// <see cref="SetVillageOffset"/>, matching AutoIt's <c>BuildingClick</c>.
    /// </summary>
    /// <param name="x">Reference X.</param>
    /// <param name="y">Reference Y.</param>
    /// <param name="zoom">Zoom factor; 0 uses the last offset applied.</param>
    /// <param name="resultX">Converted screen X.</param>
    /// <param name="resultY">Converted screen Y.</param>
    /// <returns>True when the export returned a valid pair.</returns>
    public bool TryConvertToVillagePos(int x, int y, float zoom, out int resultX, out int resultY)
    {
        resultX = x;
        resultY = y;

        if (_convertToVillagePos is null)
        {
            return false;
        }

        string raw;
        lock (_syncRoot)
        {
            raw = PointerToString(_convertToVillagePos(x, y, zoom));
        }

        return TryParsePair(raw, out resultX, out resultY);
    }

    /// <summary>
    /// Converts current screen coordinates to reference village coordinates
    /// (<c>ConvertFromVillagePos</c>).
    /// </summary>
    /// <param name="x">Screen X.</param>
    /// <param name="y">Screen Y.</param>
    /// <param name="resultX">Converted reference X.</param>
    /// <param name="resultY">Converted reference Y.</param>
    /// <returns>True when the export returned a valid pair.</returns>
    public bool TryConvertFromVillagePos(int x, int y, out int resultX, out int resultY)
    {
        resultX = x;
        resultY = y;

        if (_convertFromVillagePos is null)
        {
            return false;
        }

        string raw;
        lock (_syncRoot)
        {
            raw = PointerToString(_convertFromVillagePos(x, y));
        }

        return TryParsePair(raw, out resultX, out resultY);
    }

    private static bool TryParsePair(string raw, out int x, out int y)
    {
        x = 0;
        y = 0;

        string[] parts = raw.Split('|');
        if (parts.Length < 3)
        {
            return false;
        }

        return int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out x)
            && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out y);
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
        _findTile = null;
        _searchMultipleTiles = null;
        _getProperty = null;
        _setVillageOffset = null;
        _convertVillagePos = null;
        _convertToVillagePos = null;
        _convertFromVillagePos = null;
    }

    /// <summary>
    /// Encodes a path the way <c>DllCallMyBot</c> does for ImgLoc exports:
    /// <c>"-" + Base64(UTF-8(path))</c>.
    /// </summary>
    /// <param name="path">Plain path.</param>
    /// <returns>Encoded argument.</returns>
    internal static string EncodePath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return "-" + Convert.ToBase64String(Encoding.UTF8.GetBytes(path));
    }

    private T? TryResolve<T>(string exportName)
        where T : Delegate
    {
        if (!NativeLibrary.TryGetExport(_module, exportName, out IntPtr address))
        {
            return null;
        }

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

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private delegate IntPtr FindTileDelegate(IntPtr hBitmap, string encodedPath, string area, int maxPoints);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private delegate IntPtr SearchMultipleTilesDelegate(
        IntPtr hBitmap,
        string encodedDirectory,
        string diamond,
        int maxReturnPoints,
        string redLines,
        int minLevel,
        int maxLevel);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private delegate IntPtr GetPropertyDelegate(string key, string property);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private delegate IntPtr SetVillageOffsetDelegate(int x, int y, float zoom);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private delegate IntPtr ConvertVillagePosDelegate(int x, int y, float zoom);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private delegate IntPtr ConvertToVillagePosDelegate(int x, int y, float zoom);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    private delegate IntPtr ConvertFromVillagePosDelegate(int x, int y);
}
