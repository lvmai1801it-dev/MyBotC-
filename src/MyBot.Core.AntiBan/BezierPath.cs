using System;
using System.Collections.Generic;

namespace MyBot.Core.AntiBan;

public class BezierPath
{
    public static readonly int DefaultNumPoints = 20;
    public static readonly double DefaultOvershootChance = 0.08;
    public static readonly double OvershootMinPx = 3.0;
    public static readonly double OvershootMaxPx = 8.0;

    private readonly Random _rng;

    public BezierPath() : this(new Random()) { }
    public BezierPath(int seed) : this(new Random(seed)) { }
    public BezierPath(Random rng) { _rng = rng ?? throw new ArgumentNullException(nameof(rng)); }

    public List<(int X, int Y)> Generate(
        int x1, int y1, int x2, int y2,
        int numPoints = 20,
        double overshootChance = 0.08)
    {
        if (numPoints < 2)
            throw new ArgumentException("numPoints phải >= 2", nameof(numPoints));
        if (overshootChance < 0 || overshootChance > 1)
            throw new ArgumentException("overshootChance phải ∈ [0,1]", nameof(overshootChance));
        if (x1 == x2 && y1 == y2)
            throw new ArgumentException("Start và End không được trùng");

        double dx = x2 - x1;
        double dy = y2 - y1;
        double length = Math.Sqrt(dx * dx + dy * dy);
        double ux = dx / length;
        double uy = dy / length;
        double perpX = -uy;
        double perpY = ux;

        bool hasOvershoot = _rng.NextDouble() < overshootChance;
        double p3x, p3y;
        if (hasOvershoot)
        {
            double overshootDist = OvershootMinPx + _rng.NextDouble() * (OvershootMaxPx - OvershootMinPx);
            p3x = x2 + ux * overshootDist;
            p3y = y2 + uy * overshootDist;
        }
        else
        {
            p3x = x2;
            p3y = y2;
        }

        double offsetMag = Math.Max(5.0, length * 0.2);
        double off1 = (_rng.NextDouble() * 2 - 1) * offsetMag;
        double off2 = (_rng.NextDouble() * 2 - 1) * offsetMag;

        // TẤT CẢ control points đều là (double X, double Y) — kiểu tường minh, không dùng anonymous
        (double X, double Y) P0 = (x1, y1);
        (double X, double Y) P3 = (p3x, p3y);
        (double X, double Y) P1 = (x1 + (p3x - x1) * 0.33 + perpX * off1,
                                   y1 + (p3y - y1) * 0.33 + perpY * off1);
        (double X, double Y) P2 = (x1 + (p3x - x1) * 0.66 + perpX * off2,
                                   y1 + (p3y - y1) * 0.66 + perpY * off2);

        var result = new List<(int X, int Y)>(numPoints);

        if (hasOvershoot)
        {
            int mainPoints = numPoints - 1;
            for (int i = 0; i < mainPoints; i++)
            {
                double t = mainPoints == 1 ? 0.0 : (double)i / (mainPoints - 1);
                var (bx, by) = Bezier(P0, P1, P2, P3, t);
                result.Add(((int)Math.Round(bx), (int)Math.Round(by)));
            }
            result.Add((x2, y2)); // snap chính xác về đích thật
        }
        else
        {
            for (int i = 0; i < numPoints; i++)
            {
                double t = (double)i / (numPoints - 1);
                var (bx, by) = Bezier(P0, P1, P2, P3, t);
                result.Add(((int)Math.Round(bx), (int)Math.Round(by)));
            }
            result[0] = (x1, y1);   // force exact đầu
            result[^1] = (x2, y2);  // force exact cuối — chống drift float
        }

        return result;
    }

    private static (double X, double Y) Bezier(
        (double X, double Y) p0,
        (double X, double Y) p1,
        (double X, double Y) p2,
        (double X, double Y) p3,
        double t)
    {
        double u = 1.0 - t;
        double x = u*u*u * p0.X + 3*u*u*t * p1.X + 3*u*t*t * p2.X + t*t*t * p3.X;
        double y = u*u*u * p0.Y + 3*u*u*t * p1.Y + 3*u*t*t * p2.Y + t*t*t * p3.Y;
        return (x, y);
    }
}
