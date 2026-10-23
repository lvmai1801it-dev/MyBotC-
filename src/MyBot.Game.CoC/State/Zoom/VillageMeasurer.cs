using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using MyBot.Core.Abstractions;
using MyBot.Core.CV;
using OpenCvSharp;

namespace MyBot.Game.CoC.State.Zoom;

/// <summary>Yêu cầu đo làng cho một khung hình.</summary>
/// <param name="StonePrefix">Tiền tố file stone (<c>stone</c> hoặc <c>2stone</c>).</param>
/// <param name="TreePrefix">Tiền tố file tree (<c>tree</c> hoặc <c>2tree</c>).</param>
/// <param name="OnMainBase">True khi đang ở làng chính (bật tìm scenery).</param>
/// <param name="OnBuilderBase">True khi đang ở Builder Base.</param>
/// <param name="OnBuilderBaseEnemy">True khi đang ở làng địch Builder Base.</param>
/// <param name="CenterVillage">Callback kéo làng về giữa: (x, y, offsetX, offsetY).</param>
/// <param name="SearchZoomOutCount">
/// Giá trị <c>$g_aiSearchZoomOutCounter[0]</c>; AutoIt bỏ qua việc đo khi chưa tìm thấy
/// phong cảnh và bộ đếm còn nhỏ hơn 2 (ép zoom out trước).
/// </param>
public sealed record VillageMeasureRequest(
    string StonePrefix,
    string TreePrefix,
    bool OnMainBase,
    bool OnBuilderBase,
    bool OnBuilderBaseEnemy,
    Action<int, int, int, int>? CenterVillage = null,
    int SearchZoomOutCount = 0);

/// <summary>Kết quả đo làng, tương ứng mảng trả về của <c>GetVillageSize</c>.</summary>
public sealed class VillageMeasurement
{
    /// <summary>True nếu tìm thấy ít nhất một điểm mốc.</summary>
    public bool Found { get; init; }

    /// <summary>Kích thước làng đo được (px); 0 nếu chỉ có một điểm mốc.</summary>
    public double Size { get; init; }

    /// <summary>Hệ số zoom so với làng tham chiếu.</summary>
    public double Zoom { get; init; } = 1;

    /// <summary>Offset X của tâm làng so với vị trí chuẩn.</summary>
    public int OffsetX { get; init; }

    /// <summary>Offset Y của tâm làng so với vị trí chuẩn.</summary>
    public int OffsetY { get; init; }

    /// <summary>Toạ độ X của stone tìm được.</summary>
    public int StoneX { get; init; }

    /// <summary>Toạ độ Y của stone tìm được.</summary>
    public int StoneY { get; init; }

    /// <summary>Tên file stone tìm được.</summary>
    public string? StoneFile { get; init; }

    /// <summary>Toạ độ X của tree tìm được.</summary>
    public int TreeX { get; init; }

    /// <summary>Toạ độ Y của tree tìm được.</summary>
    public int TreeY { get; init; }

    /// <summary>Tên file tree tìm được.</summary>
    public string? TreeFile { get; init; }

    /// <summary>Mã scenery tìm được (2 ký tự cuối của tên file).</summary>
    public string SceneryCode { get; init; } = string.Empty;

    /// <summary>Kích thước làng tham chiếu của phong cảnh.</summary>
    public double ReferenceSize { get; init; }

    /// <summary>True nếu tree thuộc bộ ảnh <c>2tree</c>.</summary>
    public bool SecondTreeSet { get; init; }

    /// <summary>Mã phong cảnh suy ra từ tên file tree.</summary>
    public string TreeCode { get; init; } = string.Empty;

    /// <summary>True nếu vừa kéo làng và cần chụp lại để đo tiếp.</summary>
    public bool NeedsRecapture { get; init; }
}

/// <summary>Đo kích thước làng theo cách của AutoIt.</summary>
public interface IVillageMeasurer
{
    /// <summary>True nếu có thể đo (có tile searcher và thư mục template).</summary>
    bool IsAvailable { get; }

    /// <summary>Đo làng trên một khung hình.</summary>
    /// <param name="frame">Khung hình BGR.</param>
    /// <param name="request">Ngữ cảnh đo.</param>
    /// <returns>Kết quả đo.</returns>
    VillageMeasurement Measure(Mat frame, VillageMeasureRequest request);
}

/// <summary>
/// Port <c>GetVillageSize</c> của AutoIt: tìm scenery, stone và tree bằng ImgLoc,
/// tính kích thước làng bằng định lý Pythagoras rồi suy ra zoom và offset.
/// </summary>
public sealed class VillageMeasurer : IVillageMeasurer
{
    private static readonly Regex SceneryRefRegex = new(@"-(\d+)-(\d+)", RegexOptions.Compiled);
    private static readonly Regex FixedPointRegex =
        new(@"-(\d+)-(\d+)-(\d*,*\d+)_.*[.](?:xml|png|bmp)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly ITileSearcher _searcher;
    private readonly IVillagePositionConverter? _positionConverter;
    private readonly VillageReferenceCatalog _catalog;
    private readonly string _rootDirectory;
    private readonly IBotLogger? _logger;

    /// <summary>Khởi tạo bộ đo làng.</summary>
    /// <param name="searcher">Bộ tìm template ImgLoc.</param>
    /// <param name="positionConverter">Bộ chuyển toạ độ của DLL gốc (tuỳ chọn).</param>
    /// <param name="catalog">Bảng tham chiếu phong cảnh.</param>
    /// <param name="rootDirectory">Thư mục gốc <c>imgxml/village</c>.</param>
    /// <param name="logger">Logger tuỳ chọn.</param>
    public VillageMeasurer(
        ITileSearcher searcher,
        IVillagePositionConverter? positionConverter,
        VillageReferenceCatalog catalog,
        string rootDirectory,
        IBotLogger? logger = null)
    {
        _searcher = searcher ?? throw new ArgumentNullException(nameof(searcher));
        _positionConverter = positionConverter;
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _rootDirectory = rootDirectory ?? throw new ArgumentNullException(nameof(rootDirectory));
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsAvailable => _searcher.IsAvailable && Directory.Exists(_rootDirectory);

    /// <inheritdoc />
    public VillageMeasurement Measure(Mat frame, VillageMeasureRequest request)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(request);

        if (frame.Empty() || !IsAvailable)
        {
            return new VillageMeasurement();
        }

        bool onBuilder = request.OnBuilderBase || request.OnBuilderBaseEnemy;
        string mainDirectory = Path.Combine(_rootDirectory, "NormalVillage");
        string customDirectory = Path.Combine(_rootDirectory, "Custom");
        string builderDirectory = Path.Combine(_rootDirectory, "BuilderBase");
        string directory = onBuilder ? builderDirectory : mainDirectory;

        // Giá trị mặc định của GetVillageSize.
        int additionalX = 200;
        int additionalY = 125;
        string sceneryCode = string.Empty;
        bool sceneryFound = false;

        if (request.OnMainBase)
        {
            const int sceneryX = 185;
            const int sceneryY = 600;
            const int sceneryRight = 680;
            const int sceneryBottom = 730;

            var sceneryArea = Rect.FromLTRB(sceneryX, sceneryY, sceneryRight, sceneryBottom);
            string sceneryDirectory = Path.Combine(_rootDirectory, "VillageScenery");

            // AutoIt findMultiple: minLevel 0, maxLevel 1000, maxReturnPoints 1.
            IReadOnlyList<NamedTilePoint> sceneries =
                _searcher.SearchDirectory(frame, sceneryDirectory, sceneryArea, maxPoints: 1);

            if (sceneries.Count == 0)
            {
                // Một số phong cảnh (Egypt, Magic) bị cuộn lên trên; thử mở rộng vùng lên 125px.
                var extendedArea = Rect.FromLTRB(sceneryX, sceneryY - 125, sceneryRight, sceneryBottom);
                sceneries = _searcher.SearchDirectory(frame, sceneryDirectory, extendedArea, maxPoints: 1);
            }

            foreach (NamedTilePoint scenery in sceneries)
            {
                Match reference = SceneryRefRegex.Match(scenery.Name);
                if (!reference.Success)
                {
                    continue;
                }

                int referenceX = ParseInt(reference.Groups[1].Value);
                int referenceY = ParseInt(reference.Groups[2].Value);

                if (IsFarFromReference(scenery.X, scenery.Y, referenceX, referenceY))
                {
                    _logger?.Debug($"Scenery lệch hơn 75px tại {scenery.X},{scenery.Y} — căn giữa làng.");
                    request.CenterVillage?.Invoke(scenery.X, scenery.Y, scenery.X - referenceX, scenery.Y - referenceY);
                    return new VillageMeasurement { NeedsRecapture = true };
                }

                sceneryCode = SceneryCodeFromName(scenery.Name);
                sceneryFound = true;
                additionalX = 100;
                additionalY = 75;
                break;
            }
        }

        // AutoIt: chưa thấy phong cảnh và bộ đếm zoom < 2 thì bỏ đo để ép zoom out thêm.
        if (request.OnMainBase && !sceneryFound && request.SearchZoomOutCount < 2)
        {
            _logger?.Debug(
                $"Không thấy phong cảnh (zoomout #{request.SearchZoomOutCount}) — bỏ qua đo làng.");
            return new VillageMeasurement();
        }

        // AutoIt chỉ tìm stone của đúng phong cảnh đã nhận diện (stoneMS-*),
        // nếu không thấy phong cảnh thì tìm tất cả stone.
        string stonePrefix = sceneryFound
            ? request.StonePrefix + sceneryCode
            : request.StonePrefix;

        VillageFixedPoint? stone = SearchFixedPoint(
            frame,
            directory,
            customDirectory,
            stonePrefix,
            sceneryCode,
            additionalX,
            additionalY,
            request);

        VillageFixedPoint? tree = null;
        if (stone is null)
        {
            _logger?.Debug("Không tìm thấy stone — tìm tất cả tree.");
            tree = SearchFixedPoint(
                frame,
                directory,
                customDirectory,
                request.TreePrefix,
                sceneryCode,
                additionalX,
                additionalY,
                request);
        }
        else
        {
            string stoneScenery = SceneryCodeFromName(stone.Value.FileName);
            string treePrefix = stoneScenery == "DS"
                ? request.TreePrefix + "D"
                : request.TreePrefix + stoneScenery;

            tree = SearchFixedPoint(
                frame,
                directory,
                customDirectory,
                treePrefix,
                sceneryCode,
                additionalX,
                additionalY,
                request);
        }

        return BuildMeasurement(stone, tree, sceneryCode);
    }

    private VillageFixedPoint? SearchFixedPoint(
        Mat frame,
        string directory,
        string customDirectory,
        string prefix,
        string sceneryCode,
        int additionalX,
        int additionalY,
        VillageMeasureRequest request)
    {
        bool useCustom = IsCustomScenery(request.OnMainBase, sceneryCode);
        string searchDirectory = useCustom ? customDirectory : directory;

        foreach (string file in EnumerateTemplates(searchDirectory, prefix))
        {
            Match match = FixedPointRegex.Match(Path.GetFileName(file));
            if (!match.Success)
            {
                continue;
            }

            int referenceX = ParseInt(match.Groups[1].Value);
            int referenceY = ParseInt(match.Groups[2].Value);
            double distance = ParseDouble(match.Groups[3].Value);

            var area = Rect.FromLTRB(
                referenceX - additionalX,
                referenceY - additionalY,
                referenceX + additionalX,
                referenceY + additionalY);

            TilePoint? found = _searcher.Find(frame, file, area);
            if (found is null)
            {
                continue;
            }

            _logger?.Debug($"Tìm thấy {Path.GetFileName(file)} tại {found.Value.X},{found.Value.Y}.");
            return new VillageFixedPoint(
                found.Value.X,
                found.Value.Y,
                referenceX,
                referenceY,
                distance,
                Path.GetFileName(file));
        }

        return null;
    }

    private VillageMeasurement BuildMeasurement(
        VillageFixedPoint? stone,
        VillageFixedPoint? tree,
        string sceneryCode)
    {
        if (stone is null && tree is null)
        {
            _logger?.Debug("GetVillageSize không tìm thấy stone hoặc tree.");
            return new VillageMeasurement();
        }

        double size = 0;
        double zoom = 1;
        int offsetX;
        int offsetY;
        double referenceSize = 0;
        string treeCode = string.Empty;
        bool secondTreeSet = false;

        if (stone is null)
        {
            offsetX = tree!.Value.X - tree.Value.ReferenceX;
            offsetY = tree.Value.Y - tree.Value.ReferenceY;
        }
        else if (tree is null)
        {
            offsetX = stone.Value.X - stone.Value.ReferenceX;
            offsetY = stone.Value.Y - stone.Value.ReferenceY;
        }
        else
        {
            double a = tree.Value.X - stone.Value.X;
            double b = stone.Value.Y - tree.Value.Y;
            size = System.Math.Sqrt((a * a) + (b * b)) - stone.Value.Distance - tree.Value.Distance;

            treeCode = TreeCodeFromName(tree.Value.FileName);
            secondTreeSet = Path.GetFileName(tree.Value.FileName).Contains("2tree", StringComparison.OrdinalIgnoreCase);
            referenceSize = _catalog.GetReferenceSize(treeCode, secondTreeSet);

            zoom = referenceSize > 0 ? size / referenceSize : 1;

            (int expectedX, int expectedY) = _positionConverter is not null
                ? _positionConverter.Convert(stone.Value.ReferenceX, stone.Value.ReferenceY, zoom)
                : (stone.Value.ReferenceX, stone.Value.ReferenceY);

            offsetX = stone.Value.X - expectedX;
            offsetY = stone.Value.Y - expectedY;
        }

        return new VillageMeasurement
        {
            Found = true,
            Size = size,
            Zoom = zoom,
            OffsetX = offsetX,
            OffsetY = offsetY,
            StoneX = stone?.X ?? 0,
            StoneY = stone?.Y ?? 0,
            StoneFile = stone?.FileName,
            TreeX = tree?.X ?? 0,
            TreeY = tree?.Y ?? 0,
            TreeFile = tree?.FileName,
            SceneryCode = sceneryCode,
            ReferenceSize = referenceSize,
            SecondTreeSet = secondTreeSet,
            TreeCode = treeCode,
        };
    }

    private static IEnumerable<string> EnumerateTemplates(string directory, string prefix)
    {
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (string file in Directory.EnumerateFiles(directory, prefix + "*.*"))
        {
            string extension = Path.GetExtension(file);
            if (extension.Equals(".xml", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase))
            {
                yield return file;
            }
        }
    }

    private bool IsCustomScenery(bool onMainBase, string sceneryCode)
    {
        return onMainBase
            && !string.IsNullOrEmpty(sceneryCode)
            && _catalog.IsCustomMainVillage(sceneryCode);
    }

    private static bool IsFarFromReference(int x, int y, int referenceX, int referenceY)
    {
        return x > referenceX + 75
            || x < referenceX - 75
            || y > referenceY + 75
            || y < referenceY - 75;
    }

    /// <summary>Lấy 2 ký tự cuối của phần tên trước dấu gạch đầu tiên.</summary>
    /// <param name="fileName">Tên file hoặc object name.</param>
    /// <returns>Mã phong cảnh; rỗng nếu không phân tích được.</returns>
    public static string SceneryCodeFromName(string fileName)
    {
        string name = Path.GetFileNameWithoutExtension(fileName);
        int dash = name.IndexOf('-', StringComparison.Ordinal);
        string head = dash >= 0 ? name[..dash] : name;
        return head.Length >= 2 ? head[^2..] : head;
    }

    /// <summary>Lấy mã phong cảnh từ tên file tree (bỏ tiền tố <c>tree</c>/<c>2tree</c>).</summary>
    /// <param name="fileName">Tên file tree.</param>
    /// <returns>Mã phong cảnh, ví dụ <c>CC</c>.</returns>
    public static string TreeCodeFromName(string fileName)
    {
        string name = Path.GetFileNameWithoutExtension(fileName);
        int dash = name.IndexOf('-', StringComparison.Ordinal);
        string head = dash >= 0 ? name[..dash] : name;

        if (head.StartsWith("2tree", StringComparison.OrdinalIgnoreCase))
        {
            return head[5..];
        }

        if (head.StartsWith("tree", StringComparison.OrdinalIgnoreCase))
        {
            return head[4..];
        }

        return head;
    }

    private static int ParseInt(string value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)
            ? result
            : 0;
    }

    private static double ParseDouble(string value)
    {
        string normalized = value.Replace(',', '.');
        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out double result)
            ? result
            : 0;
    }

    private readonly record struct VillageFixedPoint(
        int X,
        int Y,
        int ReferenceX,
        int ReferenceY,
        double Distance,
        string FileName);
}
