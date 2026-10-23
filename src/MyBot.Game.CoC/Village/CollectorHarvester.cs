using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MyBot.Core.Abstractions;
using MyBot.Core.AntiBan;
using MyBot.Game.CoC.Detection;
using MyBot.Game.CoC.Math;
using OpenCvSharp;

namespace MyBot.Game.CoC.Village;

/// <summary>
/// Quét frame làng, tìm mỏ vàng / elixir / dark elixir đầy rồi tap để thu hoạch.
///
/// parity: <c>Collect</c> @ COCBot/functions/Village/Collect.au3:17 và
/// <c>returnMultipleMatchesOwnVillage</c> @ COCBot/functions/Search/multiSearch.au3:180.
/// </summary>
public class CollectorHarvester
{
    // Ref không có ngưỡng số: ImgLoc nhận ngưỡng bên trong SearchMultipleTilesBetweenLevels.
    // Giữ 0.6 theo spec V-01.
    private const double DefaultThreshold = 0.6;

    // Dedupe theo EuclideanClusterer; 20 px là bán kính mặc định của lớp sẵn có.
    private const double DuplicateDistancePx = 20.0;

    // parity: $DELAYCOLLECT2 (250 ms) @ COCBot/functions/Config/DelayTimes.au3:205.
    private const int TapDelayMeanMs = 250;
    private const int TapDelayStdMs = 60;
    private const int TapDelayMinMs = 100;
    private const int TapDelayMaxMs = 400;

    // parity: $DELAYCOLLECT3 (800 ms) @ COCBot/functions/Config/DelayTimes.au3:206.
    private const int PostCollectDelayMs = 800;

    private readonly ITemplateMatcher _matcher;
    private readonly IInputBackend _input;
    private readonly HumanDelay _delay;
    private readonly Mat? _goldCollector;
    private readonly Mat? _elixirCollector;
    private readonly Mat? _darkElixirCollector;
    private readonly Mat? _storageFull;

    /// <summary>Các vị trí khớp thô của lần quét gần nhất, dùng cho ảnh debug khoanh vùng.</summary>
    public IReadOnlyList<MatchResult> LastMatches { get; private set; } = Array.Empty<MatchResult>();

    /// <summary>Tạo harvester chỉ với template truyền theo từng lần gọi (tương thích INT-1).</summary>
    /// <param name="matcher">Bộ khớp template.</param>
    /// <param name="input">Backend tap.</param>
    /// <param name="delay">Bộ sinh delay.</param>
    public CollectorHarvester(ITemplateMatcher matcher, IInputBackend input, HumanDelay delay)
        : this(matcher, input, delay, null, null, null, null)
    {
    }

    /// <summary>Tạo harvester với bộ template cố định cho cả ba loại mỏ và kho đầy.</summary>
    /// <param name="matcher">Bộ khớp template.</param>
    /// <param name="input">Backend tap.</param>
    /// <param name="delay">Bộ sinh delay.</param>
    /// <param name="goldCollector">Template mỏ vàng đầy; null để bỏ qua.</param>
    /// <param name="elixirCollector">Template mỏ elixir đầy; null để bỏ qua.</param>
    /// <param name="darkElixirCollector">Template mỏ dark elixir đầy; null để bỏ qua.</param>
    /// <param name="storageFull">Template biểu tượng kho đầy; null để bỏ qua kiểm tra.</param>
    public CollectorHarvester(
        ITemplateMatcher matcher,
        IInputBackend input,
        HumanDelay delay,
        Mat? goldCollector,
        Mat? elixirCollector,
        Mat? darkElixirCollector,
        Mat? storageFull)
    {
        _matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _delay = delay ?? throw new ArgumentNullException(nameof(delay));
        _goldCollector = goldCollector;
        _elixirCollector = elixirCollector;
        _darkElixirCollector = darkElixirCollector;
        _storageFull = storageFull;
    }

    /// <summary>Quét và tap toàn bộ mỏ dùng bộ template của instance.</summary>
    /// <param name="frame">Ảnh làng hiện tại.</param>
    /// <param name="matcher">Bộ khớp template dùng cho lượt này.</param>
    /// <param name="input">Backend tap dùng cho lượt này.</param>
    /// <param name="ct">Token huỷ.</param>
    /// <returns>Kết quả thu hoạch.</returns>
    public Task<CollectorResult> HarvestAllAsync(
        Mat frame,
        ITemplateMatcher matcher,
        IInputBackend input,
        CancellationToken ct = default)
        => HarvestAsync(frame, matcher, input, _goldCollector, _elixirCollector, _darkElixirCollector, _storageFull, ct);

    /// <summary>Overload INT-1: quét một template duy nhất (mỏ vàng) và trả số điểm đã tap.</summary>
    /// <param name="frame">Ảnh làng hiện tại.</param>
    /// <param name="template">Template mỏ vàng.</param>
    /// <param name="ct">Token huỷ.</param>
    /// <returns>Số điểm đã tap.</returns>
    public async Task<int> HarvestAllAsync(Mat frame, Mat template, CancellationToken ct = default)
    {
        CollectorResult result = await HarvestAsync(
            frame, _matcher, _input, template, null, null, null, ct).ConfigureAwait(false);
        return result.Tapped;
    }

    private async Task<CollectorResult> HarvestAsync(
        Mat frame,
        ITemplateMatcher matcher,
        IInputBackend input,
        Mat? goldCollector,
        Mat? elixirCollector,
        Mat? darkElixirCollector,
        Mat? storageFull,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(matcher);
        ArgumentNullException.ThrowIfNull(input);

        Stopwatch stopwatch = Stopwatch.StartNew();
        if (frame is null || frame.Empty())
        {
            LastMatches = Array.Empty<MatchResult>();
            return new CollectorResult(0, 0, 0, stopwatch.Elapsed);
        }

        var matches = new List<MatchResult>();
        var tapPoints = new List<Point2D>();
        int skippedDuplicate = 0;

        // Thứ tự cố định: vàng → elixir → dark elixir (thứ tự case trong Collect.au3:44-56).
        Mat?[] templates = { goldCollector, elixirCollector, darkElixirCollector };
        foreach (Mat? template in templates)
        {
            if (template is null || template.Empty())
            {
                continue;
            }

            ct.ThrowIfCancellationRequested();

            List<MatchResult> found = new(matcher.FindAll(frame, template, DefaultThreshold));
            matches.AddRange(found);

            var centers = new List<Point2D>(found.Count);
            foreach (MatchResult match in found)
            {
                centers.Add(new Point2D(match.X + match.Width / 2, match.Y + match.Height / 2));
            }

            List<Point2D> unique = EuclideanClusterer.Cluster(centers, DuplicateDistancePx);
            skippedDuplicate += centers.Count - unique.Count;
            tapPoints.AddRange(unique);
        }

        LastMatches = matches;

        if (tapPoints.Count == 0)
        {
            return new CollectorResult(0, 0, skippedDuplicate, stopwatch.Elapsed);
        }

        if (storageFull is not null && !storageFull.Empty())
        {
            // parity: nhánh kho đầy của Collect.au3:29-31/45-61 — bỏ qua, không tap.
            var detector = new StorageDetector(matcher);
            if (detector.HasFullStorage(frame, storageFull))
            {
                return new CollectorResult(0, tapPoints.Count, skippedDuplicate, stopwatch.Elapsed);
            }
        }

        int tapped = 0;
        foreach (Point2D point in tapPoints)
        {
            ct.ThrowIfCancellationRequested();

            // parity: Click($aCollectXY[$t][0], $aCollectXY[$t][1], 1, 120, "#0430") @ Collect.au3:66.
            input.Tap((int)point.X, (int)point.Y);
            tapped++;

            // parity: _Sleep($DELAYCOLLECT2) sau mỗi click @ Collect.au3:67.
            await _delay.DelayAsync(TapDelayMeanMs, TapDelayStdMs, TapDelayMinMs, TapDelayMaxMs, ct)
                .ConfigureAwait(false);
        }

        // parity: _Sleep($DELAYCOLLECT3) sau cả lượt @ Collect.au3:72.
        await _delay.DelayAsync(PostCollectDelayMs, 0, PostCollectDelayMs, PostCollectDelayMs, ct)
            .ConfigureAwait(false);

        return new CollectorResult(tapped, 0, skippedDuplicate, stopwatch.Elapsed);
    }
}
