namespace MyBot.Game.CoC.Math;

/// <summary>Groups nearby points into greedy Euclidean clusters.</summary>
public static class EuclideanClusterer
{
    /// <summary>Returns one representative point per cluster.</summary>
    public static List<Point2D> Cluster(List<Point2D> points, double maxDistance = 20.0)
    {
        List<Point2D> clusters = new();

        if (points is null)
        {
            return clusters;
        }

        foreach (Point2D point in points)
        {
            bool isDuplicate = false;

            foreach (Point2D cluster in clusters)
            {
                double distance = System.Math.Sqrt(
                    System.Math.Pow(point.X - cluster.X, 2) + System.Math.Pow(point.Y - cluster.Y, 2));

                if (distance <= maxDistance)
                {
                    isDuplicate = true;
                    break;
                }
            }

            if (!isDuplicate)
            {
                clusters.Add(point);
            }
        }

        return clusters;
    }
}
