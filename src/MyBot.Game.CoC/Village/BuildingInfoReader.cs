using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MyBot.Core.Abstractions;
using OpenCvSharp;

namespace MyBot.Game.CoC.Village;

/// <summary>Kết quả đọc bảng thông tin công trình ở đáy màn hình.</summary>
public sealed class BuildingInfoResult
{
    /// <summary>Tên công trình OCR đọc được (ví dụ <c>Town Hall</c>).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Cấp công trình; 0 nếu không đọc được.</summary>
    public int Level { get; init; }

    /// <summary>Chuỗi cấp sau khi chuẩn hoá (<c>Broken</c> cho công trình chưa xây lại).</summary>
    public string LevelText { get; init; } = string.Empty;

    /// <summary>True khi công trình chưa được xây lại (<c>Broken</c>).</summary>
    public bool IsBroken { get; init; }

    /// <summary>True khi đọc được ít nhất tên hoặc cấp.</summary>
    public bool Found => !string.IsNullOrEmpty(Name) || Level > 0;
}

/// <summary>Đọc tên/cấp công trình bằng OCR (port <c>BuildingInfo</c> + <c>getNameBuilding</c>).</summary>
public interface IBuildingInfoReader
{
    /// <summary>Đọc một lần bằng model <c>coc-build</c>, có fallback sang <c>coc-build2</c>.</summary>
    /// <param name="frame">Khung hình BGR.</param>
    /// <param name="x">Toạ độ X vùng chữ.</param>
    /// <param name="y">Toạ độ Y vùng chữ.</param>
    /// <returns>Chuỗi OCR đã chuẩn hoá; rỗng nếu không đọc được.</returns>
    string ReadRawText(Mat frame, int x, int y);

    /// <summary>Phân tích chuỗi OCR thành tên và cấp.</summary>
    /// <param name="text">Chuỗi OCR (kết quả của <see cref="ReadRawText"/>).</param>
    /// <returns>Kết quả phân tích.</returns>
    BuildingInfoResult Parse(string text);

    /// <summary>
    /// Đọc bảng thông tin, thử lại một lần sau 2500 ms nếu lần đầu rỗng
    /// (AutoIt: <c>BuildingInfo</c>).
    /// </summary>
    /// <param name="capture">Hàm chụp khung hình mới.</param>
    /// <param name="x">Toạ độ X vùng chữ.</param>
    /// <param name="y">Toạ độ Y vùng chữ.</param>
    /// <param name="cancellationToken">Token huỷ.</param>
    /// <returns>Kết quả phân tích.</returns>
    Task<BuildingInfoResult> ReadAsync(
        Func<Mat> capture,
        int x,
        int y,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Port <c>BuildingInfo</c>/<c>getNameBuilding</c> của AutoIt: cắt vùng 420×27 px ở
/// đáy màn hình, đọc bằng model <c>coc-build</c>, fallback <c>coc-build2</c> ở dòng
/// trên khi thiếu cấp, chuẩn hoá lỗi OCR rồi tách tên khỏi <c>(Level N)</c>.
/// </summary>
public sealed class BuildingInfoReader : IBuildingInfoReader
{
    /// <summary>Toạ độ Y mặc định: <c>475 + $g_iBottomOffsetY</c>.</summary>
    public const int DefaultY = 475 + 60;

    /// <summary>Chiều rộng vùng OCR (AutoIt: 420).</summary>
    public const int Width = 420;

    /// <summary>Chiều cao vùng OCR (AutoIt: 27).</summary>
    public const int Height = 27;

    /// <summary>Thời gian chờ trước lần đọc lại. AutoIt: <c>$DELAYBUILDINGINFO1</c> = 2500 ms.</summary>
    public const int RetryDelayMs = 2500;

    private const string PrimaryModel = "coc-build";
    private const string AlternateModel = "coc-build2";

    private static readonly TimeSpan LevelRegexTimeout = TimeSpan.FromSeconds(1);

    private static readonly Regex CompleteLevelRegex = new(
        @"(?i)\(\s*Level\s+\d+\s*\)",
        RegexOptions.Compiled,
        LevelRegexTimeout);

    private static readonly Regex InvalidLevelRegex = new(
        @"(?i)\(\s*Level\s+([0-9\s]*ii)\s*\)",
        RegexOptions.Compiled,
        LevelRegexTimeout);

    private static readonly Regex WhitespaceRegex = new(
        @"\s+",
        RegexOptions.Compiled,
        LevelRegexTimeout);

    private static readonly string[] ObstacleNames =
    {
        "Broken", "Cart", "Tree", "Mush", "Trunk", "Bush", "Bark", "Gem", "Cake", "Groove",
    };

    private readonly IOcrEngine _ocr;
    private readonly ITimeProvider _time;
    private readonly IBotLogger? _logger;

    /// <summary>Khởi tạo bộ đọc bảng thông tin công trình.</summary>
    /// <param name="ocr">Bộ OCR dùng model <c>coc-build*</c>.</param>
    /// <param name="time">Nguồn thời gian cho lần đọc lại.</param>
    /// <param name="logger">Logger tuỳ chọn.</param>
    public BuildingInfoReader(IOcrEngine ocr, ITimeProvider time, IBotLogger? logger = null)
    {
        _ocr = ocr ?? throw new ArgumentNullException(nameof(ocr));
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _logger = logger;
    }

    /// <inheritdoc />
    public string ReadRawText(Mat frame, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (frame.Empty())
        {
            return string.Empty;
        }

        string result = Recognize(frame, x, y, PrimaryModel);

        // AutoIt trả về ngay sau khi sửa hai chuỗi OCR đặc biệt, không đọc fallback.
        if (result.Contains("O T T O", StringComparison.Ordinal))
        {
            return result.Replace("O T T O", "O.T.T.O", StringComparison.Ordinal);
        }

        if (result.Contains("B O B", StringComparison.Ordinal))
        {
            return result.Replace("B O B", "B.O.B", StringComparison.Ordinal);
        }

        if (result.Contains("Helper", StringComparison.OrdinalIgnoreCase))
        {
            return result;
        }

        foreach (string obstacle in ObstacleNames)
        {
            if (result.Contains(obstacle, StringComparison.OrdinalIgnoreCase))
            {
                return result;
            }
        }

        // Chỉ đọc lại dòng trên khi thiếu cụm "(Level N)" hoàn chỉnh.
        if (!CompleteLevelRegex.IsMatch(result))
        {
            string alternate = Recognize(frame, x, y - Height, AlternateModel);
            if (result.Length == 0 || CompleteLevelRegex.IsMatch(alternate))
            {
                result = alternate;
            }
        }

        // Glyph "8" hiện tại đôi khi bị đọc thành "ii" trong trường Level.
        Match invalid = InvalidLevelRegex.Match(result);
        if (invalid.Success)
        {
            string normalized = WhitespaceRegex.Replace(invalid.Groups[1].Value, string.Empty)
                .Replace("ii", "8", StringComparison.OrdinalIgnoreCase);
            result = InvalidLevelRegex.Replace(result, "(Level " + normalized + ")");
        }

        return result;
    }

    /// <inheritdoc />
    public BuildingInfoResult Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new BuildingInfoResult();
        }

        // AutoIt gắn cấp giả cho vật phẩm trang trí để phân biệt với công trình.
        string decorated = AppendFakeLevel(text);

        // AutoIt chỉ tách khi có đúng một dấu "(".
        int open = decorated.IndexOf('(');
        if (open < 0 || decorated.IndexOf('(', open + 1) >= 0)
        {
            return new BuildingInfoResult();
        }

        string name = WhitespaceRegex.Replace(decorated[..open], " ").Trim();
        string levelPart = decorated[(open + 1)..];

        int close = levelPart.IndexOf(')');
        if (close >= 0)
        {
            levelPart = levelPart[..close];
        }

        if (levelPart.Contains("Broken", StringComparison.OrdinalIgnoreCase))
        {
            levelPart += " 200";
        }

        int level = 0;
        string[] tokens = levelPart.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 2
            && int.TryParse(tokens[1], System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out int parsed))
        {
            level = parsed;
        }

        // Cấp > 95 là cấp giả: 200 = "Broken", các giá trị khác bị bỏ.
        if (level > 95)
        {
            if (level == 200)
            {
                return new BuildingInfoResult
                {
                    Name = name,
                    LevelText = "Broken",
                    IsBroken = true,
                };
            }

            return new BuildingInfoResult { Name = name };
        }

        return new BuildingInfoResult
        {
            Name = name,
            Level = level,
            LevelText = level.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
    }

    /// <inheritdoc />
    public async Task<BuildingInfoResult> ReadAsync(
        Func<Mat> capture,
        int x,
        int y,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capture);

        string text;
        using (Mat frame = capture() ?? new Mat())
        {
            text = ReadRawText(frame, x, y);
        }

        if (text.Length == 0)
        {
            await _time.DelayAsync(RetryDelayMs, cancellationToken).ConfigureAwait(false);
            using Mat retryFrame = capture() ?? new Mat();
            text = ReadRawText(retryFrame, x, y);
        }

        return Parse(text);
    }

    private string Recognize(Mat frame, int x, int y, string model)
    {
        if (x < 0 || y < 0 || x + Width > frame.Width || y + Height > frame.Height)
        {
            return string.Empty;
        }

        try
        {
            using var region = new Mat(frame, new Rect(x, y, Width, Height));
            return _ocr.Recognize(region, model) ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger?.Warning($"OCR bảng thông tin lỗi tại {x},{y}: {ex.Message}");
            return string.Empty;
        }
    }

    private static string AppendFakeLevel(string text)
    {
        if (text.Contains("Helper", StringComparison.OrdinalIgnoreCase))
        {
            return text + " (FakeLevel 105)";
        }

        if (text.Contains("B.O.B", StringComparison.Ordinal))
        {
            return text + " (FakeLevel 104)";
        }

        if (text.Contains("Cake", StringComparison.OrdinalIgnoreCase))
        {
            return text + " (FakeLevel 103)";
        }

        if (text.Contains("Cart", StringComparison.OrdinalIgnoreCase))
        {
            return text + " (FakeLevel 102)";
        }

        if (text.Contains("Tree", StringComparison.OrdinalIgnoreCase))
        {
            return text + " (FakeLevel 101)";
        }

        if (text.Contains("Mush", StringComparison.OrdinalIgnoreCase))
        {
            return text + " (FakeLevel 100)";
        }

        if (text.Contains("Trunk", StringComparison.OrdinalIgnoreCase))
        {
            return text + " (FakeLevel 99)";
        }

        if (text.Contains("Bush", StringComparison.OrdinalIgnoreCase))
        {
            return text + " (FakeLevel 98)";
        }

        if (text.Contains("Bark", StringComparison.OrdinalIgnoreCase))
        {
            return text + " (FakeLevel 97)";
        }

        if (text.Contains("Gem", StringComparison.OrdinalIgnoreCase)
            && !text.Contains("Gem Mine", StringComparison.OrdinalIgnoreCase))
        {
            return text + " (FakeLevel 96)";
        }

        return text;
    }
}
