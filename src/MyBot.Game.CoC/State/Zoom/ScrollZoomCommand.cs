using System;
using MyBot.Core.Abstractions;

namespace MyBot.Game.CoC.State.Zoom;

/// <summary>
/// Zoom bằng con lăn chuột tại tâm khung hình. Đây là đường dự phòng của bản
/// AutoIt (<c>ZoomOutCtrlWheelScroll</c> / <c>DefaultZoomOut</c> với
/// <c>ControlSend</c>) khi không dùng được script minitouch.
/// </summary>
public sealed class ScrollZoomCommand : IZoomCommand
{
    private readonly IInputBackend _input;
    private readonly ZoomOptions _options;

    /// <summary>Khởi tạo lệnh zoom bằng con lăn.</summary>
    /// <param name="input">Backend input có hỗ trợ <see cref="IInputBackend.Scroll(int, int, int)"/>.</param>
    /// <param name="options">Tham số zoom; null dùng mặc định.</param>
    public ScrollZoomCommand(IInputBackend input, ZoomOptions? options = null)
    {
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _options = options ?? new ZoomOptions();
    }

    /// <inheritdoc />
    public string Name => "con lăn";

    /// <inheritdoc />
    public bool TryZoomStep(int attempt, bool onBuilderBaseEnemy)
    {
        return _input.Scroll(_options.CenterX, _options.CenterY, _options.WheelDelta);
    }

    /// <inheritdoc />
    public bool TryWheelStep(bool control)
    {
        return _input.Scroll(_options.CenterX, _options.CenterY, _options.WheelDelta, control);
    }
}
