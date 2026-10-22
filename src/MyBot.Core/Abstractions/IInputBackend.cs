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
}
