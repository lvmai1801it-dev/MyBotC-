using System;
using System.Threading;
using System.Threading.Tasks;
using MyBot.Core.Abstractions;
using MyBot.Core.CV;
using OpenCvSharp;

namespace MyBot.Game.CoC.Village;

/// <summary>Vị trí Town Hall trong hệ toạ độ làng tham chiếu.</summary>
/// <param name="X">Toạ độ X tham chiếu.</param>
/// <param name="Y">Toạ độ Y tham chiếu.</param>
public readonly record struct TownHallPosition(int X, int Y)
{
    /// <summary>Vị trí chưa xác định (<c>$g_aiTownHallPos = [-1, -1]</c>).</summary>
    public static TownHallPosition None => new(-1, -1);

    /// <summary>True khi vị trí đã được xác định (khác <c>-1, -1</c>).</summary>
    public bool IsValid => X >= 0 && Y >= 0;
}

/// <summary>Tham số phát hiện Town Hall, port từ <c>AutoDetectTownHall</c>.</summary>
public sealed class TownHallDetectorOptions
{
    /// <summary>Chiều rộng vùng game dùng khi kẹp vùng tìm cục bộ. AutoIt: 860.</summary>
    public int GameWidth { get; init; } = 860;

    /// <summary>Chiều cao vùng game dùng khi kẹp vùng tìm cục bộ. AutoIt: 780.</summary>
    public int GameHeight { get; init; } = 780;

    /// <summary>Bán kính tìm cục bộ quanh vị trí đã lưu. AutoIt: 90 px.</summary>
    public int LocalSearchRadius { get; init; } = 90;

    /// <summary>Toạ độ X vùng đọc tên công trình. AutoIt: 242.</summary>
    public int BuildingInfoX { get; init; } = 242;

    /// <summary>Toạ độ Y vùng đọc tên công trình. AutoIt: <c>475 + $g_iBottomOffsetY</c>.</summary>
    public int BuildingInfoY { get; init; } = BuildingInfoReader.DefaultY;

    /// <summary>Thời gian chờ sau khi chạm Town Hall trước khi đọc tên. AutoIt: 500 ms.</summary>
    public int ClickDelayMs { get; init; } = 500;
}

/// <summary>Kết quả một lần phát hiện Town Hall.</summary>
public sealed class TownHallDetectionResult
{
    /// <summary>True khi đã tìm thấy và OCR xác nhận đúng Town Hall.</summary>
    public bool Success { get; init; }

    /// <summary>Cấp Town Hall đọc được từ bảng thông tin.</summary>
    public int Level { get; init; }

    /// <summary>Toạ độ X làng tham chiếu của Town Hall.</summary>
    public int VillageX { get; init; }

    /// <summary>Toạ độ Y làng tham chiếu của Town Hall.</summary>
    public int VillageY { get; init; }

    /// <summary>Toạ độ X màn hình đã chạm để mở bảng thông tin.</summary>
    public int ScreenX { get; init; }

    /// <summary>Toạ độ Y màn hình đã chạm để mở bảng thông tin.</summary>
    public int ScreenY { get; init; }

    /// <summary>Tên template Town Hall khớp được.</summary>
    public string? Template { get; init; }

    /// <summary>Mô tả kết quả để ghi log.</summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>Phát hiện Town Hall của làng mình (port <c>AutoDetectTownHall</c>).</summary>
public interface ITownHallDetector
{
    /// <summary>
    /// Tìm Town Hall: thử vùng ±90 px quanh vị trí đã lưu trước, sau đó quét toàn làng;
    /// chạm vào ứng viên và xác nhận bằng OCR tên/cấp công trình.
    /// </summary>
    /// <param name="capture">Hàm chụp khung hình mới.</param>
    /// <param name="saved">Vị trí đã lưu trong profile; null hoặc không hợp lệ để quét toàn làng.</param>
    /// <param name="cancellationToken">Token huỷ.</param>
    /// <returns>Kết quả phát hiện.</returns>
    Task<TownHallDetectionResult> AutoDetectAsync(
        Func<Mat> capture,
        TownHallPosition? saved = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Port <c>AutoDetectTownHall</c> của AutoIt: tìm cục bộ trước để tiết kiệm thời gian,
/// fallback quét toàn làng, rồi chạm và đọc bảng thông tin để chắc chắn đó là Town Hall.
/// </summary>
public sealed class TownHallDetector : ITownHallDetector
{
    private readonly ITownHallSearcher _searcher;
    private readonly IVillagePositionConverter _converter;
    private readonly IBuildingInfoReader _buildingInfo;
    private readonly IInputBackend _input;
    private readonly IMainScreenCleaner _cleaner;
    private readonly ITimeProvider _time;
    private readonly TownHallDetectorOptions _options;
    private readonly IBotLogger? _logger;

    /// <summary>Khởi tạo bộ phát hiện Town Hall.</summary>
    /// <param name="searcher">Bộ tìm Town Hall bằng ImgLoc.</param>
    /// <param name="converter">Bộ chuyển toạ độ làng ↔ màn hình.</param>
    /// <param name="buildingInfo">Bộ đọc bảng thông tin công trình.</param>
    /// <param name="input">Backend input để chạm Town Hall.</param>
    /// <param name="cleaner">Bộ đóng bảng thông tin đang mở.</param>
    /// <param name="time">Nguồn thời gian.</param>
    /// <param name="options">Tham số phát hiện.</param>
    /// <param name="logger">Logger tuỳ chọn.</param>
    public TownHallDetector(
        ITownHallSearcher searcher,
        IVillagePositionConverter converter,
        IBuildingInfoReader buildingInfo,
        IInputBackend input,
        IMainScreenCleaner cleaner,
        ITimeProvider time,
        TownHallDetectorOptions options,
        IBotLogger? logger = null)
    {
        _searcher = searcher ?? throw new ArgumentNullException(nameof(searcher));
        _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        _buildingInfo = buildingInfo ?? throw new ArgumentNullException(nameof(buildingInfo));
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _cleaner = cleaner ?? throw new ArgumentNullException(nameof(cleaner));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TownHallDetectionResult> AutoDetectAsync(
        Func<Mat> capture,
        TownHallPosition? saved = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capture);

        TownHallPosition savedPosition = saved ?? TownHallPosition.None;
        TownHallSearchResult found = new();
        Mat? frame = null;

        try
        {
            if (savedPosition.IsValid)
            {
                // AutoIt: imglocTHSearch(True, True, True, $sLocalArea, True)
                frame = capture() ?? new Mat();
                found = _searcher.Search(frame, LocalArea(savedPosition), singlePass: true);
            }

            if (!found.Found)
            {
                // AutoIt: imglocTHSearch(True, True, Not $bLocalSearch) — dùng lại ảnh
                // vừa chụp nếu đã quét cục bộ, tránh chụp hai lần.
                frame ??= capture() ?? new Mat();
                found = _searcher.Search(
                    frame,
                    Rect.FromLTRB(0, 0, frame.Width, frame.Height),
                    singlePass: false);
            }
        }
        finally
        {
            frame?.Dispose();
        }

        if (!found.Found)
        {
            _logger?.Info("Town Hall: không tìm thấy trong vùng tìm kiếm.");
            return new TownHallDetectionResult
            {
                Message = found.CoordinateConversionFailed
                    ? "Không đổi được toạ độ Town Hall."
                    : "Không tìm thấy Town Hall.",
            };
        }

        if (!_converter.ToScreen(found.VillageX, found.VillageY, out int screenX, out int screenY))
        {
            _logger?.Warning("Town Hall: không đổi được toạ độ làng sang toạ độ màn hình.");
            return new TownHallDetectionResult
            {
                VillageX = found.VillageX,
                VillageY = found.VillageY,
                Template = found.Template,
                Message = "Không đổi được toạ độ Town Hall sang màn hình.",
            };
        }

        _logger?.Debug($"BuildingClick Town Hall tại {screenX},{screenY}.");
        _input.Tap(screenX, screenY);
        await _time.DelayAsync(_options.ClickDelayMs, cancellationToken).ConfigureAwait(false);

        BuildingInfoResult info = await _buildingInfo
            .ReadAsync(capture, _options.BuildingInfoX, _options.BuildingInfoY, cancellationToken)
            .ConfigureAwait(false);

        bool isTownHall = info.Name.Contains("Town", StringComparison.Ordinal) && info.Level >= 2;

        // AutoIt gọi ClearScreen() ở cả nhánh thành công lẫn nhánh đọc sai.
        await _cleaner.ClearScreenAsync(capture, cancellationToken).ConfigureAwait(false);

        if (!isTownHall)
        {
            _logger?.Info(
                $"Town Hall: OCR không xác nhận (tên '{info.Name}', cấp {info.Level}).");
            return new TownHallDetectionResult
            {
                VillageX = found.VillageX,
                VillageY = found.VillageY,
                Template = found.Template,
                Message = $"OCR không xác nhận Town Hall (tên '{info.Name}', cấp {info.Level}).",
            };
        }

        _logger?.Info(
            $"Town Hall Detected: ({found.VillageX},{found.VillageY}), " +
            $"Level: {info.Level} ({found.Template}).");

        return new TownHallDetectionResult
        {
            Success = true,
            Level = info.Level,
            VillageX = found.VillageX,
            VillageY = found.VillageY,
            ScreenX = screenX,
            ScreenY = screenY,
            Template = found.Template,
            Message = $"Town Hall cấp {info.Level}.",
        };
    }

    /// <summary>
    /// Vùng tìm cục bộ ±90 px quanh vị trí đã lưu, kẹp trong kích thước vùng game
    /// (AutoIt: <c>GetDiamondFromRect2</c> sau khi kẹp 0..<c>$g_iGAME_WIDTH/HEIGHT</c>).
    /// </summary>
    private Rect LocalArea(TownHallPosition saved)
    {
        int radius = _options.LocalSearchRadius;
        int left = System.Math.Clamp(saved.X - radius, 0, _options.GameWidth);
        int top = System.Math.Clamp(saved.Y - radius, 0, _options.GameHeight);
        int right = System.Math.Clamp(saved.X + radius, 0, _options.GameWidth);
        int bottom = System.Math.Clamp(saved.Y + radius, 0, _options.GameHeight);
        return Rect.FromLTRB(left, top, right, bottom);
    }
}
