namespace MyBot.Game.CoC.Math;

/// <summary>A point in two-dimensional screen space.</summary>
public record Point2D(double X, double Y);

/// <summary>Eight reference points describing a diamond-shaped area.</summary>
public record DiamondArea(
    Point2D Left,
    Point2D Right,
    Point2D Top,
    Point2D Bottom,
    Point2D TopLeft,
    Point2D TopRight,
    Point2D BottomLeft,
    Point2D BottomRight);
