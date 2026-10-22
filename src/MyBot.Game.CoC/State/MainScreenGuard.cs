using System;
using OpenCvSharp;

namespace MyBot.Game.CoC.State;

/// <summary>
/// Kiểm tra nhanh xem khung hình hiện tại có phải màn hình làng chính (Main Village)
/// hay không, dựa trên màu của một pixel mốc ở góc trên phải thanh tài nguyên.
/// </summary>
public class MainScreenGuard
{
    /// <summary>Toạ độ X của pixel mốc (tính theo khung 860x732).</summary>
    private const int MarkerX = 840;

    /// <summary>Toạ độ Y của pixel mốc (tính theo khung 860x732).</summary>
    private const int MarkerY = 53;

    /// <summary>Chiều rộng tối thiểu để pixel mốc nằm trong khung hình.</summary>
    private const int MinWidth = 850;

    /// <summary>Chiều cao tối thiểu để pixel mốc nằm trong khung hình.</summary>
    private const int MinHeight = 60;

    /// <summary>Dung sai cho phép trên từng kênh màu.</summary>
    private const int Tolerance = 20;

    /// <summary>
    /// Màu mục tiêu của pixel mốc theo thứ tự B, G, R của OpenCV.
    /// AutoIt dùng <c>0xFF41FF</c> = 0xRRGGBB → RGB (255, 65, 255) → BGR (255, 65, 255).
    /// Pixel thật đo từ ảnh MEmu 860x732 là RGB (255, 67, 255), lệch 2 đơn vị ở kênh G.
    /// </summary>
    private static readonly Vec3b TargetColor = new(255, 65, 255);

    /// <summary>
    /// Xác định khung hình có đang ở màn hình làng chính hay không.
    /// </summary>
    /// <param name="frame">Khung hình BGR cần kiểm tra.</param>
    /// <returns>True nếu pixel mốc khớp màu mục tiêu trong dung sai cho phép.</returns>
    public bool IsMainScreen(Mat frame)
    {
        if (!TryGetMarkerColor(frame, out Vec3b pixel))
        {
            return false;
        }

        return System.Math.Abs(pixel.Item0 - TargetColor.Item0) <= Tolerance
            && System.Math.Abs(pixel.Item1 - TargetColor.Item1) <= Tolerance
            && System.Math.Abs(pixel.Item2 - TargetColor.Item2) <= Tolerance;
    }

    /// <summary>
    /// Đọc màu pixel mốc để phục vụ debug khi guard trả về false.
    /// </summary>
    /// <param name="frame">Khung hình BGR cần đọc.</param>
    /// <param name="color">Màu BGR đọc được tại pixel mốc.</param>
    /// <returns>False nếu frame rỗng, quá nhỏ hoặc không đủ 3 kênh màu.</returns>
    public bool TryGetMarkerColor(Mat frame, out Vec3b color)
    {
        color = default;

        if (frame is null || frame.Empty())
        {
            return false;
        }

        // Pixel mốc nằm ngoài khung nếu ảnh quá nhỏ, hoặc ảnh không phải BGR.
        if (frame.Width < MinWidth || frame.Height < MinHeight || frame.Channels() < 3)
        {
            return false;
        }

        color = frame.At<Vec3b>(MarkerY, MarkerX);
        return true;
    }
}
