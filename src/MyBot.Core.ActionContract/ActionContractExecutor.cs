using System;
using System.Diagnostics;
using System.Threading;
using OpenCvSharp;
using MyBot.Core.Abstractions;

namespace MyBot.Core.ActionContract;

/// <summary>
/// Thực thi hành động tương tác theo mẫu DAV (Detect-Act-Verify) có kiểm tra thị giác (Visual Assertion).
/// </summary>
public class ActionContractExecutor
{
    private readonly ICaptureBackend _capture;
    private readonly IInputBackend _input;
    private readonly IBotLogger _logger;
    private readonly ITimeProvider _time;
    private readonly ExecutionMode _mode;
    private readonly int _defaultMaxRetries;

    public ActionContractExecutor(
        ICaptureBackend capture,
        IInputBackend input,
        IBotLogger logger,
        ITimeProvider time,
        ExecutionMode mode,
        int defaultMaxRetries = 2)
    {
        _capture = capture;
        _input = input;
        _logger = logger;
        _time = time;
        _mode = mode;
        _defaultMaxRetries = defaultMaxRetries;
    }

    /// <summary>
    /// Thực thi click và kiểm tra điều kiện thị giác sau khi click.
    /// </summary>
    /// <param name="click">Tọa độ (x, y) để click</param>
    /// <param name="assertion">Khẳng định thị giác mong đợi sau click (null nếu không cần kiểm tra)</param>
    /// <param name="maxRetries">Số lần thử lại tối đa khi timeout (null để dùng mặc định)</param>
    /// <returns>True nếu hành động thành công hoặc thỏa mãn assertion</returns>
    public bool Execute((int x, int y) click, VisualAssertion? assertion = null, int? maxRetries = null)
    {
        if (click.x < 0 || click.y < 0)
        {
            throw new ArgumentException($"Tọa độ click không hợp lệ: ({click.x}, {click.y})");
        }

        // Chế độ Replay: bỏ qua kiểm tra hình ảnh, chỉ gửi input
        if (_mode == ExecutionMode.Replay)
        {
            _input.Tap(click.x, click.y);
            return true;
        }

        // Không yêu cầu assertion: gửi input dạng fire-and-forget
        if (assertion == null)
        {
            _input.Tap(click.x, click.y);
            return true;
        }

        int retries = maxRetries ?? _defaultMaxRetries;

        // Vòng lặp thử lại: lần đầu (attempt 0) + các lần retry
        for (int attempt = 0; attempt <= retries; attempt++)
        {
            // 1. Chụp ảnh trước khi click (preFrame)
            using var preFrame = _capture.GrabFrame();
            using var preCrop = Crop(preFrame, assertion.Region);

            // 2. Gửi lệnh click
            _input.Tap(click.x, click.y);

            // 3. Vòng lặp thăm dò (poll) kiểm tra thay đổi theo ITimeProvider
            int elapsedMs = 0;
            bool verified = false;

            while (true)
            {
                using var postFrame = _capture.GrabFrame();
                using var postCrop = Crop(postFrame, assertion.Region);

                // 5. Kiểm tra điều kiện
                if (assertion.VerifyFn(preCrop, postCrop))
                {
                    verified = true;
                    break;
                }

                if (elapsedMs >= assertion.TimeoutMs)
                {
                    break;
                }

                long remaining = assertion.TimeoutMs - elapsedMs;
                int sleepMs = (int)Math.Min(33, Math.Max(1, remaining));
                _time.DelayAsync(sleepMs).GetAwaiter().GetResult();
                elapsedMs += sleepMs;

                // Kiểm tra lại sau khi delay: nếu đã hết thời gian timeout thì dừng poll
                if (elapsedMs >= assertion.TimeoutMs)
                {
                    break;
                }
            }

            if (verified)
            {
                return true;
            }

            // 6. Timeout lần thử hiện tại
            _logger.Warning($"[ACTION_WARN] {assertion.Description} timeout after {assertion.TimeoutMs}ms");
        }

        // 7. Hết toàn bộ lượt retry
        _logger.Error($"[ACTION_FAIL] {assertion.Description} failed after {retries} retries");
        return false;
    }

    /// <summary>
    /// Cắt vùng ảnh và giới hạn tọa độ (clamp) để tránh vượt quá kích thước frame.
    /// </summary>
    private static Mat Crop(Mat? frame, Rect region)
    {
        if (frame == null || frame.Empty() || frame.Width <= 0 || frame.Height <= 0)
        {
            return new Mat();
        }

        Rect clamped = ClampRegion(region, frame.Width, frame.Height);
        return new Mat(frame, clamped).Clone();
    }

    /// <summary>
    /// Giới hạn Rect trong phạm vi kích thước của frame.
    /// </summary>
    private static Rect ClampRegion(Rect region, int frameWidth, int frameHeight)
    {
        int rx = Math.Clamp(region.X, 0, Math.Max(0, frameWidth - 1));
        int ry = Math.Clamp(region.Y, 0, Math.Max(0, frameHeight - 1));

        int rw = Math.Max(1, region.Width);
        int rh = Math.Max(1, region.Height);

        if (rx + rw > frameWidth)
        {
            rw = Math.Max(1, frameWidth - rx);
        }

        if (ry + rh > frameHeight)
        {
            rh = Math.Max(1, frameHeight - ry);
        }

        return new Rect(rx, ry, rw, rh);
    }
}
