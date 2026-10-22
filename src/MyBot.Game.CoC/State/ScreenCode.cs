using OpenCvSharp;

namespace MyBot.Game.CoC.State;

/// <summary>
/// Mã nhận diện một pixel mốc trên khung hình game, tương đương
/// <c>$aIsMain</c> / <c>$aIsOnBuilderBase</c> của bản AutoIt.
/// </summary>
/// <param name="X">Toạ độ X trong hệ toạ độ 860x732.</param>
/// <param name="Y">Toạ độ Y trong hệ toạ độ 860x732.</param>
/// <param name="Rgb">Màu kỳ vọng dạng 0xRRGGBB.</param>
/// <param name="Tolerance">Dung sai cho phép trên từng kênh màu.</param>
public readonly record struct ScreenCode(int X, int Y, int Rgb, int Tolerance)
{
    /// <summary>Kênh đỏ của màu kỳ vọng.</summary>
    public int Red => (Rgb >> 16) & 0xFF;

    /// <summary>Kênh xanh lá của màu kỳ vọng.</summary>
    public int Green => (Rgb >> 8) & 0xFF;

    /// <summary>Kênh xanh dương của màu kỳ vọng.</summary>
    public int Blue => Rgb & 0xFF;

    /// <summary>
    /// So khớp màu BGR đọc từ OpenCV với màu kỳ vọng.
    /// </summary>
    /// <param name="bgr">Màu thực tế theo thứ tự B, G, R.</param>
    /// <returns>True nếu cả ba kênh đều nằm trong dung sai cho phép.</returns>
    public bool Matches(Vec3b bgr)
    {
        return System.Math.Abs(bgr.Item0 - Blue) <= Tolerance
            && System.Math.Abs(bgr.Item1 - Green) <= Tolerance
            && System.Math.Abs(bgr.Item2 - Red) <= Tolerance;
    }
}

/// <summary>
/// Các pixel mốc dùng để nhận diện màn hình, port từ AutoIt.
/// </summary>
public static class ScreenCodes
{
    /// <summary>Main Village — icon Elixir ở góc trên phải (840, 53).</summary>
    public static readonly ScreenCode MainVillage = new(840, 53, 0xFF41FF, 20);

    /// <summary>Builder Base — thanh Elixir tím ở góc trên phải (844, 65).</summary>
    public static readonly ScreenCode BuilderBase = new(844, 65, 0x992EFC, 15);
}
