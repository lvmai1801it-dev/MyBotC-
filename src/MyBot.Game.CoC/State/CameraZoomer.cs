using System;
using System.Threading;
using System.Threading.Tasks;
using MyBot.Core.Abstractions;

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

    /// <summary>Số nấc con lăn mỗi lần zoom out.</summary>
    public const int Ticks = 5;

    /// <summary>Khoảng nghỉ giữa hai nấc con lăn (ms).</summary>
    public const int PauseMs = 150;

    /// <summary>Toạ độ X tâm khung hình 860x732.</summary>
    public const int CenterX = 430;

    /// <summary>Toạ độ Y tâm khung hình 860x732.</summary>
    public const int CenterY = 366;

    private readonly ITimeProvider _time;

    /// <summary>Khởi tạo với nguồn thời gian dùng cho khoảng nghỉ giữa các nấc lăn.</summary>
    /// <param name="time">Nguồn thời gian (có thể mock trong test).</param>
    public CameraZoomer(ITimeProvider time)
    {
        _time = time ?? throw new ArgumentNullException(nameof(time));
    }

    /// <summary>
    /// Zoom out tại tâm khung hình 860x732.
    /// </summary>
    /// <param name="input">Backend input có hỗ trợ con lăn.</param>
    /// <param name="cancellationToken">Token huỷ.</param>
    /// <returns>True nếu đã gửi đủ số nấc con lăn.</returns>
    public Task<bool> ZoomOutAsync(IInputBackend input, CancellationToken cancellationToken = default)
    {
        return ZoomOutAsync(input, CenterX, CenterY, cancellationToken);
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

        for (int i = 0; i < Ticks; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!input.Scroll(x, y, WheelDelta))
            {
                return false;
            }

            await _time.DelayAsync(PauseMs, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }
}
