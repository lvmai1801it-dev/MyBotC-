using System;
using System.Threading;
using System.Threading.Tasks;
using MyBot.Core.Abstractions;
using OpenCvSharp;

namespace MyBot.Game.CoC.Village;

/// <summary>Kết quả bước nhận diện công trình lần đầu (port <c>BotDetectFirstTime</c>).</summary>
public sealed class BotFirstTimeResult
{
    /// <summary>True khi đã xác định được Town Hall.</summary>
    public bool Success { get; init; }

    /// <summary>Cấp Town Hall.</summary>
    public int Level { get; init; }

    /// <summary>Toạ độ X Town Hall trong hệ tham chiếu của làng.</summary>
    public int VillageX { get; init; }

    /// <summary>Toạ độ Y Town Hall trong hệ tham chiếu của làng.</summary>
    public int VillageY { get; init; }

    /// <summary>Tên template Town Hall khớp được.</summary>
    public string? Template { get; init; }

    /// <summary>Mô tả kết quả để ghi log.</summary>
    public string Message { get; init; } = string.Empty;
}

/// <summary>Chạy bước nhận diện công trình lần đầu sau khi đã zoom và căn giữa làng.</summary>
public interface IBotFirstTimeDetector
{
    /// <summary>
    /// Đóng bảng đang mở, chờ camera ổn định rồi phát hiện Town Hall, đúng thứ tự
    /// <c>BotDetectFirstTime</c> của AutoIt.
    /// </summary>
    /// <param name="capture">Hàm chụp khung hình mới.</param>
    /// <param name="saved">Vị trí Town Hall đã lưu trong profile; null nếu chưa có.</param>
    /// <param name="cancellationToken">Token huỷ.</param>
    /// <returns>Kết quả nhận diện.</returns>
    Task<BotFirstTimeResult> DetectAsync(
        Func<Mat> capture,
        TownHallPosition? saved = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Port <c>BotDetectFirstTime</c>: <c>ClearScreen</c> → chờ 1000 ms →
/// <c>AutoDetectTownHall</c> → cảnh báo TH dưới 6 → chờ 2×1000 ms.
/// </summary>
public sealed class BotFirstTimeDetector : IBotFirstTimeDetector
{
    /// <summary>AutoIt: <c>$DELAYBOTDETECT1</c> = 1000 ms.</summary>
    public const int DetectDelayMs = 1000;

    private readonly ITownHallDetector _townHall;
    private readonly IMainScreenCleaner _cleaner;
    private readonly ITimeProvider _time;
    private readonly IBotLogger? _logger;

    /// <summary>Khởi tạo bộ nhận diện công trình lần đầu.</summary>
    /// <param name="townHall">Bộ phát hiện Town Hall.</param>
    /// <param name="cleaner">Bộ đóng bảng thông tin đang mở.</param>
    /// <param name="time">Nguồn thời gian.</param>
    /// <param name="logger">Logger tuỳ chọn.</param>
    public BotFirstTimeDetector(
        ITownHallDetector townHall,
        IMainScreenCleaner cleaner,
        ITimeProvider time,
        IBotLogger? logger = null)
    {
        _townHall = townHall ?? throw new ArgumentNullException(nameof(townHall));
        _cleaner = cleaner ?? throw new ArgumentNullException(nameof(cleaner));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<BotFirstTimeResult> DetectAsync(
        Func<Mat> capture,
        TownHallPosition? saved = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capture);

        await _cleaner.ClearScreenAsync(capture, cancellationToken).ConfigureAwait(false);
        await _time.DelayAsync(DetectDelayMs, cancellationToken).ConfigureAwait(false);

        _logger?.Info("Detecting Buildings");
        TownHallDetectionResult detection = await _townHall
            .AutoDetectAsync(capture, saved, cancellationToken)
            .ConfigureAwait(false);

        if (detection.Success && detection.Level > 1 && detection.Level < 6)
        {
            _logger?.Warning("Warning: Town Hall level below 6 is not recommended!");
            _logger?.Warning("Proceed with caution as errors may occur");
        }

        await _time.DelayAsync(DetectDelayMs, cancellationToken).ConfigureAwait(false);
        await _time.DelayAsync(DetectDelayMs, cancellationToken).ConfigureAwait(false);

        if (!detection.Success)
        {
            _logger?.Info("Town Hall: Unknow Position");
            _logger?.Info("Require manual detection");
        }

        return new BotFirstTimeResult
        {
            Success = detection.Success,
            Level = detection.Level,
            VillageX = detection.VillageX,
            VillageY = detection.VillageY,
            Template = detection.Template,
            Message = detection.Message,
        };
    }
}
