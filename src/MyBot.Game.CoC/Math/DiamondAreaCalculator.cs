namespace MyBot.Game.CoC.Math;

/// <summary>
/// Calculates the eight reference points of the inner and outer diamond areas
/// (port of the AutoIt <c>ConvertInternalExternArea()</c> helper).
/// </summary>
public static class DiamondAreaCalculator
{
    /// <summary>Calculates the inner and outer diamond reference points.</summary>
    /// <exception cref="ArgumentException">Thrown when the inner diamond coordinates are invalid.</exception>
    public static (DiamondArea Inner, DiamondArea Outer) Calculate(
        double innerLeft,
        double innerRight,
        double innerTop,
        double innerBottom,
        double offsetLeft,
        double offsetRight,
        double offsetTop,
        double offsetBottom)
    {
        if (innerLeft >= innerRight || innerTop >= innerBottom)
        {
            throw new ArgumentException("Tọa độ hình thoi không hợp lệ");
        }

        double outerLeft = innerLeft - offsetLeft;
        double outerRight = innerRight + offsetRight;
        double outerTop = innerTop - offsetTop;
        double outerBottom = innerBottom + offsetBottom;
        double middleX = (outerLeft + outerRight) / 2.0;
        double middleY = (outerTop + outerBottom) / 2.0;

        DiamondArea outer = BuildArea(outerLeft, outerRight, outerTop, outerBottom, middleX, middleY);

        double innerMiddleX = (innerLeft + innerRight) / 2.0;
        double innerMiddleY = (innerTop + innerBottom) / 2.0;

        DiamondArea inner = BuildArea(innerLeft, innerRight, innerTop, innerBottom, innerMiddleX, innerMiddleY);

        return (inner, outer);
    }

    private static DiamondArea BuildArea(
        double left,
        double right,
        double top,
        double bottom,
        double middleX,
        double middleY)
    {
        Point2D leftPoint = new(left, middleY);
        Point2D rightPoint = new(right, middleY);
        Point2D topPoint = new(middleX, top);
        Point2D bottomPoint = new(middleX, bottom);
        Point2D topLeft = new(left + (middleX - left) / 2.0, top + (middleY - top) / 2.0);
        Point2D topRight = new(middleX + (right - middleX) / 2.0, top + (middleY - top) / 2.0);
        Point2D bottomLeft = new(left + (middleX - left) / 2.0, middleY + (bottom - middleY) / 2.0);
        Point2D bottomRight = new(middleX + (right - middleX) / 2.0, middleY + (bottom - middleY) / 2.0);

        return new DiamondArea(leftPoint, rightPoint, topPoint, bottomPoint, topLeft, topRight, bottomLeft, bottomRight);
    }

    /// <summary>Determines whether a point lies inside the diamond with the given center and half-axes.</summary>
    /// <exception cref="ArgumentException">Thrown when either half-axis is not positive.</exception>
    public static bool IsPointInside(
        double x,
        double y,
        double centerX,
        double centerY,
        double halfWidth,
        double halfHeight)
    {
        if (halfWidth <= 0 || halfHeight <= 0)
        {
            throw new ArgumentException("Bán trục hình thoi phải lớn hơn 0");
        }

        return System.Math.Abs(x - centerX) / halfWidth + System.Math.Abs(y - centerY) / halfHeight <= 1.0;
    }

    /// <summary>Determines whether a point lies inside the given diamond area.</summary>
    public static bool IsPointInsideArea(Point2D point, DiamondArea area)
    {
        double centerX = (area.Left.X + area.Right.X) / 2.0;
        double centerY = (area.Top.Y + area.Bottom.Y) / 2.0;
        double halfWidth = (area.Right.X - area.Left.X) / 2.0;
        double halfHeight = (area.Bottom.Y - area.Top.Y) / 2.0;

        return IsPointInside(point.X, point.Y, centerX, centerY, halfWidth, halfHeight);
    }
}
