namespace MyBot.Game.CoC.State.Zoom;

/// <summary>
/// Một nấc zoom out. AutoIt có ba cách gửi: script minitouch qua ADB
/// (<c>AndroidZoomOut</c>), phím/con lăn qua cửa sổ giả lập
/// (<c>ControlSend</c>/<c>MouseWheel</c>), hoặc tổ hợp Ctrl + lăn.
/// </summary>
public interface IZoomCommand
{
    /// <summary>Tên ngắn để ghi log.</summary>
    string Name { get; }

    /// <summary>
    /// Gửi một nấc zoom out.
    /// </summary>
    /// <param name="attempt">Số thứ tự vòng zoom (dùng cho chẩn đoán).</param>
    /// <param name="onBuilderBaseEnemy">True khi đang ở làng địch Builder Base.</param>
    /// <returns>False nếu lệnh không gửi được.</returns>
    bool TryZoomStep(int attempt, bool onBuilderBaseEnemy);

    /// <summary>
    /// Gửi một nấc con lăn dự phòng (AutoIt: <c>DefaultZoomOut</c>/<c>ZoomOutCtrlWheelScroll</c>).
    /// </summary>
    /// <param name="control">True để giữ Ctrl trong lúc lăn.</param>
    /// <returns>False nếu backend không hỗ trợ con lăn.</returns>
    bool TryWheelStep(bool control);
}
