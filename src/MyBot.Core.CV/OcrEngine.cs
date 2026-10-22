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

        using Mat gray = new();
        Cv2.CvtColor(region, gray, ColorConversionCodes.BGR2GRAY);

        using Mat thresholded = new();
        Cv2.Threshold(gray, thresholded, 150, 255, ThresholdTypes.Binary);

        Cv2.ImEncode(".png", thresholded, out byte[] pngBytes);

        using Pix pix = Pix.LoadFromMemory(pngBytes);
        using Page page = _engine!.Process(pix, PageSegMode.SingleLine);

        return Regex.Replace(page.GetText(), @"[^\d]", string.Empty);
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
