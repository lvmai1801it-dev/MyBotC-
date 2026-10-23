using System;
using System.Threading;
using System.Threading.Tasks;
using MyBot.Core.Abstractions;
using OpenCvSharp;

namespace MyBot.Game.CoC.Village;

/// <summary>Đóng bảng thông tin công trình đang mở (port <c>ClearScreen</c> của AutoIt).</summary>
public interface IMainScreenCleaner
{
    /// <summary>
    /// Nếu màn hình đang bị làm mờ hoặc có bảng tên công trình ở đáy thì chạm ra vùng
    /// trống để đóng bảng, rồi chờ 1500 ms.
    /// </summary>
    /// <param name="capture">Hàm chụp khung hình mới.</param>
    /// <param name="cancellationToken">Token huỷ.</param>
    /// <returns>True nếu đã gửi thao tác đóng bảng.</returns>
    Task<bool> ClearScreenAsync(Func<Mat> capture, CancellationToken cancellationToken = default);
}

/// <summary>
/// Port <c>ClearScreen</c>/<c>ClickAway</c>/<c>IsMainGrayed</c>: kiểm tra bảng thông tin
/// ở <c>(242, 535)</c> và 3 bộ màu pixel "bị làm mờ", sau đó chạm ngẫu nhiên vào một
/// trong hai vùng trống của màn hình.
/// </summary>
public sealed class MainScreenCleaner : IMainScreenCleaner
{
    /// <summary>Vùng chạm trái của <c>ClickAway</c> (x1, y1, x2, y2).</summary>
    public static readonly Rect ClickAwayLeft = Rect.FromLTRB(235, 10, 245, 30);

    /// <summary>Vùng chạm phải của <c>ClickAway</c> (x1, y1, x2, y2).</summary>
    public static readonly Rect ClickAwayRight = Rect.FromLTRB(570, 5, 620, 15);

    private const int ClearDelayMs = 1500;
    private const int ColorTolerance = 15;

    // Ba bộ màu RGB mà AutoIt dùng để nhận biết làng đang bị làm mờ.
    private static readonly (int Primary, int Second, int Third)[] GrayColorSets =
    {
        (0x576F7B, 0x3D5F72, 0x7B7B77),
        (0x34434A, 0x253944, 0x4A4A47),
        (0x688593, 0x497188, 0x93938E),
    };

    private readonly IBuildingInfoReader _buildingInfo;
    private readonly IInputBackend _input;
    private readonly ITimeProvider _time;
    private readonly IBotLogger? _logger;
    private readonly Random _random;

    /// <summary>Khởi tạo bộ đóng bảng thông tin.</summary>
    /// <param name="buildingInfo">Bộ đọc bảng thông tin (dùng để biết bảng có mở không).</param>
    /// <param name="input">Backend input để chạm ra vùng trống.</param>
    /// <param name="time">Nguồn thời gian cho thời gian chờ 1500 ms.</param>
    /// <param name="logger">Logger tuỳ chọn.</param>
    /// <param name="random">Nguồn ngẫu nhiên; null = <see cref="Random.Shared"/>.</param>
    public MainScreenCleaner(
        IBuildingInfoReader buildingInfo,
        IInputBackend input,
        ITimeProvider time,
        IBotLogger? logger = null,
        Random? random = null)
    {
        _buildingInfo = buildingInfo ?? throw new ArgumentNullException(nameof(buildingInfo));
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _logger = logger;
        _random = random ?? Random.Shared;
    }

    /// <inheritdoc />
    public async Task<bool> ClearScreenAsync(
        Func<Mat> capture,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capture);

        using Mat frame = capture() ?? new Mat();
        string buildingText = _buildingInfo.ReadRawText(
            frame,
            BuildingInfoX,
            BuildingInfoReader.DefaultY);
        bool grayed = !frame.Empty() && IsMainGrayed(frame);

        if (buildingText.Length == 0 && !grayed)
        {
            return false;
        }

        _logger?.Info("Clearing Screen");
        (int x, int y) = RandomClickAwayPoint();
        _input.Tap(x, y);
        await _time.DelayAsync(ClearDelayMs, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Port <c>IsMainGrayed</c>: dò 3 bộ màu mốc trong dải <c>(370, 7)-(390, 9)</c>.
    /// </summary>
    /// <param name="frame">Khung hình BGR.</param>
    /// <returns>True nếu màn hình đang bị làm mờ.</returns>
    public bool IsMainGrayed(Mat frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (frame.Empty() || frame.Width <= 399 || frame.Height <= 9)
        {
            return false;
        }

        for (int y = 7; y <= 9; y++)
        {
            for (int x = 370; x <= 390; x++)
            {
                foreach ((int primary, int second, int third) in GrayColorSets)
                {
                    if (MatchesRgb(frame, x, y, primary)
                        && MatchesRgb(frame, x + 4, y, second)
                        && MatchesRgb(frame, x + 9, y, third))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>Chọn điểm ngẫu nhiên trong vùng <c>ClickAway</c> trái hoặc phải.</summary>
    private (int X, int Y) RandomClickAwayPoint()
    {
        Rect region = _random.Next(0, 2) > 0 ? ClickAwayLeft : ClickAwayRight;
        int x = _random.Next(region.Left, region.Right + 1);
        int y = _random.Next(region.Top, region.Bottom + 1);
        return (x, y);
    }

    private static bool MatchesRgb(Mat frame, int x, int y, int rgb)
    {
        Vec3b pixel = frame.At<Vec3b>(y, x);
        int red = (rgb >> 16) & 0xFF;
        int green = (rgb >> 8) & 0xFF;
        int blue = rgb & 0xFF;

        return System.Math.Abs(pixel.Item0 - blue) <= ColorTolerance
            && System.Math.Abs(pixel.Item1 - green) <= ColorTolerance
            && System.Math.Abs(pixel.Item2 - red) <= ColorTolerance;
    }

    // Vùng OCR của bảng thông tin, tách hằng số để dễ đọc.
    private const int BuildingInfoX = 242;
}
