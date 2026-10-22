namespace MyBot.Game.CoC.Math;

/// <summary>Interpolates a smooth dropline path through the given control points.</summary>
public static class DroplineInterpolator
{
    private const int SplineSubdivisions = 20;

    /// <summary>Resamples the control-point path to exactly <paramref name="numPoints"/> points.</summary>
    public static List<Point2D> Interpolate(List<Point2D> controlPoints, int numPoints, bool useSpline = true)
    {
        if (controlPoints is null)
        {
            return new List<Point2D>();
        }

        if (controlPoints.Count < 2 || numPoints < 2)
        {
            return controlPoints;
        }

        List<Point2D> dense = useSpline ? BuildSpline(controlPoints) : controlPoints;

        List<double> cumulative = new() { 0 };
        double totalLength = 0;
        for (int i = 1; i < dense.Count; i++)
        {
            totalLength += Distance(dense[i - 1], dense[i]);
            cumulative.Add(totalLength);
        }

        if (totalLength <= 0)
        {
            return new List<Point2D>(controlPoints);
        }

        double stepLength = totalLength / (numPoints - 1);
        List<Point2D> result = new() { controlPoints[0] };

        for (int i = 1; i < dense.Count && result.Count < numPoints - 1; i++)
        {
            double segmentStart = cumulative[i - 1];
            double segmentEnd = cumulative[i];
            double segmentLength = segmentEnd - segmentStart;

            if (segmentLength <= 0)
            {
                continue;
            }

            while (result.Count < numPoints - 1 && stepLength * result.Count <= segmentEnd + 1e-9)
            {
                double target = stepLength * result.Count;
                double ratio = (target - segmentStart) / segmentLength;
                result.Add(Lerp(dense[i - 1], dense[i], ratio));
            }
        }

        while (result.Count < numPoints)
        {
            result.Add(dense[^1]);
        }

        // Pin the endpoints to avoid accumulated floating-point drift.
        result[0] = controlPoints[0];
        result[^1] = controlPoints[^1];

        return result;
    }

    private static List<Point2D> BuildSpline(List<Point2D> points)
    {
        List<Point2D> dense = new();

        for (int i = 0; i < points.Count - 1; i++)
        {
            Point2D p0 = points[System.Math.Max(i - 1, 0)];
            Point2D p1 = points[i];
            Point2D p2 = points[i + 1];
            Point2D p3 = points[System.Math.Min(i + 2, points.Count - 1)];

            for (int step = 0; step < SplineSubdivisions; step++)
            {
                double t = (double)step / SplineSubdivisions;
                dense.Add(CatmullRom(p0, p1, p2, p3, t));
            }
        }

        dense.Add(points[^1]);
        return dense;
    }

    private static Point2D CatmullRom(Point2D p0, Point2D p1, Point2D p2, Point2D p3, double t)
    {
        double t2 = t * t;
        double t3 = t2 * t;

        double x = 0.5 * (2 * p1.X
            + (-p0.X + p2.X) * t
            + (2 * p0.X - 5 * p1.X + 4 * p2.X - p3.X) * t2
            + (-p0.X + 3 * p1.X - 3 * p2.X + p3.X) * t3);

        double y = 0.5 * (2 * p1.Y
            + (-p0.Y + p2.Y) * t
            + (2 * p0.Y - 5 * p1.Y + 4 * p2.Y - p3.Y) * t2
            + (-p0.Y + 3 * p1.Y - 3 * p2.Y + p3.Y) * t3);

        return new Point2D(x, y);
    }

    private static Point2D Lerp(Point2D from, Point2D to, double ratio) =>
        new(from.X + (to.X - from.X) * ratio, from.Y + (to.Y - from.Y) * ratio);

    private static double Distance(Point2D a, Point2D b) =>
        System.Math.Sqrt(System.Math.Pow(b.X - a.X, 2) + System.Math.Pow(b.Y - a.Y, 2));
}
