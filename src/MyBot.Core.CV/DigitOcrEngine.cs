using System.Text;
using MyBot.Core.Abstractions;
using OpenCvSharp;

namespace MyBot.Core.CV;

/// <summary>
/// OCR engine for game digits: splits the region into digit contours and matches each one
/// against 10 grayscale templates (<c>0.png</c>..<c>9.png</c>) using OpenCV template matching.
/// Replaces Tesseract, which does not handle the Supercell font reliably.
/// </summary>
public class DigitOcrEngine : IOcrEngine, IDisposable
{
    private const int DigitWidth = 24;
    private const int DigitHeight = 32;

    /// <summary>Ngưỡng nhị phân giữ lại lõi chữ trắng (có thể chỉnh 150-200 nếu cần).</summary>
    private const double BinaryThreshold = 150;

    private const int MinDigitHeight = 15;
    private const int MaxDigitHeight = 35;
    private const int MinDigitWidth = 2;

    private readonly Dictionary<char, Mat> _templates = new();
    private string _templateDirectory = string.Empty;
    private bool _disposed;

    /// <summary>Chuỗi thô của lần đọc gần nhất, dùng cho debug.</summary>
    public string LastRawText { get; private set; } = string.Empty;

    /// <summary>
    /// Nạp 10 ảnh mẫu số từ thư mục cho trước. Ảnh mẫu được xử lý giống hệt vùng cần đọc
    /// (grayscale → nhị phân đảo ngược → resize chuẩn) để so khớp chính xác.
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

    /// <summary>Reads all digits in the region, left to right.</summary>
    /// <param name="frame">Region containing the number.</param>
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

        // BinaryInv: chữ trắng (> ngưỡng) → đen (0), nền vàng/đen → trắng (255).
        using Mat binary = new();
        Cv2.Threshold(gray, binary, BinaryThreshold, 255, ThresholdTypes.BinaryInv);

        SaveDebugBinary(binary);

        Cv2.FindContours(
            binary,
            out Point[][] contours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        var digitRects = new List<Rect>();
        foreach (Point[] contour in contours)
        {
            Rect rect = Cv2.BoundingRect(contour);
            if (rect.Height < MinDigitHeight || rect.Height > MaxDigitHeight || rect.Width < MinDigitWidth)
            {
                // Bỏ nhiễu nhỏ và các khối lớn (đồng xu, khung).
                continue;
            }

            digitRects.Add(rect);
        }

        // Đọc theo thứ tự trái sang phải.
        digitRects.Sort((left, right) => left.X.CompareTo(right.X));

        var result = new StringBuilder();
        foreach (Rect rect in digitRects)
        {
            using Mat roi = new(binary, rect);
            using Mat resized = new();
            Cv2.Resize(roi, resized, new Size(DigitWidth, DigitHeight), 0, 0, InterpolationFlags.Cubic);

            result.Append(MatchDigit(resized));
        }

        LastRawText = result.ToString();
        return LastRawText;
    }

    /// <summary>Resizes and matches one digit against all templates, returning the best label.</summary>
    private char MatchDigit(Mat resized)
    {
        char bestDigit = '?';
        double bestScore = double.NegativeInfinity;

        foreach ((char digit, Mat template) in _templates)
        {
            using Mat scores = new();
            Cv2.MatchTemplate(resized, template, scores, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(scores, out _, out double maxScore, out _, out _);

            if (maxScore > bestScore)
            {
                bestScore = maxScore;
                bestDigit = digit;
            }
        }

        return bestDigit;
    }

    /// <summary>Loads and normalizes all 10 digit templates.</summary>
    private bool LoadTemplates()
    {
        ClearTemplates();

        if (_templateDirectory.Length == 0 || !Directory.Exists(_templateDirectory))
        {
            return false;
        }

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

            using Mat binary = new();
            Cv2.Threshold(raw, binary, BinaryThreshold, 255, ThresholdTypes.BinaryInv);

            var normalized = new Mat();
            Cv2.Resize(binary, normalized, new Size(DigitWidth, DigitHeight), 0, 0, InterpolationFlags.Cubic);
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
