using System.Globalization;
using MyBot.Core.CV.Native;
using OpenCvSharp;

namespace MyBot.Core.CV;

/// <summary>Một điểm khớp template trả về từ ImgLoc.</summary>
/// <param name="X">Toạ độ X trên khung hình.</param>
/// <param name="Y">Toạ độ Y trên khung hình.</param>
public readonly record struct TilePoint(int X, int Y);

/// <summary>Một đối tượng tìm thấy khi quét cả thư mục template.</summary>
/// <param name="Name">Tên đối tượng (tên file template, không có đuôi).</param>
/// <param name="X">Toạ độ X của tâm.</param>
/// <param name="Y">Toạ độ Y của tâm.</param>
/// <param name="Level">
/// Cấp của đối tượng do ImgLoc đọc từ tên file (<c>objectlevel</c>);
/// 0 khi nguồn tìm kiếm không cung cấp cấp.
/// </param>
public readonly record struct NamedTilePoint(string Name, int X, int Y, int Level = 0);

/// <summary>
/// Tìm template bằng chính engine ImgLoc của <c>MyBot.run.dll</c>, thay vì
/// template-matching của OpenCV. Đây là cách bản AutoIt tìm stone/tree/scenery,
/// nên toạ độ và độ chính xác giống hệt bản gốc.
/// </summary>
public interface ITileSearcher
{
    /// <summary>True nếu DLL và các export ImgLoc đã sẵn sàng.</summary>
    bool IsAvailable { get; }

    /// <summary>Tìm một template trong vùng chỉ định.</summary>
    /// <param name="frame">Khung hình BGR.</param>
    /// <param name="templatePath">Đường dẫn file <c>.xml</c>/<c>.png</c>/<c>.bmp</c>.</param>
    /// <param name="area">Vùng tìm kiếm.</param>
    /// <param name="maxPoints">Số điểm tối đa cần lấy.</param>
    /// <returns>Điểm đầu tiên, hoặc null nếu không thấy.</returns>
    TilePoint? Find(Mat frame, string templatePath, Rect area, int maxPoints = 1);

    /// <summary>Quét toàn bộ template trong một thư mục.</summary>
    /// <param name="frame">Khung hình BGR.</param>
    /// <param name="directory">Thư mục template.</param>
    /// <param name="area">Vùng tìm kiếm.</param>
    /// <param name="maxPoints">Số đối tượng tối đa; 0 = không giới hạn.</param>
    /// <returns>Danh sách đối tượng tìm thấy (có thể rỗng).</returns>
    IReadOnlyList<NamedTilePoint> SearchDirectory(Mat frame, string directory, Rect area, int maxPoints = 0);

    /// <summary>
    /// Quét thư mục template trong một dải cấp, giống <c>findMultiple</c> của AutoIt.
    /// Cài đặt mặc định bỏ qua dải cấp vì nguồn tìm kiếm không có thông tin cấp.
    /// </summary>
    /// <param name="frame">Khung hình BGR.</param>
    /// <param name="directory">Thư mục template.</param>
    /// <param name="area">Vùng tìm kiếm.</param>
    /// <param name="maxPoints">Số đối tượng tối đa; 0 = không giới hạn.</param>
    /// <param name="minLevel">Cấp nhỏ nhất (bao gồm).</param>
    /// <param name="maxLevel">Cấp lớn nhất (bao gồm).</param>
    /// <returns>Danh sách đối tượng tìm thấy (có thể rỗng).</returns>
    IReadOnlyList<NamedTilePoint> SearchDirectory(
        Mat frame,
        string directory,
        Rect area,
        int maxPoints,
        int minLevel,
        int maxLevel)
    {
        return SearchDirectory(frame, directory, area, maxPoints);
    }
}

/// <summary>Chuyển toạ độ làng tham chiếu sang toạ độ màn hình bằng DLL gốc.</summary>
public interface IVillagePositionConverter
{
    /// <summary>True nếu các export chuyển đổi toạ độ đã sẵn sàng.</summary>
    bool IsAvailable { get; }

    /// <summary>Cập nhật offset làng bên trong DLL (tương đương <c>setVillageOffset</c>).</summary>
    /// <param name="x">Offset X.</param>
    /// <param name="y">Offset Y.</param>
    /// <param name="zoom">Hệ số zoom.</param>
    void SetOffset(int x, int y, double zoom);

    /// <summary>Chuyển toạ độ tham chiếu sang toạ độ màn hình hiện tại.</summary>
    /// <param name="x">Toạ độ X tham chiếu.</param>
    /// <param name="y">Toạ độ Y tham chiếu.</param>
    /// <param name="zoom">Hệ số zoom.</param>
    /// <returns>Toạ độ màn hình.</returns>
    (int X, int Y) Convert(int x, int y, double zoom);

    /// <summary>
    /// Áp dụng lại offset làng đã lưu bên trong DLL (AutoIt: <c>EnsureVillageOffset</c>).
    /// Gọi sau khi một phép chuyển toạ độ thất bại để thử lại.
    /// </summary>
    /// <returns>True nếu offset đã sẵn sàng để chuyển toạ độ.</returns>
    bool EnsureOffset() => IsAvailable;

    /// <summary>
    /// Chuyển toạ độ làng tham chiếu sang toạ độ màn hình hiện tại
    /// (AutoIt: <c>ConvertToVillagePos</c>, dùng cho <c>BuildingClick</c>).
    /// </summary>
    /// <param name="x">Toạ độ X tham chiếu.</param>
    /// <param name="y">Toạ độ Y tham chiếu.</param>
    /// <param name="screenX">Toạ độ X trên màn hình.</param>
    /// <param name="screenY">Toạ độ Y trên màn hình.</param>
    /// <returns>True nếu chuyển đổi thành công.</returns>
    bool ToScreen(int x, int y, out int screenX, out int screenY)
    {
        (screenX, screenY) = Convert(x, y, 0);
        return IsAvailable;
    }

    /// <summary>
    /// Chuyển toạ độ màn hình về toạ độ làng tham chiếu
    /// (AutoIt: <c>ConvertFromVillagePos</c>).
    /// </summary>
    /// <param name="x">Toạ độ X trên màn hình.</param>
    /// <param name="y">Toạ độ Y trên màn hình.</param>
    /// <param name="villageX">Toạ độ X tham chiếu.</param>
    /// <param name="villageY">Toạ độ Y tham chiếu.</param>
    /// <returns>True nếu chuyển đổi thành công.</returns>
    bool FromScreen(int x, int y, out int villageX, out int villageY)
    {
        villageX = x;
        villageY = y;
        return false;
    }
}

/// <summary>Bọc các export ImgLoc của <c>MyBot.run.dll</c>.</summary>
public sealed class NativeTileSearcher : ITileSearcher, IVillagePositionConverter, IDisposable
{
    private readonly MyBotRunNative _native = new();
    private readonly object _gate = new();
    private bool _disposed;

    // Offset làng gần nhất (tương đương $g_iVILLAGE_OFFSET của AutoIt) để có thể
    // áp dụng lại bên trong DLL khi một phép chuyển toạ độ thất bại.
    private int _offsetX;
    private int _offsetY;
    private double _offsetZoom = 1;
    private bool _offsetApplied;

    /// <inheritdoc />
    public bool IsAvailable => _native.HasTileSearch;

    /// <inheritdoc />
    public bool IsInitialized => _native.IsLoaded;

    /// <summary>Nạp <c>MyBot.run.dll</c> từ thư mục chỉ định.</summary>
    /// <param name="libDirectory">Thư mục chứa DLL và model.</param>
    /// <returns>True nếu nạp được và có export tìm template.</returns>
    public bool Initialize(string libDirectory)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(libDirectory))
        {
            return false;
        }

        if (!_native.Load(Path.GetFullPath(libDirectory)))
        {
            return false;
        }

        return _native.HasTileSearch;
    }

    /// <inheritdoc />
    public TilePoint? Find(Mat frame, string templatePath, Rect area, int maxPoints = 1)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(templatePath);

        if (!_native.HasTileSearch || frame.Empty())
        {
            return null;
        }

        using NativeBitmap bitmap = NativeBitmap.FromMat(frame);
        string raw = _native.FindTile(bitmap.Handle, templatePath, ToDiamond(area), maxPoints);

        // Định dạng: "count|x,y|x,y|..."
        string[] parts = raw.Split('|');
        if (parts.Length < 2)
        {
            return null;
        }

        for (int i = 1; i < parts.Length; i++)
        {
            if (TryParsePoint(parts[i], out TilePoint point))
            {
                return point;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public IReadOnlyList<NamedTilePoint> SearchDirectory(Mat frame, string directory, Rect area, int maxPoints = 0)
    {
        return SearchDirectory(frame, directory, area, maxPoints, 0, 1000);
    }

    /// <inheritdoc />
    public IReadOnlyList<NamedTilePoint> SearchDirectory(
        Mat frame,
        string directory,
        Rect area,
        int maxPoints,
        int minLevel,
        int maxLevel)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(directory);

        var results = new List<NamedTilePoint>();
        if (!_native.HasTileSearch || frame.Empty())
        {
            return results;
        }

        using NativeBitmap bitmap = NativeBitmap.FromMat(frame);
        string raw = _native.SearchMultipleTiles(
            bitmap.Handle,
            directory,
            ToDiamond(area),
            maxPoints,
            string.Empty,
            minLevel,
            maxLevel);

        if (string.IsNullOrWhiteSpace(raw))
        {
            return results;
        }

        // Mỗi token là id của một object; tên, toạ độ và cấp đọc qua GetProperty.
        foreach (string key in raw.Split('|'))
        {
            if (string.IsNullOrWhiteSpace(key) || key == "0")
            {
                continue;
            }

            string name = _native.GetProperty(key, "objectname");
            string points = _native.GetProperty(key, "objectpoints");
            if (string.IsNullOrWhiteSpace(name) || !TryParsePoint(points, out TilePoint point))
            {
                continue;
            }

            string levelText = _native.GetProperty(key, "objectlevel");
            _ = int.TryParse(levelText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int level);
            results.Add(new NamedTilePoint(name, point.X, point.Y, level));
        }

        return results;
    }

    /// <inheritdoc />
    public void SetOffset(int x, int y, double zoom)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _offsetX = x;
        _offsetY = y;
        _offsetZoom = zoom;

        if (!_native.HasVillagePosition)
        {
            _offsetApplied = false;
            return;
        }

        lock (_gate)
        {
            _native.SetVillageOffset(x, y, (float)zoom);
        }

        _offsetApplied = true;
    }

    /// <inheritdoc />
    public bool EnsureOffset()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_native.HasVillagePosition)
        {
            _offsetApplied = false;
            return false;
        }

        if (_offsetApplied)
        {
            return true;
        }

        lock (_gate)
        {
            _native.SetVillageOffset(_offsetX, _offsetY, (float)_offsetZoom);
        }

        _offsetApplied = true;
        return true;
    }

    /// <inheritdoc />
    public bool ToScreen(int x, int y, out int screenX, out int screenY)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        screenX = x;
        screenY = y;

        if (!_native.HasCoordinateConversion || !EnsureOffset())
        {
            return false;
        }

        bool converted;
        lock (_gate)
        {
            converted = _native.TryConvertToVillagePos(x, y, 0f, out screenX, out screenY);
        }

        if (!converted)
        {
            _offsetApplied = false;
        }

        return converted;
    }

    /// <inheritdoc />
    public bool FromScreen(int x, int y, out int villageX, out int villageY)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        villageX = x;
        villageY = y;

        if (!_native.HasCoordinateConversion || !EnsureOffset())
        {
            return false;
        }

        bool converted;
        lock (_gate)
        {
            converted = _native.TryConvertFromVillagePos(x, y, out villageX, out villageY);
        }

        if (!converted)
        {
            _offsetApplied = false;
        }

        return converted;
    }

    /// <inheritdoc />
    public (int X, int Y) Convert(int x, int y, double zoom)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_native.HasVillagePosition)
        {
            return (x, y);
        }

        lock (_gate)
        {
            return _native.ConvertVillagePos(x, y, (float)zoom);
        }
    }

    /// <summary>Giải phóng DLL.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _native.Dispose();
    }

    /// <summary>DEBUG tạm thời: trả nguyên chuỗi export FindTile.</summary>
    public string DebugFindRaw(Mat frame, string templatePath, Rect area, int maxPoints = 1)
    {
        using NativeBitmap bitmap = NativeBitmap.FromMat(frame);
        return _native.FindTile(bitmap.Handle, templatePath, ToDiamond(area), maxPoints);
    }

    /// <summary>Đổi hình chữ nhật sang định dạng diamond của ImgLoc.</summary>    /// <param name="area">Vùng tìm kiếm.</param>
    /// <returns>Chuỗi <c>x1,y1|x2,y1|x2,y2|x1,y2</c>.</returns>
    public static string ToDiamond(Rect area)
    {
        int left = area.X;
        int top = area.Y;
        int right = area.X + area.Width;
        int bottom = area.Y + area.Height;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{left},{top}|{right},{top}|{right},{bottom}|{left},{bottom}");
    }

    private static bool TryParsePoint(string text, out TilePoint point)
    {
        point = default;
        string[] values = text.Split(',');
        if (values.Length < 2)
        {
            return false;
        }

        if (int.TryParse(values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x)
            && int.TryParse(values[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y))
        {
            point = new TilePoint(x, y);
            return true;
        }

        return false;
    }
}
