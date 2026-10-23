using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MyBot.Core.Abstractions;
using MyBot.Core.CV;
using OpenCvSharp;

namespace MyBot.Game.CoC.State.Zoom;

/// <summary>Kết quả một lần căn giữa làng nhanh (<c>ZoomOutHelperQuick</c>).</summary>
public sealed class VillageCenterResult
{
    /// <summary>True nếu tìm thấy điểm mốc (tree/stone) trong vùng tìm kiếm.</summary>
    public bool Found { get; init; }

    /// <summary>True nếu đã gửi thao tác kéo làng.</summary>
    public bool Dragged { get; init; }

    /// <summary>Độ lệch X so với toạ độ tham chiếu ghi trong tên template.</summary>
    public int OffsetX { get; init; }

    /// <summary>Độ lệch Y so với toạ độ tham chiếu ghi trong tên template.</summary>
    public int OffsetY { get; init; }

    /// <summary>Toạ độ X của điểm mốc tìm được.</summary>
    public int AnchorX { get; init; }

    /// <summary>Toạ độ Y của điểm mốc tìm được.</summary>
    public int AnchorY { get; init; }

    /// <summary>Tên template của điểm mốc.</summary>
    public string? AnchorName { get; init; }
}

/// <summary>
/// Căn giữa làng bằng điểm mốc cố định. Port <c>ZoomOutHelperQuick</c> của AutoIt:
/// tìm tree ở góc trên phải, nếu không có thì tìm stone ở giữa trái, rồi kéo làng
/// đúng bằng độ lệch so với toạ độ tham chiếu ghi trong tên template.
/// </summary>
public interface IVillageCenterer
{
    /// <summary>
    /// Thử căn giữa làng trên một khung hình. Có thể kéo làng và chờ camera ổn định.
    /// </summary>
    /// <param name="frame">Khung hình BGR hiện tại.</param>
    /// <param name="context">Ngữ cảnh làng.</param>
    /// <param name="cancellationToken">Token huỷ.</param>
    /// <returns>Kết quả căn giữa.</returns>
    Task<VillageCenterResult> TryCenterAsync(
        Mat frame,
        ZoomContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Port <c>ZoomOutHelperQuick</c> của AutoIt. Khác với việc đo kích thước làng,
/// bước này chỉ cần một điểm mốc và chạy trước mỗi lần đo để tránh rơi vào
/// nhánh tìm scenery tốn kém khi làng chỉ bị lệch.
/// </summary>
public sealed class VillageCenterer : IVillageCenterer
{
    private static readonly Regex TreeNameRegex =
        new(@"^tree([0-9A-Z]+)-(\d+)-(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex StoneNameRegex =
        new(@"^stone([0-9A-Z]+)-(\d+)-(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly ZoomOptions _options;
    private readonly ITileSearcher _searcher;
    private readonly IInputBackend _input;
    private readonly string _anchorDirectory;
    private readonly ITimeProvider _time;
    private readonly IBotLogger? _logger;
    private readonly Func<bool>? _clearScreen;

    /// <summary>Khởi tạo bộ căn giữa làng.</summary>
    /// <param name="options">Tham số port từ AutoIt.</param>
    /// <param name="searcher">Bộ tìm template ImgLoc.</param>
    /// <param name="input">Backend input để kéo làng.</param>
    /// <param name="anchorDirectory">Thư mục template mốc (<c>imgxml/village/NormalVillage</c>).</param>
    /// <param name="time">Nguồn thời gian.</param>
    /// <param name="logger">Logger tuỳ chọn.</param>
    /// <param name="clearScreen">Callback đóng bảng thông tin đang mở (AutoIt: <c>ClearScreen</c>).</param>
    public VillageCenterer(
        ZoomOptions options,
        ITileSearcher searcher,
        IInputBackend input,
        string anchorDirectory,
        ITimeProvider time,
        IBotLogger? logger = null,
        Func<bool>? clearScreen = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _searcher = searcher ?? throw new ArgumentNullException(nameof(searcher));
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _anchorDirectory = anchorDirectory ?? throw new ArgumentNullException(nameof(anchorDirectory));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _logger = logger;
        _clearScreen = clearScreen;
    }

    /// <inheritdoc />
    public async Task<VillageCenterResult> TryCenterAsync(
        Mat frame,
        ZoomContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(context);

        if (frame.Empty() || !_searcher.IsAvailable)
        {
            return new VillageCenterResult();
        }

        // AutoIt: If Not isOnMainVillage(True) Or $g_bOnBuilderBaseEnemyVillage Then Return False
        if (!context.OnMainBase || context.OnBuilderBaseEnemy)
        {
            return new VillageCenterResult();
        }

        Anchor? anchor = FindAnchor(frame);
        if (anchor is null)
        {
            _logger?.Debug("[Căn giữa] Không thấy tree/stone trong vùng mốc.");
            return new VillageCenterResult();
        }

        int offsetX = anchor.Value.X - anchor.Value.ReferenceX;
        int offsetY = anchor.Value.Y - anchor.Value.ReferenceY;

        var result = new VillageCenterResult
        {
            Found = true,
            OffsetX = offsetX,
            OffsetY = offsetY,
            AnchorX = anchor.Value.X,
            AnchorY = anchor.Value.Y,
            AnchorName = anchor.Value.Name,
        };

        // AutoIt: If Abs($iOffsetX) <= 15 And Abs($iOffsetY) <= 15 Then Return False
        if (System.Math.Abs(offsetX) <= _options.QuickCenterTolerancePx
            && System.Math.Abs(offsetY) <= _options.QuickCenterTolerancePx)
        {
            _logger?.Debug(
                $"[Căn giữa] Làng đã ở giữa (lệch {offsetX},{offsetY}, mốc {anchor.Value.Name}).");
            return result;
        }

        // AutoIt luôn kéo từ $aCenterHomeVillageClickDrag, không dùng toạ độ mốc.
        int scrollX = _options.DragX;
        int scrollY = _options.DragY;

        _logger?.Info(
            $"[Căn giữa] Kéo làng {offsetX},{offsetY} từ {scrollX},{scrollY} (mốc {anchor.Value.Name}).");

        // AutoIt: ClearScreen() trước khi kéo để đóng bảng thông tin đang mở.
        _clearScreen?.Invoke();

        _input.Swipe(
            scrollX,
            scrollY,
            scrollX - offsetX,
            scrollY - offsetY,
            _options.SwipeDurationMs);

        // AutoIt: _Sleep(250)
        await _time.DelayAsync(_options.CenterDelayMs, cancellationToken).ConfigureAwait(false);

        return new VillageCenterResult
        {
            Found = true,
            Dragged = true,
            OffsetX = offsetX,
            OffsetY = offsetY,
            AnchorX = anchor.Value.X,
            AnchorY = anchor.Value.Y,
            AnchorName = anchor.Value.Name,
        };
    }

    /// <summary>
    /// Tìm điểm mốc: tree ở vùng (430,20)-(750,200), nếu không có thì stone ở
    /// vùng (60,330)-(430,560). Đúng thứ tự và vùng của <c>ZoomOutHelperQuick</c>.
    /// </summary>
    private Anchor? FindAnchor(Mat frame)
    {
        var treeArea = Rect.FromLTRB(430, 20, 750, 200);
        Anchor? tree = SearchRegion(frame, treeArea, TreeNameRegex);
        if (tree is not null)
        {
            return tree;
        }

        var stoneArea = Rect.FromLTRB(60, 330, 430, 560);
        return SearchRegion(frame, stoneArea, StoneNameRegex);
    }

    private Anchor? SearchRegion(Mat frame, Rect area, Regex nameRegex)
    {
        IReadOnlyList<NamedTilePoint> found = _searcher.SearchDirectory(
            frame,
            _anchorDirectory,
            area,
            maxPoints: 1);

        foreach (NamedTilePoint point in found)
        {
            Match match = nameRegex.Match(point.Name);
            if (!match.Success)
            {
                continue;
            }

            int referenceX = ParseInt(match.Groups[2].Value);
            int referenceY = ParseInt(match.Groups[3].Value);

            return new Anchor(
                point.Name,
                point.X,
                point.Y,
                referenceX,
                referenceY);
        }

        return null;
    }

    private static int ParseInt(string value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)
            ? result
            : 0;
    }

    private readonly record struct Anchor(
        string Name,
        int X,
        int Y,
        int ReferenceX,
        int ReferenceY);
}
