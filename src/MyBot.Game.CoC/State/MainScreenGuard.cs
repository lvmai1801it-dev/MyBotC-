using System;
using System.IO;
using MyBot.Core.Abstractions;
using OpenCvSharp;

namespace MyBot.Game.CoC.State;

/// <summary>
/// Kiểm tra nhanh xem khung hình hiện tại có phải màn hình làng chính (Main Village)
/// hay không, dựa trên màu của một pixel mốc ở góc trên phải thanh tài nguyên.
///
/// Guard còn phát hiện overlay "reconnecting" (mất kết nối mạng) để tránh nhận nhầm
/// làng chính, tương đương <c>_CheckPixel(...) And Not checkObstacles_Network(...)</c>
/// của bản AutoIt. Việc phát hiện overlay cần ảnh mẫu <c>CocReconnecting.png</c>;
/// nếu thiếu ảnh, <see cref="ReconnectDetectionEnabled"/> trả về false và guard chỉ
/// kiểm tra pixel.
/// </summary>
public class MainScreenGuard
{
    /// <summary>Chiều rộng khung hình chuẩn mà mọi toạ độ được hiệu chỉnh.</summary>
    public const int ExpectedWidth = 860;

    /// <summary>Chiều cao khung hình chuẩn mà mọi toạ độ được hiệu chỉnh.</summary>
    public const int ExpectedHeight = 732;

    /// <summary>
    /// Độ lệch dọc giữa khung client 780px và vùng game 720px của bản AutoIt
    /// (<c>$g_iMidOffsetY</c>). Vùng tìm overlay reconnect dùng toạ độ game nên phải cộng offset này.
    /// </summary>
    private const int GameMidOffsetY = 30;

    private const double TemplateThreshold = 0.8;

    /// <summary>Vùng chứa icon "reconnecting" (toạ độ đã cộng offset dọc).</summary>
    public static readonly Rect ReconnectRegion = new(420, 325 + GameMidOffsetY, 20, 20);

    /// <summary>Vùng chứa nút/icon Builder Base ở góc dưới phải.</summary>
    public static readonly Rect BuilderBaseRegion = new(750, 570, 80, 60);

    private readonly ITemplateMatcher? _matcher;
    private readonly Mat? _reconnectTemplate;
    private readonly Mat? _builderBaseTemplate;

    /// <summary>
    /// Tạo guard chỉ kiểm tra pixel (không phát hiện overlay/template).
    /// Dùng cho unit test hoặc khi chưa có ảnh mẫu.
    /// </summary>
    public MainScreenGuard()
    {
    }

    /// <summary>
    /// Tạo guard có kiểm tra thêm overlay "reconnecting" và Builder Base bằng template.
    /// </summary>
    /// <param name="matcher">Bộ so khớp ảnh mẫu.</param>
    /// <param name="reconnectTemplate">
    /// Ảnh mẫu icon "reconnecting"; null để tắt kiểm tra mất kết nối.
    /// Guard không sở hữu (không dispose) các Mat này.
    /// </param>
    /// <param name="builderBaseTemplate">
    /// Ảnh mẫu nút Builder Base; null để chỉ dùng pixel mốc.
    /// </param>
    public MainScreenGuard(ITemplateMatcher matcher, Mat? reconnectTemplate = null, Mat? builderBaseTemplate = null)
    {
        _matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
        _reconnectTemplate = reconnectTemplate;
        _builderBaseTemplate = builderBaseTemplate;
    }

    /// <summary>True nếu guard đã nạp được ảnh mẫu icon "reconnecting".</summary>
    public bool ReconnectDetectionEnabled => _reconnectTemplate is not null;

    /// <summary>True nếu guard đã nạp được ảnh mẫu nút Builder Base.</summary>
    public bool BuilderBaseTemplateEnabled => _builderBaseTemplate is not null;

    /// <summary>
    /// Xác định khung hình có đang ở màn hình làng chính hay không.
    /// Tương đương <c>_checkMainScreenImage()</c>: pixel mốc khớp VÀ không có overlay reconnect.
    /// </summary>
    /// <param name="frame">Khung hình BGR cần kiểm tra.</param>
    /// <returns>True nếu là màn hình làng chính và không có overlay mất kết nối.</returns>
    public bool IsMainScreen(Mat frame)
    {
        return TryGetMarkerColor(frame, ScreenCodes.MainVillage, out Vec3b color)
            && ScreenCodes.MainVillage.Matches(color)
            && !IsReconnecting(frame);
    }

    /// <summary>
    /// Xác định khung hình có đang ở Builder Base hay không.
    /// Ưu tiên pixel mốc (844, 65); nếu chưa khớp và có ảnh mẫu thì tìm template
    /// trong vùng 750,570 → 830,630 giống <c>isOnBuilderBase()</c> của AutoIt.
    /// </summary>
    /// <param name="frame">Khung hình BGR cần kiểm tra.</param>
    /// <returns>True nếu là Builder Base.</returns>
    public bool IsBuilderBase(Mat frame)
    {
        return (TryGetMarkerColor(frame, ScreenCodes.BuilderBase, out Vec3b color)
                && ScreenCodes.BuilderBase.Matches(color))
            || MatchesTemplate(frame, BuilderBaseRegion, _builderBaseTemplate);
    }

    /// <summary>
    /// Kiểm tra màn hình theo chế độ hiện tại (Main Village hoặc Builder Base).
    /// </summary>
    /// <param name="frame">Khung hình BGR cần kiểm tra.</param>
    /// <param name="builderBase">True để kiểm tra Builder Base thay vì Main Village.</param>
    /// <returns>True nếu khung hình thuộc màn hình cần kiểm tra.</returns>
    public bool IsCurrentScreen(Mat frame, bool builderBase)
    {
        return builderBase ? IsBuilderBase(frame) : IsMainScreen(frame);
    }

    /// <summary>
    /// Phát hiện overlay "reconnecting" (mất kết nối mạng) trong vùng
    /// <see cref="ReconnectRegion"/>.
    /// </summary>
    /// <param name="frame">Khung hình BGR cần kiểm tra.</param>
    /// <returns>True nếu đang hiển thị overlay reconnect.</returns>
    public bool IsReconnecting(Mat frame)
    {
        return MatchesTemplate(frame, ReconnectRegion, _reconnectTemplate);
    }

    /// <summary>
    /// Đọc màu pixel mốc của Main Village để phục vụ debug.
    /// </summary>
    /// <param name="frame">Khung hình BGR cần đọc.</param>
    /// <param name="color">Màu BGR đọc được tại pixel mốc.</param>
    /// <returns>False nếu frame rỗng, quá nhỏ hoặc không đủ 3 kênh màu.</returns>
    public bool TryGetMarkerColor(Mat frame, out Vec3b color)
    {
        return TryGetMarkerColor(frame, ScreenCodes.MainVillage, out color);
    }

    /// <summary>
    /// Đọc màu tại pixel mốc của một <see cref="ScreenCode"/> bất kỳ.
    /// </summary>
    /// <param name="frame">Khung hình BGR cần đọc.</param>
    /// <param name="code">Pixel mốc cần đọc.</param>
    /// <param name="color">Màu BGR đọc được.</param>
    /// <returns>False nếu frame rỗng, pixel nằm ngoài khung hoặc ảnh không phải BGR.</returns>
    public bool TryGetMarkerColor(Mat frame, ScreenCode code, out Vec3b color)
    {
        color = default;

        if (frame is null || frame.Empty())
        {
            return false;
        }

        // Pixel mốc nằm ngoài khung nếu ảnh quá nhỏ, hoặc ảnh không phải BGR.
        if (frame.Width <= code.X || frame.Height <= code.Y || frame.Channels() < 3)
        {
            return false;
        }

        color = frame.At<Vec3b>(code.Y, code.X);
        return true;
    }

    /// <summary>
    /// Kiểm tra khung hình có đúng kích thước 860x732 mà toạ độ được hiệu chỉnh hay không.
    /// </summary>
    /// <param name="frame">Khung hình cần kiểm tra.</param>
    /// <returns>True nếu frame không rỗng và đúng kích thước chuẩn.</returns>
    public static bool IsExpectedFrameSize(Mat frame)
    {
        return frame is not null
            && !frame.Empty()
            && frame.Width == ExpectedWidth
            && frame.Height == ExpectedHeight;
    }

    /// <summary>
    /// Nạp ảnh mẫu <c>CocReconnecting.png</c> và <c>IsOnBuilderBase.png</c> từ thư mục
    /// ứng dụng (hoặc thư mục con <c>assets</c>) rồi tạo guard.
    /// Ảnh thiếu sẽ được bỏ qua kèm cảnh báo, guard vẫn hoạt động ở chế độ chỉ kiểm tra pixel.
    /// </summary>
    /// <param name="matcher">Bộ so khớp ảnh mẫu.</param>
    /// <param name="logger">Logger để cảnh báo khi thiếu ảnh mẫu.</param>
    /// <param name="searchDirectory">Thư mục tìm ảnh; mặc định là thư mục chạy ứng dụng.</param>
    /// <returns>Guard đã nạp các ảnh mẫu có sẵn.</returns>
    public static MainScreenGuard Create(
        ITemplateMatcher matcher,
        IBotLogger logger,
        string? searchDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(matcher);
        ArgumentNullException.ThrowIfNull(logger);

        string directory = searchDirectory ?? AppDomain.CurrentDomain.BaseDirectory;

        Mat? reconnect = LoadTemplate(directory, "CocReconnecting.png", logger, "phát hiện mất kết nối");
        Mat? builderBase = LoadTemplate(directory, "IsOnBuilderBase.png", logger, "phát hiện Builder Base");

        return new MainScreenGuard(matcher, reconnect, builderBase);
    }

    private static Mat? LoadTemplate(string directory, string fileName, IBotLogger logger, string purpose)
    {
        string directPath = Path.Combine(directory, fileName);
        string assetsPath = Path.Combine(directory, "assets", fileName);
        string? path = File.Exists(directPath) ? directPath
            : File.Exists(assetsPath) ? assetsPath
            : null;

        if (path is null)
        {
            logger.Warning($"Thiếu ảnh mẫu {fileName} — tạm tắt {purpose}.");
            return null;
        }

        Mat template = Cv2.ImRead(path, ImreadModes.Color);
        if (template.Empty())
        {
            logger.Warning($"Không đọc được ảnh mẫu {fileName} tại {path} — tạm tắt {purpose}.");
            return null;
        }

        return template;
    }

    /// <summary>
    /// Tìm template trong một vùng của frame, trả false thay vì ném lỗi khi không đủ điều kiện.
    /// </summary>
    private bool MatchesTemplate(Mat frame, Rect region, Mat? template)
    {
        if (_matcher is null || template is null || template.Empty())
        {
            return false;
        }

        if (frame is null || frame.Empty() || frame.Channels() != 3 || template.Channels() != 3)
        {
            return false;
        }

        if (region.X < 0 || region.Y < 0
            || region.Right > frame.Width || region.Bottom > frame.Height)
        {
            return false;
        }

        if (template.Width > region.Width || template.Height > region.Height)
        {
            return false;
        }

        using var crop = new Mat(frame, region);
        try
        {
            return _matcher.Find(crop, template, TemplateThreshold) is not null;
        }
        catch (Exception)
        {
            // Template không cùng kiểu/không đủ vùng: coi như không khớp, không làm chết vòng lặp.
            return false;
        }
    }
}
