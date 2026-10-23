using System;
using System.Threading;
using System.Threading.Tasks;
using MyBot.Core.Abstractions;
using MyBot.Core.CV;
using OpenCvSharp;

namespace MyBot.Game.CoC.State.Zoom;

/// <summary>Ngữ cảnh làng hiện tại dùng khi zoom.</summary>
/// <param name="OnMainBase">True khi đang ở làng chính.</param>
/// <param name="OnBuilderBase">True khi đang ở Builder Base.</param>
/// <param name="OnBuilderBaseEnemy">True khi đang ở làng địch Builder Base.</param>
public sealed record ZoomContext(bool OnMainBase, bool OnBuilderBase = false, bool OnBuilderBaseEnemy = false);

/// <summary>Kết quả của một lần <c>SearchZoomOut</c>.</summary>
public sealed class ZoomSearchResult
{
    /// <summary>True khi làng đã ở đúng mức zoom (chuỗi bắt đầu bằng <c>zoomout:</c>).</summary>
    public bool IsZoomedOut { get; init; }

    /// <summary>Offset X của làng sau khi căn giữa.</summary>
    public int OffsetX { get; init; }

    /// <summary>Offset Y của làng sau khi căn giữa.</summary>
    public int OffsetY { get; init; }

    /// <summary>Thay đổi offset X so với lần đo trước (sau khi căn giữa).</summary>
    public int ChangeX { get; init; }

    /// <summary>Thay đổi offset Y so với lần đo trước (sau khi căn giữa).</summary>
    public int ChangeY { get; init; }

    /// <summary>Tên file stone dùng để xác nhận (giống hậu tố <c>zoomout:</c> của AutoIt).</summary>
    public string? StoneFile { get; init; }

    /// <summary>Kích thước làng đo được.</summary>
    public double VillageSize { get; init; }

    /// <summary>Hệ số zoom đo được.</summary>
    public double Zoom { get; init; }
}

/// <summary>
/// Port trung thành <c>ZoomOut()</c> / <c>DefaultZoomOut()</c> / <c>SearchZoomOut()</c>
/// của AutoIt: zoom từng nấc bằng minitouch ADB, đo lại làng sau mỗi nấc,
/// căn giữa làng bằng stone/tree và xử lý popup lỗi trong lúc zoom.
/// </summary>
public sealed class ZoomOutEngine
{
    private readonly ZoomOptions _options;
    private readonly IVillageMeasurer _measurer;
    private readonly IVillagePositionConverter? _positionConverter;
    private readonly Func<Mat> _capture;
    private readonly IInputBackend _input;
    private readonly IZoomCommand? _adbCommand;
    private readonly IZoomCommand? _wheelCommand;
    private readonly IProblemDetector _problemDetector;
    private readonly ITimeProvider _time;
    private readonly IBotLogger? _logger;
    private readonly IVillageCenterer? _quickCenterer;

    private int _searchFailures;
    private int _postZoomOuts = 1;
    private int _restartCount;
    private bool _offsetKnown;
    private int _offsetX;
    private int _offsetY;
    private double _offsetZoom = 1;

    /// <summary>Khởi tạo engine zoom.</summary>
    /// <param name="options">Tham số port từ AutoIt.</param>
    /// <param name="measurer">Bộ đo làng.</param>
    /// <param name="positionConverter">Bộ chuyển toạ độ của DLL gốc; null nếu không có.</param>
    /// <param name="capture">Hàm chụp khung hình mới (caller sở hữu Mat trả về).</param>
    /// <param name="input">Backend input để kéo căn giữa làng.</param>
    /// <param name="adbCommand">Lệnh zoom qua ADB minitouch; null nếu không có.</param>
    /// <param name="wheelCommand">Lệnh zoom dự phòng bằng con lăn; null nếu không có.</param>
    /// <param name="problemDetector">Bộ phát hiện popup lỗi.</param>
    /// <param name="time">Nguồn thời gian.</param>
    /// <param name="logger">Logger tuỳ chọn.</param>
    /// <param name="quickCenterer">Bộ căn giữa nhanh trước mỗi lần đo; null nếu không có.</param>
    public ZoomOutEngine(
        ZoomOptions options,
        IVillageMeasurer measurer,
        IVillagePositionConverter? positionConverter,
        Func<Mat> capture,
        IInputBackend input,
        IZoomCommand? adbCommand,
        IZoomCommand? wheelCommand,
        IProblemDetector problemDetector,
        ITimeProvider time,
        IBotLogger? logger = null,
        IVillageCenterer? quickCenterer = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _measurer = measurer ?? throw new ArgumentNullException(nameof(measurer));
        _positionConverter = positionConverter;
        _capture = capture ?? throw new ArgumentNullException(nameof(capture));
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _adbCommand = adbCommand;
        _wheelCommand = wheelCommand;
        _problemDetector = problemDetector ?? throw new ArgumentNullException(nameof(problemDetector));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _logger = logger;
        _quickCenterer = quickCenterer;
    }

    /// <summary>
    /// Cờ <c>$g_bSkipFirstZoomout</c>: lần đo đầu tiên không ép zoom thêm khi làng &gt; 480px.
    /// </summary>
    public bool SkipFirstZoomOut { get; set; }

    /// <summary>Số lần đo làng thất bại liên tiếp (<c>$g_aiSearchZoomOutCounter[0]</c>).</summary>
    public int SearchFailures => _searchFailures;

    /// <summary>Được gọi khi cần khởi động lại CoC; phải chờ tới màn hình chính.</summary>
    public Func<CancellationToken, Task>? RestartCoCAsync { get; set; }

    /// <summary>Được gọi khi cần reboot Android; phải chờ tới màn hình chính.</summary>
    public Func<CancellationToken, Task>? RebootAndroidAsync { get; set; }

    /// <summary>Được gọi khi phát hiện popup lỗi; trả true nếu đã đóng được.</summary>
    public Func<bool>? ClearObstacles { get; set; }

    /// <summary>
    /// Zoom out tới mức chuẩn. Tương đương <c>ZoomOut()</c> → <c>ZoomOutMEmu()</c> →
    /// <c>DefaultZoomOut("{F3}", 0, true)</c>.
    /// </summary>
    /// <param name="context">Ngữ cảnh làng.</param>
    /// <param name="cancellationToken">Token huỷ.</param>
    /// <returns>True nếu đã zoom xong (hoặc đã ở mức zoom chuẩn từ trước).</returns>
    public async Task<bool> ZoomOutAsync(ZoomContext context, CancellationToken cancellationToken = default)
    {
        _searchFailures = 0;
        _postZoomOuts = 1;

        ZoomSearchResult picture = await SearchZoomOutAsync(
            context, centerVillage: true, update: true, cancellationToken).ConfigureAwait(false);

        if (!picture.IsZoomedOut)
        {
            _logger?.Info("Zooming Out");
            await _time.DelayAsync(_options.DelayFirstMs, cancellationToken).ConfigureAwait(false);

            // AutoIt: chế độ 3 (WinAPI) không dùng minitouch.
            bool useAdb = _adbCommand is not null && _options.ZoomOutMode != 3;
            if (useAdb)
            {
                useAdb = _adbCommand!.TryZoomStep(0, context.OnBuilderBaseEnemy);
            }

            picture = await SearchZoomOutAsync(
                context, centerVillage: true, update: true, cancellationToken).ConfigureAwait(false);

            int i = 0;
            while (!picture.IsZoomedOut)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (useAdb)
                {
                    if (!_adbCommand!.TryZoomStep(i, context.OnBuilderBaseEnemy))
                    {
                        _logger?.Warning("Zoom ADB lỗi — chuyển sang zoom bằng con lăn.");
                        useAdb = false;
                    }
                }

                if (!useAdb)
                {
                    if (_wheelCommand is null)
                    {
                        _logger?.Error("Không có lệnh zoom dự phòng.");
                        return false;
                    }

                    await _time.DelayAsync(_options.DelayStepMs, cancellationToken).ConfigureAwait(false);
                    _ = _wheelCommand.TryWheelStep(control: true);
                }

                if (i > _options.SlowAfterAttempts)
                {
                    await _time.DelayAsync(_options.DelaySlowMs, cancellationToken).ConfigureAwait(false);
                }

                // AutoIt: sau 40 vòng ADB thì chuyển hẳn sang Ctrl + con lăn.
                if (useAdb
                    && _options.WheelAfterAttempts > 0
                    && i > _options.WheelAfterAttempts)
                {
                    _logger?.Info("Zoom ADB chưa xong — chuyển sang Ctrl + con lăn.");
                    useAdb = false;
                    i = 0;
                }

                if (i > _options.MaxAttempts)
                {
                    _logger?.Warning("Zoom out vượt quá số lần tối đa.");
                    return false;
                }

                await CheckProblemAsync(cancellationToken).ConfigureAwait(false);

                i++;
                picture = await SearchZoomOutAsync(
                    context, centerVillage: true, update: true, cancellationToken).ConfigureAwait(false);
            }
        }

        return true;
    }

    /// <summary>
    /// Kéo làng về giữa bằng điểm mốc nhanh, lặp tối đa
    /// <see cref="ZoomOptions.QuickCenterAttempts"/> lần cho tới khi độ lệch nằm
    /// trong dung sai. Dùng được độc lập sau khi zoom hoặc trước khi OCR.
    /// </summary>
    /// <param name="context">Ngữ cảnh làng.</param>
    /// <param name="cancellationToken">Token huỷ.</param>
    /// <returns>True nếu làng đã ở giữa (hoặc không cần căn giữa).</returns>
    public async Task<bool> CenterVillageAsync(
        ZoomContext context,
        CancellationToken cancellationToken = default)
    {
        if (_quickCenterer is null || !context.OnMainBase || context.OnBuilderBaseEnemy)
        {
            return false;
        }

        for (int attempt = 0; attempt < _options.QuickCenterAttempts; attempt++)
        {
            using Mat frame = _capture() ?? new Mat();
            VillageCenterResult result = await _quickCenterer
                .TryCenterAsync(frame, context, cancellationToken)
                .ConfigureAwait(false);

            if (!result.Found)
            {
                return false;
            }

            if (!result.Dragged)
            {
                return true;
            }

            // AutoIt: $g_aiSearchZoomOutCounter[0] = 0
            _searchFailures = 0;
        }

        return true;
    }

    /// <summary>
    /// Đo làng và quyết định đã zoom đủ chưa. Tương đương <c>SearchZoomOut()</c>.
    /// </summary>
    /// <param name="context">Ngữ cảnh làng.</param>
    /// <param name="centerVillage">True để kéo làng về giữa khi lệch.</param>
    /// <param name="update">True để cập nhật village offset toàn cục.</param>
    /// <param name="cancellationToken">Token huỷ.</param>
    /// <returns>Kết quả đo và trạng thái zoom.</returns>
    public async Task<ZoomSearchResult> SearchZoomOutAsync(
        ZoomContext context,
        bool centerVillage,
        bool update,
        CancellationToken cancellationToken = default)
    {
        // AutoIt chạy ZoomOutHelperQuick TRƯỚC khi chọn bộ ảnh stone/tree:
        // nếu căn giữa được thì bộ đếm đo thất bại về 0 → dùng ảnh chính.
        // If $bCenterVillage And Not $g_bOnBuilderBaseEnemyVillage Then
        //     If ZoomOutHelperQuick($sSource) Then _CaptureRegion2()
        if (centerVillage && !context.OnBuilderBaseEnemy && _quickCenterer is not null)
        {
            using Mat quickFrame = _capture() ?? new Mat();
            VillageCenterResult quick = await _quickCenterer
                .TryCenterAsync(quickFrame, context, cancellationToken)
                .ConfigureAwait(false);

            if (quick.Dragged)
            {
                // AutoIt: $g_aiSearchZoomOutCounter[0] = 0
                _searchFailures = 0;
            }
        }

        if (_searchFailures == 5)
        {
            _logger?.Info("Thử bộ ảnh đo làng thứ cấp (2stone/2tree)...");
        }

        string stonePrefix = _searchFailures < 5 ? "stone" : "2stone";
        string treePrefix = _searchFailures < 5 ? "tree" : "2tree";

        var request = new VillageMeasureRequest(
            stonePrefix,
            treePrefix,
            context.OnMainBase,
            context.OnBuilderBase,
            context.OnBuilderBaseEnemy,
            (x, y, dx, dy) => CenterVillage(x, y, dx, dy, context),
            _searchFailures);

        Mat frame = _capture();
        if (frame is null)
        {
            frame = new Mat();
        }

        bool needsRecapture = false;
        VillageMeasurement measurement;
        try
        {
            measurement = _measurer.Measure(frame, request);

            // GetVillageSize căn giữa khi scenery lệch quá 75px rồi ép chụp lại.
            int recaptures = 0;
            while (measurement.NeedsRecapture && recaptures < 2)
            {
                await _time.DelayAsync(150, cancellationToken).ConfigureAwait(false);
                frame.Dispose();
                frame = _capture() ?? new Mat();
                measurement = _measurer.Measure(frame, request);
                recaptures++;
                needsRecapture = true;
            }

            if (needsRecapture && measurement.NeedsRecapture)
            {
                measurement = new VillageMeasurement();
            }

        }
        finally
        {
            frame.Dispose();
        }

        if (_searchFailures > 0)
        {
            await _time.DelayAsync(1000, cancellationToken).ConfigureAwait(false);
        }

        var result = new ZoomSearchResult
        {
            OffsetX = measurement.OffsetX,
            OffsetY = measurement.OffsetY,
            VillageSize = measurement.Size,
            Zoom = measurement.Zoom,
        };

        if (measurement.Found)
        {
            int minSize = (int)System.Math.Round(measurement.ReferenceSize * _options.MinZoomFactor);
            int maxSize = (int)System.Math.Round(measurement.ReferenceSize * _options.MaxZoomFactor);

            if (measurement.ReferenceSize > 0)
            {
                _logger?.Debug($"Ref: {measurement.ReferenceSize}, Min: {minSize}, Max: {maxSize}");
            }

            if (measurement.Size > minSize && measurement.Size < maxSize)
            {
                result = new ZoomSearchResult
                {
                    IsZoomedOut = true,
                    OffsetX = measurement.OffsetX,
                    OffsetY = measurement.OffsetY,
                    StoneFile = measurement.StoneFile,
                    VillageSize = measurement.Size,
                    Zoom = measurement.Zoom,
                };

                bool offsetChanged = !_offsetKnown
                    || measurement.OffsetX != _offsetX
                    || measurement.OffsetY != _offsetY;

                bool zoomChanged = System.Math.Abs(measurement.Zoom - _offsetZoom) > 1e-9;

                if (centerVillage
                    && (measurement.OffsetX != 0 || measurement.OffsetY != 0)
                    && (!update || offsetChanged)
                    && !context.OnBuilderBaseEnemy)
                {
                    _logger?.Debug(
                        $"Căn giữa làng theo offset {measurement.OffsetX}, {measurement.OffsetY}.");

                    (int scrollX, int scrollY) = ChooseScrollPoint(measurement, context);
                    CenterVillage(scrollX, scrollY, measurement.OffsetX, measurement.OffsetY, context);
                    await _time.DelayAsync(250, cancellationToken).ConfigureAwait(false);

                    ZoomSearchResult centered = await SearchZoomOutAsync(
                        context, centerVillage: false, update: update, cancellationToken).ConfigureAwait(false);

                    return new ZoomSearchResult
                    {
                        IsZoomedOut = centered.IsZoomedOut,
                        OffsetX = centered.OffsetX,
                        OffsetY = centered.OffsetY,
                        ChangeX = centered.OffsetX - measurement.OffsetX,
                        ChangeY = centered.OffsetY - measurement.OffsetY,
                        StoneFile = centered.StoneFile,
                        VillageSize = centered.VillageSize,
                        Zoom = centered.Zoom,
                    };
                }

                if (update)
                {
                    if (offsetChanged || zoomChanged)
                    {
                        _logger?.Debug(
                            $"Village Offset cập nhật: {measurement.OffsetX}, {measurement.OffsetY}, {measurement.Zoom}");
                    }

                    // AutoIt gọi setVillageOffset mỗi lần SearchZoomOut được chấp nhận.
                    SetVillageOffset(measurement.OffsetX, measurement.OffsetY, measurement.Zoom);
                }
            }
            else
            {
                // Chỉ tìm thấy một điểm mốc (hoặc làng quá lớn): căn giữa rồi ép zoom thêm.
                if (!(context.OnBuilderBaseEnemy && _searchFailures == 0))
                {
                    if (measurement.StoneFile is null && measurement.TreeFile is not null)
                    {
                        CenterVillage(measurement.TreeX, measurement.TreeY, measurement.OffsetX, measurement.OffsetY, context);
                    }
                    else if (measurement.TreeFile is null && measurement.StoneFile is not null)
                    {
                        CenterVillage(measurement.StoneX, measurement.StoneY, measurement.OffsetX, measurement.OffsetY, context);
                    }
                }
            }
        }

        if (update)
        {
            if (!result.IsZoomedOut)
            {
                if (_searchFailures > _options.MaxMeasureFailures)
                {
                    _searchFailures = 0;
                    await RestartAfterMeasureFailuresAsync(cancellationToken).ConfigureAwait(false);
                    return await SearchZoomOutAsync(context, centerVillage, update: true, cancellationToken)
                        .ConfigureAwait(false);
                }

                _searchFailures++;
            }
            else if (measurement.Size > _options.ForceZoomAboveSize)
            {
                if (!SkipFirstZoomOut)
                {
                    result = CopyWithoutZoom(result);
                }
                else if (_postZoomOuts > 0 && _searchFailures > 0)
                {
                    _postZoomOuts--;
                    result = CopyWithoutZoom(result);
                }
            }

            SkipFirstZoomOut = true;
        }

        return result;
    }

    private async Task RestartAfterMeasureFailuresAsync(CancellationToken cancellationToken)
    {
        _restartCount++;
        if (_restartCount <= _options.MaxCoCRestarts)
        {
            _logger?.Info("Khởi động lại CoC để reset zoom...");
            if (RestartCoCAsync is not null)
            {
                await RestartCoCAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        else
        {
            _logger?.Info("Reboot Android để reset zoom...");
            _restartCount = 0;
            if (RebootAndroidAsync is not null)
            {
                await RebootAndroidAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        await _time.DelayAsync(1000, cancellationToken).ConfigureAwait(false);
    }

    private async Task CheckProblemAsync(CancellationToken cancellationToken)
    {
        using Mat frame = _capture() ?? new Mat();
        if (frame.Empty() || !_problemDetector.IsProblemAffect(frame))
        {
            return;
        }

        _logger?.Warning("Phát hiện cửa sổ lỗi trong lúc zoom.");
        if (ClearObstacles?.Invoke() == true)
        {
            _logger?.Info("Đã đóng cửa sổ lỗi, tiếp tục zoom.");
        }

        await _time.DelayAsync(100, cancellationToken).ConfigureAwait(false);
    }

    private (int X, int Y) ChooseScrollPoint(VillageMeasurement measurement, ZoomContext context)
    {
        if (measurement.StoneFile is not null
            && IsCoordSafe(measurement.StoneX, measurement.StoneY, context))
        {
            return (measurement.StoneX, measurement.StoneY);
        }

        if (measurement.TreeFile is not null
            && IsCoordSafe(measurement.TreeX, measurement.TreeY, context))
        {
            return (measurement.TreeX, measurement.TreeY);
        }

        return (_options.DragX, _options.DragY);
    }

    private void CenterVillage(int x, int y, int offsetX, int offsetY, ZoomContext context)
    {
        (int scrollX, int scrollY) = IsCoordSafe(x, y, context)
            ? (x, y)
            : (_options.DragX, _options.DragY);

        _logger?.Debug(
            $"CenterVillage tại {scrollX},{scrollY} offset {offsetX},{offsetY}.");

        _input.Swipe(scrollX, scrollY, scrollX - offsetX, scrollY - offsetY, _options.SwipeDurationMs);
    }

    /// <summary>Port <c>IsCoordSafe</c>: tránh kéo vào các nút HUD.</summary>
    /// <param name="x">Toạ độ X.</param>
    /// <param name="y">Toạ độ Y.</param>
    /// <param name="context">Ngữ cảnh làng.</param>
    /// <returns>True nếu toạ độ an toàn để bắt đầu kéo.</returns>
    public bool IsCoordSafe(int x, int y, ZoomContext context)
    {
        int midOffset = _options.MidOffsetY;
        int bottomOffset = _options.BottomOffsetY;

        if (context.OnMainBase && x < 82 && y > 427 + bottomOffset)
        {
            return false; // nút War
        }

        if (x < 72 && y > 270 + midOffset && y < 345 + midOffset)
        {
            return false; // tab CHAT
        }

        if (y < 63)
        {
            return false; // nút Builder/Shield
        }

        if (context.OnMainBase && x > 692 && y > 126 + midOffset && y < 180 + midOffset)
        {
            return false; // nút Gems
        }

        return true;
    }

    private void SetVillageOffset(int x, int y, double zoom)
    {
        _offsetX = x;
        _offsetY = y;
        _offsetZoom = zoom;
        _offsetKnown = true;
        _positionConverter?.SetOffset(x, y, zoom);
    }

    private static ZoomSearchResult CopyWithoutZoom(ZoomSearchResult source)
    {
        return new ZoomSearchResult
        {
            IsZoomedOut = false,
            OffsetX = source.OffsetX,
            OffsetY = source.OffsetY,
            StoneFile = source.StoneFile,
            VillageSize = source.VillageSize,
            Zoom = source.Zoom,
        };
    }
}
