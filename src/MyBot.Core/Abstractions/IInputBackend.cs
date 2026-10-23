using System;

namespace MyBot.Core.Abstractions;

public interface IInputBackend : IDisposable
{
    bool Initialize(EmulatorTarget target);
    void Tap(int x, int y);
    void Swipe(int x1, int y1, int x2, int y2, int durationMs);

    /// <summary>
    /// Gửi một nấc con lăn chuột tại toạ độ chỉ định (dùng để zoom camera).
    /// Backend không hỗ trợ con lăn trả về false.
    /// </summary>
    /// <param name="x">Toạ độ X (hệ toạ độ của backend).</param>
    /// <param name="y">Toạ độ Y (hệ toạ độ của backend).</param>
    /// <param name="delta">Độ lớn con lăn; giá trị âm là lăn xuống (zoom out).</param>
    /// <returns>True nếu lệnh đã được gửi thành công.</returns>
    bool Scroll(int x, int y, int delta);

    /// <summary>
    /// Gửi một nấc con lăn kèm phím Ctrl. MEmu xử lý Ctrl + lăn xuống như lệnh
    /// zoom out dự phòng (<c>ZoomOutCtrlWheelScroll</c> của bản AutoIt).
    /// Backend không hỗ trợ tổ hợp phím trả về kết quả của <see cref="Scroll(int, int, int)"/>.
    /// </summary>
    /// <param name="x">Toạ độ X (hệ toạ độ của backend).</param>
    /// <param name="y">Toạ độ Y (hệ toạ độ của backend).</param>
    /// <param name="delta">Độ lớn con lăn; giá trị âm là lăn xuống.</param>
    /// <param name="control">True để giữ Ctrl trong lúc lăn.</param>
    /// <returns>True nếu lệnh đã được gửi thành công.</returns>
    bool Scroll(int x, int y, int delta, bool control) => Scroll(x, y, delta);
}
