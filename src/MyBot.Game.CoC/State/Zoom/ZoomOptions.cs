namespace MyBot.Game.CoC.State.Zoom;

/// <summary>
/// Tham số cho <see cref="CameraZoomer"/>. Giá trị mặc định port từ bản AutoIt
/// (<c>ZoomOut.au3</c>, <c>DelayTimes.au3</c>, <c>ScreenCoordinates.au3</c>).
/// </summary>
public sealed class ZoomOptions
{
    /// <summary>
    /// Chế độ zoom out của AutoIt (<c>[android] zoomoutmode</c>):
    /// 0 = mặc định, 1 = ADB minitouch, 2 = ADB dd, 3 = WinAPI, 4 = shared_prefs.
    /// </summary>
    public int ZoomOutMode { get; init; }

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

    /// <summary>Điểm kéo để căn giữa làng (<c>$aCenterHomeVillageClickDrag</c>): X.</summary>
    public int DragX { get; init; } = 430;

    /// <summary>Điểm kéo để căn giữa làng (<c>$aCenterHomeVillageClickDrag</c>): Y.</summary>
    public int DragY { get; init; } = 650;

    /// <summary>Thời gian của một cú kéo căn giữa làng (ms).</summary>
    public int SwipeDurationMs { get; init; } = 500;

    /// <summary>
    /// Dung sai căn giữa nhanh (px). AutoIt: <c>Abs($iOffsetX) &lt;= 15 And Abs($iOffsetY) &lt;= 15</c>
    /// thì coi là làng đã ở giữa và bỏ qua thao tác kéo.
    /// </summary>
    public int QuickCenterTolerancePx { get; init; } = 15;

    /// <summary>Thời gian chờ camera ổn định sau khi kéo căn giữa (ms). AutoIt: <c>_Sleep(250)</c>.</summary>
    public int CenterDelayMs { get; init; } = 250;

    /// <summary>
    /// Số lần kéo căn giữa liên tiếp tối đa khi gọi <c>CenterVillageAsync</c>.
    /// AutoIt kéo một lần mỗi <c>SearchZoomOut</c>; port lặp lại để hội tụ nhanh.
    /// </summary>
    public int QuickCenterAttempts { get; init; } = 3;

    /// <summary>Thời gian chờ trước lần zoom đầu. AutoIt: <c>DELAYZOOMOUT1</c> = 1500ms.</summary>
    public int DelayFirstMs { get; init; } = 1500;

    /// <summary>Thời gian chờ giữa hai nấc zoom. AutoIt: <c>DELAYZOOMOUT2</c> = 200ms.</summary>
    public int DelayStepMs { get; init; } = 200;

    /// <summary>Thời gian chờ sau 20 nấc zoom. AutoIt: <c>DELAYZOOMOUT3</c> = 1000ms.</summary>
    public int DelaySlowMs { get; init; } = 1000;

    /// <summary>Số vòng zoom tối đa. AutoIt: <c>$exitCount</c> = 80.</summary>
    public int MaxAttempts { get; init; } = 80;

    /// <summary>Số vòng trước khi nghỉ dài. AutoIt: <c>$delayCount</c> = 20.</summary>
    public int SlowAfterAttempts { get; init; } = 20;

    /// <summary>
    /// Số vòng zoom ADB trước khi chuyển hẳn sang Ctrl + con lăn.
    /// AutoIt: <c>$tryCtrlWheelScrollAfterCycles</c> = 40.
    /// </summary>
    public int WheelAfterAttempts { get; init; } = 40;

    /// <summary>Số lần đo làng thất bại liên tiếp trước khi restart CoC. AutoIt: 10.</summary>
    public int MaxMeasureFailures { get; init; } = 10;

    /// <summary>Số lần restart CoC trước khi reboot Android. AutoIt: 2.</summary>
    public int MaxCoCRestarts { get; init; } = 2;

    /// <summary>Kích thước làng tham chiếu. AutoIt: 440px.</summary>
    public double ReferenceVillageSize { get; init; } = 440;

    /// <summary>Hệ số dưới của dải kích thước hợp lệ. AutoIt: 0.9.</summary>
    public double MinZoomFactor { get; init; } = 0.9;

    /// <summary>Hệ số trên của dải kích thước hợp lệ. AutoIt: 1.1.</summary>
    public double MaxZoomFactor { get; init; } = 1.1;

    /// <summary>Làng lớn hơn ngưỡng này bị ép zoom thêm. AutoIt: 480px.</summary>
    public double ForceZoomAboveSize { get; init; } = 480;

    /// <summary>Số lần lỗi zoom ADB liên tiếp trước khi reboot (MEmu). AutoIt: 2.</summary>
    public int MaxAdbZoomFailures { get; init; } = 2;

    /// <summary>Thư mục chứa script minitouch, tương đối theo thư mục chạy.</summary>
    public string AdbScriptsDirectory { get; init; } = "lib/adb.scripts";

    /// <summary>Thư mục tạm trên Android để chứa minitouch và script.</summary>
    public string RemoteScriptsDirectory { get; init; } = "/data/local/tmp";

    /// <summary>Thời gian chờ tối đa khi chạy một script minitouch (ms).</summary>
    public int AdbScriptTimeoutMs { get; init; } = 20_000;

    /// <summary>Thư mục gốc chứa template đo làng (<c>imgxml/village</c>).</summary>
    public string VillageTemplatesDirectory { get; init; } = "imgxml/village";

    /// <summary>Độ lệch dọc của vùng game so với khung chụp (<c>$g_iMidOffsetY</c>).</summary>
    public int MidOffsetY { get; init; } = 30;

    /// <summary>Độ lệch dọc thanh dưới (<c>$g_iBottomOffsetY</c>).</summary>
    public int BottomOffsetY { get; init; } = 60;

    /// <summary>API Android dùng để chọn màu xám của hộp thoại lỗi.</summary>
    public int AndroidApiLevel { get; init; } = 30;
}
