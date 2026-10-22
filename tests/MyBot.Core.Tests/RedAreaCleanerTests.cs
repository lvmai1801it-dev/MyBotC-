using FluentAssertions;
using MyBot.Game.CoC.Math;

namespace MyBot.Core.Tests;

public class RedAreaCleanerTests
{
    [Fact]
    public void Clean_EmptyOrSinglePoint_ReturnsOriginal()
    {
        RedAreaCleaner.Clean(new List<Point2D>()).Should().BeEmpty();

        var single = new List<Point2D> { new(42, 24) };
        RedAreaCleaner.Clean(single).Should().Equal(new Point2D(42, 24));
    }

    [Fact]
    public void Clean_StraightLine_NoPointsRemoved()
    {
        var points = Enumerable.Range(0, 10).Select(i => new Point2D(i * 10, 0)).ToList();

        var cleaned = RedAreaCleaner.Clean(points, maxDistance: 15);

        cleaned.Should().HaveCount(10);
        cleaned.Should().Equal(points);
    }

    [Fact]
    public void Clean_WithOutliers_RemovesOutliers()
    {
        var points = Enumerable.Range(0, 10).Select(i => new Point2D(i * 10, 0)).ToList();
        points.Add(new Point2D(500, 500));

        var cleaned = RedAreaCleaner.Clean(points, maxDistance: 20);

        cleaned.Should().HaveCount(10);
        cleaned.Should().NotContain(new Point2D(500, 500));
    }

    [Fact]
    public void Clean_CustomMaxDistance_FiltersCorrectly()
    {
        var points = new List<Point2D>
        {
            new(0, 0),
            new(10, 0),
            new(25, 0),
            new(40, 0),
            new(100, 0),
        };

        var cleaned = RedAreaCleaner.Clean(points, maxDistance: 20);

        cleaned.Should().Equal(
            new Point2D(0, 0),
            new Point2D(10, 0),
            new Point2D(25, 0),
            new Point2D(40, 0));
    }

    [Fact]
    public void CleanAll_ProcessesAllQuadrants()
    {
        static List<Point2D> Outline() => new()
        {
            new(0, 0),
            new(10, 0),
            new(500, 500),
        };

        var raw = new RedAreaResult(Outline(), Outline(), Outline(), Outline());

        var cleaned = RedAreaCleaner.CleanAll(raw, maxDistance: 20);

        cleaned.TopLeft.Should().HaveCount(2);
        cleaned.TopRight.Should().HaveCount(2);
        cleaned.BottomLeft.Should().HaveCount(2);
        cleaned.BottomRight.Should().HaveCount(2);
    }
}
