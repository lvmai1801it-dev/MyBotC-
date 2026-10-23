using System;
using System.Collections.Generic;
using System.IO;
using MyBot.Core.Abstractions;
using MyBot.Core.CV;
using OpenCvSharp;

namespace MyBot.Game.CoC.Village;

/// <summary>Tham số tìm Town Hall, port từ <c>imglocTHSearch</c> của AutoIt.</summary>
public sealed class TownHallSearchOptions
{
    /// <summary>
    /// Thư mục gốc <c>imgxml/Buildings</c> chứa <c>Townhall</c>, <c>Townhall2</c>,
    /// <c>snow-Townhall</c> và <c>snow-Townhall2</c>.
    /// </summary>
    public string BuildingsDirectory { get; init; } = string.Empty;

    /// <summary>Cấp Town Hall nhỏ nhất được tìm. AutoIt: 6.</summary>
    public int MinLevel { get; init; } = 6;

    /// <summary>Cấp Town Hall lớn nhất được tìm. AutoIt: 100.</summary>
    public int MaxLevel { get; init; } = 100;

    /// <summary>
    /// True khi làng đang dùng theme tuyết (<c>$g_iDetectedImageType = 1</c>),
    /// bật thêm 2 lượt quét với bộ ảnh <c>snow-Townhall*</c>.
    /// </summary>
    public bool SnowTheme { get; init; }
}

/// <summary>Kết quả một lần tìm Town Hall.</summary>
public sealed class TownHallSearchResult
{
    /// <summary>True khi tìm thấy Town Hall và đổi được toạ độ.</summary>
    public bool Found { get; init; }

    /// <summary>Toạ độ X trên màn hình do ImgLoc trả về.</summary>
    public int ScreenX { get; init; }

    /// <summary>Toạ độ Y trên màn hình do ImgLoc trả về.</summary>
    public int ScreenY { get; init; }

    /// <summary>Toạ độ X trong hệ tham chiếu của làng (dùng để lưu profile).</summary>
    public int VillageX { get; init; }

    /// <summary>Toạ độ Y trong hệ tham chiếu của làng (dùng để lưu profile).</summary>
    public int VillageY { get; init; }

    /// <summary>Cấp Town Hall đọc từ tên template (0 nếu nguồn tìm kiếm không cung cấp).</summary>
    public int Level { get; init; }

    /// <summary>Tên template khớp được (tên file, không có đuôi).</summary>
    public string? Template { get; init; }

    /// <summary>
    /// True khi tìm thấy template nhưng DLL không đổi được toạ độ màn hình sang
    /// toạ độ làng kể cả sau khi áp dụng lại village offset.
    /// </summary>
    public bool CoordinateConversionFailed { get; init; }
}

/// <summary>Tìm Town Hall trong làng của mình bằng ImgLoc (port <c>imglocTHSearch</c>).</summary>
public interface ITownHallSearcher
{
    /// <summary>True nếu có DLL ImgLoc và thư mục template Town Hall.</summary>
    bool IsAvailable { get; }

    /// <summary>Tìm Town Hall trong một khung hình.</summary>
    /// <param name="frame">Khung hình BGR.</param>
    /// <param name="searchArea">Vùng tìm kiếm (toạ độ màn hình).</param>
    /// <param name="singlePass">True để chỉ quét đúng một lượt (<c>$bSinglePass</c>).</param>
    /// <returns>Kết quả tìm kiếm.</returns>
    TownHallSearchResult Search(Mat frame, Rect searchArea, bool singlePass);
}

/// <summary>
/// Port nhánh <c>$myVillage = True</c> của <c>imglocTHSearch</c>: quét
/// <c>Townhall</c>/<c>Townhall2</c> theo từng lượt, chọn kết quả cấp cao nhất rồi
/// đổi toạ độ màn hình về toạ độ làng tham chiếu của DLL.
/// </summary>
public sealed class TownHallSearcher : ITownHallSearcher
{
    private readonly ITileSearcher _searcher;
    private readonly IVillagePositionConverter _converter;
    private readonly TownHallSearchOptions _options;
    private readonly IBotLogger? _logger;

    /// <summary>Khởi tạo bộ tìm Town Hall.</summary>
    /// <param name="searcher">Bộ tìm template ImgLoc.</param>
    /// <param name="converter">Bộ chuyển toạ độ của DLL gốc.</param>
    /// <param name="options">Tham số tìm kiếm.</param>
    /// <param name="logger">Logger tuỳ chọn.</param>
    public TownHallSearcher(
        ITileSearcher searcher,
        IVillagePositionConverter converter,
        TownHallSearchOptions options,
        IBotLogger? logger = null)
    {
        _searcher = searcher ?? throw new ArgumentNullException(nameof(searcher));
        _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsAvailable
    {
        get
        {
            if (!_searcher.IsAvailable || string.IsNullOrWhiteSpace(_options.BuildingsDirectory))
            {
                return false;
            }

            return Directory.Exists(Path.Combine(_options.BuildingsDirectory, "Townhall"))
                || Directory.Exists(Path.Combine(_options.BuildingsDirectory, "Townhall2"));
        }
    }

    /// <inheritdoc />
    public TownHallSearchResult Search(Mat frame, Rect searchArea, bool singlePass)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (frame.Empty() || !IsAvailable)
        {
            return new TownHallSearchResult();
        }

        Rect area = ClampToFrame(searchArea, frame);
        string directoryA = Path.Combine(_options.BuildingsDirectory, "Townhall");
        string directoryB = Path.Combine(_options.BuildingsDirectory, "Townhall2");
        string snowA = Path.Combine(_options.BuildingsDirectory, "snow-Townhall");
        string snowB = Path.Combine(_options.BuildingsDirectory, "snow-Townhall2");

        // AutoIt: 2 lượt bình thường, 4 lượt khi theme tuyết, 1 lượt khi SinglePass.
        int retries = _options.SnowTheme ? 3 : 1;
        if (singlePass)
        {
            retries = 0;
        }

        for (int retry = 0; retry <= retries; retry++)
        {
            bool even = retry % 2 == 0;
            bool useSnow = _options.SnowTheme && retry < 2;
            string directory = useSnow
                ? (even ? snowA : snowB)
                : (even ? directoryA : directoryB);

            if (!Directory.Exists(directory))
            {
                continue;
            }

            IReadOnlyList<NamedTilePoint> results = _searcher.SearchDirectory(
                frame,
                directory,
                area,
                maxPoints: 1,
                _options.MinLevel,
                _options.MaxLevel);

            NamedTilePoint? best = ChooseBestResult(results);
            if (best is null)
            {
                if (retry < retries)
                {
                    _logger?.Debug($"imglocTHSearch: không thấy Town Hall ở {Path.GetFileName(directory)} (lượt {retry}).");
                }

                continue;
            }

            NamedTilePoint point = best.Value;
            int screenX = point.X;
            int screenY = point.Y;

            if (!_converter.FromScreen(screenX, screenY, out int villageX, out int villageY))
            {
                // AutoIt: ép áp dụng lại village offset rồi thử đổi toạ độ lần nữa.
                _logger?.Debug("Đổi toạ độ Town Hall thất bại — áp dụng lại village offset.");
                bool offsetReady = _converter.EnsureOffset();
                if (!offsetReady || !_converter.FromScreen(screenX, screenY, out villageX, out villageY))
                {
                    _logger?.Warning("Không đổi được toạ độ Town Hall sau khi làm mới village offset.");
                    return new TownHallSearchResult
                    {
                        ScreenX = screenX,
                        ScreenY = screenY,
                        Level = point.Level,
                        Template = point.Name,
                        CoordinateConversionFailed = true,
                    };
                }
            }

            _logger?.Info(
                $"imglocTHSearch: thấy {point.Name} (cấp {point.Level}) tại " +
                $"{screenX},{screenY} → làng {villageX},{villageY}.");

            return new TownHallSearchResult
            {
                Found = true,
                ScreenX = screenX,
                ScreenY = screenY,
                VillageX = villageX,
                VillageY = villageY,
                Level = point.Level,
                Template = point.Name,
            };
        }

        return new TownHallSearchResult();
    }

    /// <summary>
    /// AutoIt chọn kết quả có <c>objectlevel</c> cao nhất khi ImgLoc trả nhiều hơn một.
    /// </summary>
    private static NamedTilePoint? ChooseBestResult(IReadOnlyList<NamedTilePoint> results)
    {
        if (results.Count == 0)
        {
            return null;
        }

        NamedTilePoint best = results[0];
        for (int i = 1; i < results.Count; i++)
        {
            if (results[i].Level > best.Level)
            {
                best = results[i];
            }
        }

        return best;
    }

    private static Rect ClampToFrame(Rect area, Mat frame)
    {
        int left = System.Math.Max(0, area.X);
        int top = System.Math.Max(0, area.Y);
        int right = System.Math.Min(frame.Width, area.X + area.Width);
        int bottom = System.Math.Min(frame.Height, area.Y + area.Height);

        if (right <= left || bottom <= top)
        {
            return new Rect(0, 0, frame.Width, frame.Height);
        }

        return Rect.FromLTRB(left, top, right, bottom);
    }
}
