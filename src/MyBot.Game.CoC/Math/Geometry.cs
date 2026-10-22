namespace MyBot.Game.CoC.Math;

/// <summary>A point in two-dimensional screen space.</summary>
/// <param name="X">Horizontal coordinate.</param>
/// <param name="Y">Vertical coordinate.</param>
public record Point2D(double X, double Y);

/// <summary>Eight reference points describing a diamond-shaped area.</summary>
/// <param name="Left">Leftmost vertex.</param>
/// <param name="Right">Rightmost vertex.</param>
/// <param name="Top">Topmost vertex.</param>
/// <param name="Bottom">Bottommost vertex.</param>
/// <param name="TopLeft">Vertex halfway between the left and top vertices.</param>
/// <param name="TopRight">Vertex halfway between the right and top vertices.</param>
/// <param name="BottomLeft">Vertex halfway between the left and bottom vertices.</param>
/// <param name="BottomRight">Vertex halfway between the right and bottom vertices.</param>
public record DiamondArea(
    Point2D Left,
    Point2D Right,
    Point2D Top,
    Point2D Bottom,
    Point2D TopLeft,
    Point2D TopRight,
    Point2D BottomLeft,
    Point2D BottomRight);
