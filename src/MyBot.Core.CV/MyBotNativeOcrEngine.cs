using MyBot.Core.Abstractions;
using MyBot.Core.CV.Native;
using OpenCvSharp;

namespace MyBot.Core.CV;

/// <summary>
/// OCR engine dùng chính <c>MyBot.run.dll</c> của bản AutoIt, thay vì so khớp template số.
/// Kết quả đọc vì vậy giống hệt bản gốc vì cùng một engine và cùng bộ model
/// <c>listSymbols_*.xml</c> nằm cạnh DLL.
/// </summary>
/// <remarks>
/// DLL là module 32-bit, nên tiến trình gọi phải chạy x86.
/// </remarks>
public sealed class MyBotNativeOcrEngine : IOcrEngine, IDisposable
{
    /// <summary>Model mặc định cho thanh tài nguyên ở làng chính.</summary>
    private const string DefaultModel = "coc-ms";

    private const int AsyncPollIntervalMs = 100;
    private const int AsyncTimeoutMs = 5000;

    private readonly MyBotRunNative _native = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    /// <summary>Gets or sets tên model OCR, ví dụ <c>coc-ms</c> hoặc <c>coc-v-g</c>.</summary>
    public string Model { get; set; } = DefaultModel;

    /// <summary>Gets or sets a value indicating whether bật ghi ảnh debug của engine gốc.</summary>
    public bool Debug { get; set; }

    /// <summary>Gets a value indicating whether DLL đã nạp thành công.</summary>
    public bool IsInitialized => _native.IsLoaded;

    /// <summary>Nạp <c>MyBot.run.dll</c> từ thư mục chứa DLL và các file model.</summary>
    /// <param name="modelPath">Thư mục chứa <c>MyBot.run.dll</c> (thường là <c>lib</c> cạnh file exe).</param>
    /// <returns><see langword="true"/> nếu nạp được DLL và đủ export OCR.</returns>
    public bool Initialize(string modelPath)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(modelPath))
        {
            return false;
        }

        if (!_native.Load(Path.GetFullPath(modelPath)))
        {
            return false;
        }

        return _native.IsLoaded;
    }

    /// <summary>Đọc chữ trong vùng ảnh bằng <see cref="Model"/> hiện tại.</summary>
    /// <param name="region">Vùng ảnh cần đọc (BGR, grayscale hoặc BGRA).</param>
    /// <returns>Chuỗi đọc được, hoặc chuỗi rỗng.</returns>
    public string Recognize(Mat region)
    {
        return Recognize(region, Model);
    }

    /// <summary>Đọc chữ trong vùng ảnh bằng model chỉ định.</summary>
    /// <param name="region">Vùng ảnh cần đọc (BGR, grayscale hoặc BGRA).</param>
    /// <param name="model">Tên model OCR, ví dụ <c>coc-v-g</c>.</param>
    /// <returns>Chuỗi đọc được, hoặc chuỗi rỗng.</returns>
    public string Recognize(Mat region, string model)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(region);

        if (region.Empty())
        {
            return string.Empty;
        }

        if (!_native.IsLoaded)
        {
            throw new InvalidOperationException("MyBot.run.dll chưa được nạp. Hãy gọi Initialize trước.");
        }

        _gate.Wait();
        try
        {
            using NativeBitmap image = NativeBitmap.FromMat(region);
            return _native.Ocr(image.Handle, model, Debug ? 1 : 0);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Đọc chữ bằng ImgLoc bundle (<c>DoOCR</c>). Hàm này bất đồng bộ vì engine gốc
    /// trả về <c>&lt;GetAsyncResult&gt;</c> cho tới khi có kết quả.
    /// </summary>
    /// <param name="region">Vùng ảnh cần đọc.</param>
    /// <param name="bundle">Tên bundle, ví dụ <c>chinese-bundle</c>.</param>
    /// <param name="timeProvider">Nguồn thời gian dùng để chờ kết quả.</param>
    /// <param name="timeoutMs">Thời gian chờ tối đa, tính bằng mili giây.</param>
    /// <param name="cancellationToken">Token hủy.</param>
    /// <returns>Chuỗi đọc được, hoặc chuỗi rỗng nếu quá thời gian chờ.</returns>
    public async Task<string> ReadImgLocAsync(
        Mat region,
        string bundle,
        ITimeProvider timeProvider,
        int timeoutMs = AsyncTimeoutMs,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (region.Empty())
        {
            return string.Empty;
        }

        if (!_native.IsLoaded)
        {
            throw new InvalidOperationException("MyBot.run.dll chưa được nạp. Hãy gọi Initialize trước.");
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using NativeBitmap image = NativeBitmap.FromMat(region);
            string result = _native.DoOcr(image.Handle, bundle);
            DateTime deadline = timeProvider.UtcNow.AddMilliseconds(timeoutMs);

            while (result == MyBotRunNative.AsyncResultMarker && timeProvider.UtcNow < deadline)
            {
                await timeProvider.DelayAsync(AsyncPollIntervalMs, cancellationToken).ConfigureAwait(false);
                result = _native.GetAsyncResult();
            }

            return result == MyBotRunNative.AsyncResultMarker ? string.Empty : result;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Giải phóng DLL và semaphore.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _native.Dispose();
        _gate.Dispose();
    }
}
