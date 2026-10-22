using FluentAssertions;
using MyBot.Game.CoC.Math;

namespace MyBot.Core.Tests;

public class DroplineInterpolatorTests
{
    [Fact]
    public void Interpolate_LinearMode_ReturnsCorrectSpacing()
    {
        var controlPoints = new List<Point2D> { new(0, 0), new(100, 0) };

        var result = DroplineInterpolator.Interpolate(controlPoints, numPoints: 5, useSpline: false);

        result.Should().Equal(
            new Point2D(0, 0),
            new Point2D(25, 0),
            new Point2D(50, 0),
            new Point2D(75, 0),
            new Point2D(100, 0));
    }

    [Fact]
    public void Interpolate_SplineMode_ReturnsSmoothCurve()
    {
        var controlPoints = new List<Point2D> { new(0, 0), new(50, 50), new(100, 0) };

        var result = DroplineInterpolator.Interpolate(controlPoints, numPoints: 5, useSpline: true);

        result.Should().HaveCount(5);
        result[2].Y.Should().BeGreaterThan(25);
    }

    [Fact]
    public void Interpolate_LessThanTwoPoints_ReturnsOriginal()
    {
        var controlPoints = new List<Point2D> { new(42, 24) };

        var result = DroplineInterpolator.Interpolate(controlPoints, numPoints: 5);

        result.Should().Equal(new Point2D(42, 24));
    }

    [Fact]
    public void Interpolate_NumPointsLessThan2_ReturnsOriginal()
    {
        var controlPoints = new List<Point2D>
        {
            new(0, 0),
            new(10, 10),
            new(20, 0),
            new(30, 10),
            new(40, 0),
        };

        var result = DroplineInterpolator.Interpolate(controlPoints, numPoints: 1);

        result.Should().Equal(controlPoints);
    }

    [Fact]
    public void Interpolate_ExactPointsAtEnds()
    {
        var controlPoints = new List<Point2D> { new(0, 0), new(50, 50), new(100, 0) };

        var spline = DroplineInterpolator.Interpolate(controlPoints, numPoints: 7, useSpline: true);
        var linear = DroplineInterpolator.Interpolate(controlPoints, numPoints: 7, useSpline: false);

        spline[0].Should().Be(controlPoints[0]);
        spline[^1].Should().Be(controlPoints[^1]);
        linear[0].Should().Be(controlPoints[0]);
        linear[^1].Should().Be(controlPoints[^1]);
    }
}
