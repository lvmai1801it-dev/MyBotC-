namespace MyBot.Game.CoC.Math;

/// <summary>Removes outlier points from scanned red-area droplines.</summary>
public static class RedAreaCleaner
{
    /// <summary>Filters out points farther than <paramref name="maxDistance"/> from the previous kept point.</summary>
    public static List<Point2D> Clean(List<Point2D> points, double maxDistance = 25.0)
    {
        if (points is null || points.Count <= 1)
        {
            return points ?? new List<Point2D>();
        }

        List<Point2D> sorted = points.OrderBy(p => p.X).ToList();
        List<Point2D> cleaned = new() { sorted[0] };

        for (int i = 1; i < sorted.Count; i++)
        {
            Point2D lastPoint = cleaned[^1];
            Point2D point = sorted[i];
            double distance = System.Math.Sqrt(
                System.Math.Pow(point.X - lastPoint.X, 2) + System.Math.Pow(point.Y - lastPoint.Y, 2));

            if (distance <= maxDistance)
            {
                cleaned.Add(point);
            }
        }

        return cleaned;
    }

    /// <summary>Cleans all four quadrant lists in the result.</summary>
    public static RedAreaResult CleanAll(RedAreaResult rawResult, double maxDistance = 25.0) =>
        new(
            Clean(rawResult.TopLeft, maxDistance),
            Clean(rawResult.TopRight, maxDistance),
            Clean(rawResult.BottomLeft, maxDistance),
            Clean(rawResult.BottomRight, maxDistance));
}
