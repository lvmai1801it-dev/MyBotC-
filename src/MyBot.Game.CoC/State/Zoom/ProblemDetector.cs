using System;
using OpenCvSharp;

namespace MyBot.Game.CoC.State.Zoom;

/// <summary>Phát hiện hộp thoại lỗi của CoC/giả lập đang che màn hình.</summary>
public interface IProblemDetector
{
    /// <summary>True nếu frame đang bị hộp thoại xám che.</summary>
    /// <param name="frame">Khung hình BGR.</param>
    /// <returns>True nếu phát hiện hộp thoại lỗi.</returns>
    bool IsProblemAffect(Mat frame);
}

/// <summary>
/// Port <c>isProblemAffect</c> của AutoIt: 6 pixel trên dải xám của hộp thoại
/// phải cùng màu xám, và pixel (823, 32) không được là màu trắng của thanh trên.
/// </summary>
public sealed class ProblemDetector : IProblemDetector
{
    private static readonly int[] GrayXs = { 253, 373, 473, 283, 320, 594 };

    private readonly int _grayY;
    private readonly byte _gray;
    private readonly int _tolerance;

    /// <summary>Khởi tạo bộ phát hiện.</summary>
    /// <param name="androidApiLevel">API Android; từ Lollipop trở lên dùng màu xám sáng hơn.</param>
    /// <param name="midOffsetY">Độ lệch dọc của vùng game (<c>$g_iMidOffsetY</c>).</param>
    /// <param name="tolerance">Sai lệch tối đa mỗi kênh màu.</param>
    public ProblemDetector(int androidApiLevel = 30, int midOffsetY = 30, int tolerance = 10)
    {
        _grayY = 395 + midOffsetY;
        _gray = androidApiLevel >= 21 ? (byte)0x42 : (byte)0x28;
        _tolerance = tolerance;
    }

    /// <inheritdoc />
    public bool IsProblemAffect(Mat frame)
    {
        if (frame is null || frame.Empty() || frame.Channels() < 3)
        {
            return false;
        }

        if (frame.Height <= _grayY || frame.Width <= 823 || frame.Height <= 32)
        {
            return false;
        }

        foreach (int x in GrayXs)
        {
            if (!IsColor(frame, x, _grayY, _gray, _gray, _gray))
            {
                return false;
            }
        }

        // Pixel này phải KHÔNG khớp màu 0xF8FCFF (RGB → BGR: FF FC F8).
        return !IsColor(frame, 823, 32, 0xFF, 0xFC, 0xF8);
    }

    private bool IsColor(Mat frame, int x, int y, byte b, byte g, byte r)
    {
        if (x < 0 || y < 0 || x >= frame.Width || y >= frame.Height)
        {
            return false;
        }

        Vec3b pixel = frame.At<Vec3b>(y, x);
        return System.Math.Abs(pixel.Item0 - b) <= _tolerance
            && System.Math.Abs(pixel.Item1 - g) <= _tolerance
            && System.Math.Abs(pixel.Item2 - r) <= _tolerance;
    }
}
