namespace MyBot.Game.CoC.Math;

/// <summary>Red (no-deploy) pixels grouped by screen quadrant.</summary>
public record RedAreaResult(
    List<Point2D> TopLeft,
    List<Point2D> TopRight,
    List<Point2D> BottomLeft,
    List<Point2D> BottomRight);
