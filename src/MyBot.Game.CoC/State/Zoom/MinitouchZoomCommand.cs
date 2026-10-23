using System;

namespace MyBot.Game.CoC.State.Zoom;

/// <summary>
/// Lệnh zoom out ưu tiên script minitouch qua ADB. Khi ADB không dùng được,
/// <see cref="TryZoomStep"/> trả false để engine chuyển sang con lăn.
/// </summary>
public sealed class MinitouchZoomCommand : IZoomCommand
{
    private readonly AndroidZoomOut _androidZoomOut;

    /// <summary>Khởi tạo lệnh zoom ADB.</summary>
    /// <param name="androidZoomOut">Bộ chọn/chạy script minitouch.</param>
    public MinitouchZoomCommand(AndroidZoomOut androidZoomOut)
    {
        _androidZoomOut = androidZoomOut ?? throw new ArgumentNullException(nameof(androidZoomOut));
    }

    /// <inheritdoc />
    public string Name => "minitouch";

    /// <inheritdoc />
    public bool TryZoomStep(int attempt, bool onBuilderBaseEnemy)
    {
        return _androidZoomOut.ZoomStep(onBuilderBaseEnemy);
    }

    /// <inheritdoc />
    public bool TryWheelStep(bool control) => false;
}
