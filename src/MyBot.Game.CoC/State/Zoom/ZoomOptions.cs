namespace MyBot.Game.CoC.State.Zoom;

/// <summary>
/// Tham số cho <see cref="CameraZoomer"/>. Giá trị mặc định port từ bản AutoIt
/// (<c>ZoomOut.au3</c>, <c>DelayTimes.au3</c>, <c>ScreenCoordinates.au3</c>).
/// </summary>
public sealed class ZoomOptions
{
    /// <summary>Số nấc lăn khi không có frame để xác nhận (chế độ cũ). AutoIt: 5.</summary>
    public int ScrollTicks { get; init; } = 5;

    /// <summary>Số nấc lăn tối đa khi xác nhận bằng frame. AutoIt: tối đa 80 vòng.</summary>
    public int MaxScrollTicks { get; init; } = 6;

    /// <summary>Độ lớn một nấc con lăn; âm = lăn xuống (zoom out).</summary>
    public int WheelDelta { get; init; } = -120;

    /// <summary>Toạ độ X tâm khung hình 860x732.</summary>
    public int CenterX { get; init; } = 430;

    /// <summary>Toạ độ Y tâm khung hình 860x732.</summary>
    public int CenterY { get; init; } = 366;

    /// <summary>Khoảng nghỉ giữa hai nấc lăn (ms). AutoIt: DELAYZOOMOUT2 = 200ms.</summary>
    public int TickDelayMs { get; init; } = 150;

    /// <summary>Thời gian chờ camera ổn định trước khi chụp lại (ms).</summary>
    public int SettleDelayMs { get; init; } = 900;

    /// <summary>Mép trái vùng so sánh (bỏ HUD hai bên).</summary>
    public int SceneLeft { get; init; } = 40;

    /// <summary>Mép trên vùng so sánh (bỏ HUD tài nguyên).</summary>
    public int SceneTop { get; init; } = 180;

    /// <summary>Mép phải vùng so sánh.</summary>
    public int SceneRight { get; init; } = 820;

    /// <summary>Mép dưới vùng so sánh (bỏ thanh công cụ dưới).</summary>
    public int SceneBottom { get; init; } = 580;

    /// <summary>Bước lấy mẫu (px) khi so sánh hai frame.</summary>
    public int SampleStep { get; init; } = 20;

    /// <summary>Tổng chênh lệch BGR để coi một điểm ảnh là đã đổi.</summary>
    public int PixelChangeThreshold { get; init; } = 90;

    /// <summary>
    /// Tỉ lệ điểm ảnh đổi tối thiểu để coi là camera còn đổi mức zoom.
    /// Thực nghiệm trên MEmu: một nấc zoom đổi ~50% mẫu, pan/dao động ~5–20%.
    /// </summary>
    public double SaturatedChangeRatio { get; init; } = 0.25;
}
