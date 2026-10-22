using System;
using System.IO;
using System.Text.RegularExpressions;
using MyBot.Core.Abstractions;
using OpenCvSharp;
using Tesseract;

namespace MyBot.Core.CV;

/// <summary>
/// OCR engine backed by Tesseract, tuned for reading single-line numeric HUD values.
/// </summary>
public class TesseractOcrEngine : IOcrEngine, IDisposable
{
    private readonly string _tessDataPath;
    private TesseractEngine? _engine;
    private bool _disposed;

    /// <summary>Creates an engine that loads <c>eng.traineddata</c> from the app's tessdata folder.</summary>
    public TesseractOcrEngine()
        : this(ResolveTessDataPath())
    {
    }

    /// <summary>Creates an engine that loads its language data from the given folder.</summary>
    /// <param name="tessDataPath">Folder containing <c>eng.traineddata</c>.</param>
    public TesseractOcrEngine(string tessDataPath)
    {
        if (string.IsNullOrWhiteSpace(tessDataPath))
        {
            throw new ArgumentException("Tessdata path must not be empty.", nameof(tessDataPath));
        }

        // FIX-INT2: Tesseract yêu cầu đường dẫn tuyệt đối; đường dẫn tương đối phụ thuộc
        // vào thư mục làm việc hiện tại nên dễ trỏ sai chỗ.
        _tessDataPath = Path.GetFullPath(tessDataPath);
    }

    /// <summary>
    /// Tìm thư mục <c>tessdata</c> chứa <c>eng.traineddata</c>, ưu tiên cạnh file exe.
    /// Nếu chưa được copy ra output (ví dụ chạy Debug sau khi build Release), tự động
    /// dò ngược lên thư mục project để vẫn tìm thấy dữ liệu ngôn ngữ.
    /// </summary>
    private static string ResolveTessDataPath()
    {
        const string languageDataFile = "eng.traineddata";
        string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

        string outputPath = Path.Combine(baseDirectory, "tessdata");
        if (File.Exists(Path.Combine(outputPath, languageDataFile)))
        {
            return outputPath;
        }

        DirectoryInfo? directory = new(baseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "tessdata");
            if (File.Exists(Path.Combine(candidate, languageDataFile)))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return outputPath;
    }

    /// <summary>Validates that the given tessdata folder can be used.</summary>
    /// <param name="modelPath">Folder containing <c>eng.traineddata</c>.</param>
    /// <returns>True if the engine was initialized successfully.</returns>
    public bool Initialize(string modelPath)
    {
        try
        {
            EnsureEngine(modelPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Tiền xử lý vùng ảnh cho OCR font Supercell: Gray → phóng to 4x → Gaussian blur
    /// → ngưỡng cố định 200 (chỉ giữ lõi trắng của chữ) → đảo màu để có chữ đen nền trắng.
    /// </summary>
    /// <param name="frame">Vùng ảnh BGR cần xử lý.</param>
    /// <returns>Ảnh nhị phân mới; người gọi chịu trách nhiệm dispose. Trả Mat rỗng nếu input rỗng.</returns>
    public Mat PreprocessImage(Mat frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        if (frame.Empty())
        {
            return new Mat();
        }

        using Mat gray = new();
        Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);

        // Phóng to trước khi threshold để nét chữ dày lên, Tesseract dễ đọc hơn.
        using Mat resized = new();
        Cv2.Resize(gray, resized, new Size(0, 0), 4.0, 4.0, InterpolationFlags.Cubic);

        using Mat blurred = new();
        Cv2.GaussianBlur(resized, blurred, new Size(3, 3), 0);

        // Ngưỡng cố định: chỉ pixel rất sáng (lõi trắng) thành trắng;
        // viền đen và nền vàng bị loại thành đen.
        var binary = new Mat();
        Cv2.Threshold(blurred, binary, 200, 255, ThresholdTypes.Binary);

        // Đảo màu: lõi chữ trắng → đen, nền → trắng (định dạng Tesseract ưu tiên).
        Cv2.BitwiseNot(binary, binary);
        return binary;
    }

    /// <summary>Reads the digits from a single line of text in the given image region.</summary>
    /// <param name="region">Image region containing the number.</param>
    /// <returns>The digits found in the region, or an empty string.</returns>
    public string Recognize(Mat region)
    {
        if (region is null || region.Empty())
        {
            return string.Empty;
        }

        EnsureEngine(_tessDataPath);

        using Mat binary = PreprocessImage(region);
        if (binary.Empty())
        {
            return string.Empty;
        }

        Cv2.ImEncode(".png", binary, out byte[] pngBytes);

        using Pix pix = Pix.LoadFromMemory(pngBytes);
        using Page page = _engine!.Process(pix, PageSegMode.SingleLine);

        string text = Regex.Replace(page.GetText(), @"[^\d]", string.Empty);

        // FIX-OCR-2: giữ lại ảnh nhị phân khi OCR thất bại để user kiểm tra
        // vùng cắt/tiền xử lý có đúng không.
        if (text.Length == 0)
        {
            string debugPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gold_crop_binary.png");
            Cv2.ImWrite(debugPath, binary);
        }

        return text;
    }

    private void EnsureEngine(string tessDataPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_engine is not null)
        {
            return;
        }

        // FIX-INT2: báo lỗi rõ ràng thay vì để Tesseract ném thông báo khó hiểu
        // khi thư mục tessdata bị thiếu hoặc đặt sai vị trí.
        string languageDataFile = Path.Combine(tessDataPath, "eng.traineddata");
        if (!File.Exists(languageDataFile))
        {
            throw new FileNotFoundException(
                $"Không tìm thấy dữ liệu ngôn ngữ Tesseract tại '{languageDataFile}'. " +
                "Hãy copy thư mục tessdata (chứa eng.traineddata) vào cạnh file exe.",
                languageDataFile);
        }

        _engine = new TesseractEngine(tessDataPath, "eng", EngineMode.Default);
    }

    /// <summary>Releases the native Tesseract engine.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _engine?.Dispose();
        _engine = null;
        _disposed = true;
    }
}
