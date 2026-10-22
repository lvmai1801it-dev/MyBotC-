namespace MyBot.Game.CoC.Math;

/// <summary>Red (no-deploy) pixels grouped by screen quadrant.</summary>
/// <param name="TopLeft">Red pixels in the top-left quadrant.</param>
/// <param name="TopRight">Red pixels in the top-right quadrant.</param>
/// <param name="BottomLeft">Red pixels in the bottom-left quadrant.</param>
/// <param name="BottomRight">Red pixels in the bottom-right quadrant.</param>
public record RedAreaResult(
    List<Point2D> TopLeft,
    List<Point2D> TopRight,
    List<Point2D> BottomLeft,
    List<Point2D> BottomRight);
