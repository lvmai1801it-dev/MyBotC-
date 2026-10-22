using System;
using System.Threading;
using System.Threading.Tasks;
using MyBot.Core.Abstractions;
using MyBot.Game.CoC.State.Zoom;
using OpenCvSharp;

namespace MyBot.Game.CoC.State;

/// <summary>
/// Đưa camera game về mức zoom chuẩn bằng các nấc con lăn chuột (zoom out),
/// tương đương <c>ZoomOut()</c> của bản AutoIt. Toạ độ OCR/template chỉ đúng
/// khi camera ở mức zoom chuẩn nên bước này chạy một lần ngay sau khi nhận diện làng.
/// </summary>
public sealed class CameraZoomer
{
    /// <summary>Độ lớn một nấc con lăn; âm = lăn xuống (zoom out).</summary>
    public const int WheelDelta = -120;

    /// <summary>Số nấc con lăn mỗi lần zoom out khi không xác nhận bằng frame.</summary>
    public const int Ticks = 5;

    /// <summary>Khoảng nghỉ giữa hai nấc con lăn (ms).</summary>
    public const int PauseMs = 150;

    /// <summary>Toạ độ X tâm khung hình 860x732.</summary>
    public const int CenterX = 430;

    /// <summary>Toạ độ Y tâm khung hình 860x732.</summary>
    public const int CenterY = 366;

    private readonly ITimeProvider _time;
    private readonly ZoomOptions _options;

    /// <summary>Khởi tạo với nguồn thời gian dùng cho khoảng nghỉ giữa các nấc lăn.</summary>
    /// <param name="time">Nguồn thời gian (có thể mock trong test).</param>
    /// <param name="options">Tham số zoom; null dùng giá trị mặc định.</param>
    public CameraZoomer(ITimeProvider time, ZoomOptions? options = null)
    {
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _options = options ?? new ZoomOptions();
    }

    /// <summary>
    /// Zoom out tại tâm khung hình 860x732.
    /// </summary>
    /// <param name="input">Backend input có hỗ trợ con lăn.</param>
    /// <param name="cancellationToken">Token huỷ.</param>
    /// <returns>True nếu đã gửi đủ số nấc con lăn.</returns>
    public Task<bool> ZoomOutAsync(IInputBackend input, CancellationToken cancellationToken = default)
    {
        return ZoomOutAsync(input, _options.CenterX, _options.CenterY, cancellationToken);
    }

    /// <summary>
    /// Zoom out tại toạ độ chỉ định.
    /// </summary>
    /// <param name="input">Backend input có hỗ trợ con lăn.</param>
    /// <param name="x">Toạ độ X tâm zoom.</param>
    /// <param name="y">Toạ độ Y tâm zoom.</param>
    /// <param name="cancellationToken">Token huỷ.</param>
    /// <returns>
    /// True nếu đã gửi đủ số nấc con lăn; false nếu một nấc gửi thất bại
    /// (ví dụ ADB không hỗ trợ <c>input roll</c>).
    /// </returns>
    public async Task<bool> ZoomOutAsync(
        IInputBackend input,
        int x,
        int y,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        for (int i = 0; i < _options.ScrollTicks; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!input.Scroll(x, y, _options.WheelDelta))
            {
                return false;
            }

            await _time.DelayAsync(_options.TickDelayMs, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>
    /// Zoom out từng nấc và chụp lại để biết camera đã tới mức zoom xa nhất chưa.
    /// Khi frame không còn đổi đáng kể, dừng ngay thay vì lăn thêm (lăn thêm ở mức
    /// tối đa làm camera bị trôi ngang trên MEmu).
    /// </summary>
    /// <remarks>
    /// Đây là mức xác nhận gần đúng thay cho <c>SearchZoomOut</c> của AutoIt;
    /// khi có bộ template <c>imgxml/village</c> có thể thay bằng đo kích thước làng.
    /// </remarks>
    /// <param name="input">Backend input có hỗ trợ con lăn.</param>
    /// <param name="capture">Backend chụp màn hình dùng để so sánh trước/sau.</param>
    /// <param name="cancellationToken">Token huỷ.</param>
    /// <returns>Kết quả gồm số nấc đã gửi và tỉ lệ điểm ảnh đổi ở lần cuối.</returns>
    public async Task<CameraZoomResult> ZoomOutAndVerifyAsync(
        IInputBackend input,
        ICaptureBackend capture,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(capture);

        double lastChangeRatio = 1.0;

        for (int tick = 0; tick < _options.MaxScrollTicks; tick++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using Mat before = Grab(capture);

            if (!input.Scroll(_options.CenterX, _options.CenterY, _options.WheelDelta))
            {
                return new CameraZoomResult(false, tick, lastChangeRatio);
            }

            await _time.DelayAsync(_options.SettleDelayMs, cancellationToken).ConfigureAwait(false);

            using Mat after = Grab(capture);
            if (!CanCompare(before, after))
            {
                // Không chụp được frame để xác nhận — tiếp tục gửi cho tới hạn mức.
                lastChangeRatio = 1.0;
                await _time.DelayAsync(_options.TickDelayMs, cancellationToken).ConfigureAwait(false);
                continue;
            }

            lastChangeRatio = CalculateChangeRatio(before, after);
            if (lastChangeRatio < _options.SaturatedChangeRatio)
            {
                return new CameraZoomResult(true, tick + 1, lastChangeRatio);
            }

            await _time.DelayAsync(_options.TickDelayMs, cancellationToken).ConfigureAwait(false);
        }

        // Đã gửi hết hạn mức; coi như đã zoom hết mức có thể.
        return new CameraZoomResult(true, _options.MaxScrollTicks, lastChangeRatio);
    }

    private static Mat Grab(ICaptureBackend capture)
    {
        try
        {
            return capture.GrabFrame();
        }
        catch
        {
            return new Mat();
        }
    }

    private static bool CanCompare(Mat before, Mat after)
    {
        return !before.Empty()
            && !after.Empty()
            && before.Type() == MatType.CV_8UC3
            && after.Type() == MatType.CV_8UC3
            && before.Rows == after.Rows
            && before.Cols == after.Cols;
    }

    /// <summary>Tỉ lệ điểm ảnh đổi đáng kể trong vùng cảnh (bỏ HUD).</summary>
    private double CalculateChangeRatio(Mat before, Mat after)
    {
        int step = System.Math.Max(1, _options.SampleStep);
        int left = System.Math.Clamp(_options.SceneLeft, 0, before.Cols - 1);
        int right = System.Math.Clamp(_options.SceneRight, left + 1, before.Cols);
        int top = System.Math.Clamp(_options.SceneTop, 0, before.Rows - 1);
        int bottom = System.Math.Clamp(_options.SceneBottom, top + 1, before.Rows);

        int total = 0;
        int changed = 0;

        for (int y = top; y < bottom; y += step)
        {
            for (int x = left; x < right; x += step)
            {
                Vec3b a = before.At<Vec3b>(y, x);
                Vec3b b = after.At<Vec3b>(y, x);
                int diff = System.Math.Abs(a.Item0 - b.Item0)
                    + System.Math.Abs(a.Item1 - b.Item1)
                    + System.Math.Abs(a.Item2 - b.Item2);

                if (diff > _options.PixelChangeThreshold)
                {
                    changed++;
                }

                total++;
            }
        }

        return total == 0 ? 1.0 : (double)changed / total;
    }
}

/// <summary>Kết quả của <see cref="CameraZoomer.ZoomOutAndVerifyAsync"/>.</summary>
/// <param name="Success">False nếu backend input từ chối lệnh con lăn.</param>
/// <param name="Ticks">Số nấc con lăn đã gửi.</param>
/// <param name="LastChangeRatio">Tỉ lệ điểm ảnh đổi ở lần đo cuối (0..1).</param>
public sealed record CameraZoomResult(bool Success, int Ticks, double LastChangeRatio);
