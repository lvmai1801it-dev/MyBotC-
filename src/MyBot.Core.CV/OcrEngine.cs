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
        : this(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata"))
    {
    }

    /// <summary>Creates an engine that loads its language data from the given folder.</summary>
    /// <param name="tessDataPath">Folder containing <c>eng.traineddata</c>.</param>
    public TesseractOcrEngine(string tessDataPath)
    {
        _tessDataPath = tessDataPath ?? throw new ArgumentNullException(nameof(tessDataPath));
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
        _engine ??= new TesseractEngine(tessDataPath, "eng", EngineMode.Default);
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
