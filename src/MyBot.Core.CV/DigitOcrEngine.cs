using System.Text;
using MyBot.Core.Abstractions;
using OpenCvSharp;

namespace MyBot.Core.CV;

/// <summary>
/// OCR engine for the monospaced Supercell number font. Instead of contour detection
/// (which merges glyphs because of the game's glow), it slices the region into fixed-width
/// cells and matches each cell against 10 digit templates.
/// </summary>
public class DigitOcrEngine : IOcrEngine, IDisposable
{
    /// <summary>Chuỗi số dài tối đa (kể cả ô trống).</summary>
    private const int MaxCharacters = 9;

    /// <summary>Ngưỡng rất cao: chỉ giữ lõi trắng của chữ, bỏ quầng sáng vàng.</summary>
    private const double WhiteCoreThreshold = 220;

    /// <summary>Điểm khớp tối thiểu để một ô được coi là chữ số.</summary>
    private const double MatchThreshold = 0.5;

    private readonly Dictionary<char, Mat> _templates = new();
    private string _templateDirectory = string.Empty;
    private bool _disposed;

    /// <summary>Chuỗi thô của lần đọc gần nhất, dùng cho debug.</summary>
    public string LastRawText { get; private set; } = string.Empty;

    /// <summary>
    /// Nạp 10 ảnh mẫu số từ thư mục cho trước. Ảnh mẫu được xử lý giống hệt vùng cần đọc
    /// (grayscale → ngưỡng lõi trắng 220 → dilate) để so khớp đúng chiều màu.
    /// </summary>
    /// <param name="templatePath">Thư mục chứa <c>0.png</c>..<c>9.png</c>.</param>
    /// <returns>True nếu nạp đủ 10 ảnh mẫu.</returns>
    public bool Initialize(string templatePath)
    {
        if (string.IsNullOrWhiteSpace(templatePath))
        {
            return false;
        }

        _templateDirectory = Path.GetFullPath(templatePath);
        return LoadTemplates();
    }

    /// <summary>Reads all digits in the region, left to right, using fixed-width cells.</summary>
    /// <param name="frame">Region containing the number (monospaced font).</param>
    /// <returns>The recognized digit string, or an empty string.</returns>
    public string Recognize(Mat frame)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (frame is null || frame.Empty())
        {
            LastRawText = string.Empty;
            return string.Empty;
        }

        // Thử nạp lại nếu lúc khởi động thư mục mẫu chưa có ảnh.
        if (_templates.Count == 0)
        {
            LoadTemplates();
        }

        if (_templates.Count == 0)
        {
            LastRawText = string.Empty;
            return string.Empty;
        }

        using Mat gray = new();
        Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);

        // Chỉ giữ lõi trắng tinh; quầng sáng vàng và nền bị loại.
        using Mat binary = new();
        Cv2.Threshold(gray, binary, WhiteCoreThreshold, 255, ThresholdTypes.Binary);

        // Làm dày nét chữ một chút trước khi cắt ô.
        using Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(2, 2));
        Cv2.Dilate(binary, binary, kernel, iterations: 1);

        SaveDebugBinary(binary);

        // Font game là monospaced: chia vùng đọc thành MaxCharacters ô đều nhau.
        int charWidth = binary.Width / MaxCharacters;
        if (charWidth <= 0)
        {
            LastRawText = string.Empty;
            return string.Empty;
        }

        var result = new StringBuilder();
        for (int i = 0; i < MaxCharacters; i++)
        {
            int startX = i * charWidth;
            if (startX + charWidth > binary.Width)
            {
                break;
            }

            var charRect = new Rect(startX, 0, charWidth, binary.Height);
            using Mat charMat = new(binary, charRect);

            (char digit, double score) = MatchDigit(charMat);
            if (score > MatchThreshold)
            {
                result.Append(digit);
            }

            // score <= MatchThreshold: ô trống/dấu cách hoặc nhiễu → bỏ qua.
        }

        LastRawText = result.ToString();
        return LastRawText;
    }

    /// <summary>Resizes every template to the cell size and returns the best-scoring digit.</summary>
    private (char Digit, double Score) MatchDigit(Mat charMat)
    {
        char bestDigit = '?';
        double bestScore = double.NegativeInfinity;

        foreach ((char digit, Mat template) in _templates)
        {
            using Mat resizedTemplate = new();
            Cv2.Resize(template, resizedTemplate, charMat.Size(), 0, 0, InterpolationFlags.Cubic);

            using Mat scores = new();
            Cv2.MatchTemplate(charMat, resizedTemplate, scores, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(scores, out _, out double maxScore, out _, out _);

            if (maxScore > bestScore)
            {
                bestScore = maxScore;
                bestDigit = digit;
            }
        }

        return (bestDigit, bestScore);
    }

    /// <summary>Loads and normalizes all 10 digit templates at their original size.</summary>
    private bool LoadTemplates()
    {
        ClearTemplates();

        if (_templateDirectory.Length == 0 || !Directory.Exists(_templateDirectory))
        {
            return false;
        }

        using Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(2, 2));

        for (int digit = 0; digit <= 9; digit++)
        {
            string file = Path.Combine(_templateDirectory, $"{digit}.png");
            if (!File.Exists(file))
            {
                ClearTemplates();
                return false;
            }

            using Mat raw = Cv2.ImRead(file, ImreadModes.Grayscale);
            if (raw.Empty())
            {
                ClearTemplates();
                return false;
            }

            using Mat whiteCore = new();
            Cv2.Threshold(raw, whiteCore, WhiteCoreThreshold, 255, ThresholdTypes.Binary);

            var normalized = new Mat();
            Cv2.Dilate(whiteCore, normalized, kernel, iterations: 1);
            _templates[(char)('0' + digit)] = normalized;
        }

        return true;
    }

    /// <summary>Saves the thresholded region for visual debugging next to the executable.</summary>
    private static void SaveDebugBinary(Mat binary)
    {
        string debugPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gold_crop_binary.png");
        Cv2.ImWrite(debugPath, binary);
    }

    private void ClearTemplates()
    {
        foreach (Mat template in _templates.Values)
        {
            template.Dispose();
        }

        _templates.Clear();
    }

    /// <summary>Releases all loaded template images.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ClearTemplates();
        _disposed = true;
    }
}
