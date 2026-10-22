using System;

namespace MyBot.Core.AntiBan;

/// <summary>
/// Sinh tọa độ click ngẫu nhiên trong một hình chữ nhật theo phân phối Gaussian
/// (tập trung gần tâm, clamp trong biên an toàn) để tránh click trùng điểm.
/// </summary>
public class BoundingBoxClick
{
    /// <summary>Tỷ lệ trung bình của tọa độ click so với kích thước hộp (0.5 = tâm).</summary>
    public static readonly double MeanRatio = 0.5;

    /// <summary>Độ lệch chuẩn của tỷ lệ tọa độ click.</summary>
    public static readonly double StdRatio = 0.12;

    /// <summary>Tỷ lệ nhỏ nhất được phép (biên an toàn).</summary>
    public static readonly double MinRatio = 0.2;

    /// <summary>Tỷ lệ lớn nhất được phép (biên an toàn).</summary>
    public static readonly double MaxRatio = 0.8;

    private readonly Random _rng;

    /// <summary>Khởi tạo với nguồn ngẫu nhiên mặc định.</summary>
    public BoundingBoxClick() : this(new Random()) { }

    /// <summary>Khởi tạo với seed cố định để tái lập chuỗi tọa độ.</summary>
    /// <param name="seed">Seed khởi tạo bộ sinh số ngẫu nhiên.</param>
    public BoundingBoxClick(int seed) : this(new Random(seed)) { }

    /// <summary>Khởi tạo với nguồn ngẫu nhiên do caller cung cấp.</summary>
    /// <param name="rng">Bộ sinh số ngẫu nhiên.</param>
    public BoundingBoxClick(Random rng)
    {
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
    }

    /// <summary>Sinh tọa độ click ngẫu nhiên bên trong hộp cho trước.</summary>
    /// <param name="x">Tọa độ X góc trên-trái của hộp.</param>
    /// <param name="y">Tọa độ Y góc trên-trái của hộp.</param>
    /// <param name="width">Chiều rộng hộp (phải &gt; 0).</param>
    /// <param name="height">Chiều cao hộp (phải &gt; 0).</param>
    /// <returns>Tọa độ (X, Y) đã được clamp trong phạm vi hộp.</returns>
    public (int X, int Y) NextClick(int x, int y, int width, int height)
    {
        if (width <= 0)
        {
            throw new ArgumentException("width phải > 0", nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentException("height phải > 0", nameof(height));
        }

        double u1 = _rng.NextDouble();
        if (u1 <= 0.0)
        {
            u1 = double.Epsilon;
        }

        double u2 = _rng.NextDouble();
        double zx = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        double ratioX = MeanRatio + StdRatio * zx;

        double u3 = _rng.NextDouble();
        if (u3 <= 0.0)
        {
            u3 = double.Epsilon;
        }

        double u4 = _rng.NextDouble();
        double zy = Math.Sqrt(-2.0 * Math.Log(u3)) * Math.Cos(2.0 * Math.PI * u4);
        double ratioY = MeanRatio + StdRatio * zy;

        ratioX = Math.Max(MinRatio, Math.Min(MaxRatio, ratioX));
        ratioY = Math.Max(MinRatio, Math.Min(MaxRatio, ratioY));

        int px = x + (int)Math.Round(ratioX * width);
        int py = y + (int)Math.Round(ratioY * height);

        px = Math.Max(x, Math.Min(x + width - 1, px));
        py = Math.Max(y, Math.Min(y + height - 1, py));

        (int X, int Y) result = (px, py);
        return result;
    }
}
