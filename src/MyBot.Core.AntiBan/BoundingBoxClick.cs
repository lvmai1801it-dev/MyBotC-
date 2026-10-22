using System;

namespace MyBot.Core.AntiBan;

public class BoundingBoxClick
{
    public static readonly double MeanRatio = 0.5;
    public static readonly double StdRatio = 0.12;
    public static readonly double MinRatio = 0.2;
    public static readonly double MaxRatio = 0.8;

    private readonly Random _rng;

    public BoundingBoxClick() : this(new Random()) { }

    public BoundingBoxClick(int seed) : this(new Random(seed)) { }

    public BoundingBoxClick(Random rng)
    {
        _rng = rng ?? throw new ArgumentNullException(nameof(rng));
    }

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
