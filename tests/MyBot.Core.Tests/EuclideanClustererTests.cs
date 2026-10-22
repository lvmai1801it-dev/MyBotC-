using FluentAssertions;
using MyBot.Game.CoC.Math;

namespace MyBot.Core.Tests;

public class EuclideanClustererTests
{
    [Fact]
    public void Cluster_EmptyList_ReturnsEmpty()
    {
        var result = EuclideanClusterer.Cluster(new List<Point2D>());

        result.Should().BeEmpty();
    }

    [Fact]
    public void Cluster_FarApartPoints_ReturnsAll()
    {
        var points = new List<Point2D>
        {
            new(0, 0),
            new(100, 0),
            new(200, 0),
        };

        var result = EuclideanClusterer.Cluster(points, maxDistance: 20);

        result.Should().HaveCount(3);
    }

    [Fact]
    public void Cluster_OverlappingPoints_GroupsTogether()
    {
        var points = new List<Point2D>
        {
            new(0, 0),
            new(3, 4),
            new(6, 8),
        };

        var result = EuclideanClusterer.Cluster(points, maxDistance: 20);

        result.Should().HaveCount(1);
    }

    [Fact]
    public void Cluster_MixedPoints_GroupsCorrectly()
    {
        var points = new List<Point2D>
        {
            new(0, 0),
            new(5, 5),
            new(100, 100),
            new(105, 105),
            new(200, 200),
        };

        var result = EuclideanClusterer.Cluster(points, maxDistance: 20);

        result.Should().HaveCount(3);
        result.Should().Contain(new Point2D(0, 0));
        result.Should().Contain(new Point2D(100, 100));
        result.Should().Contain(new Point2D(200, 200));
    }

    [Fact]
    public void Cluster_SinglePoint_ReturnsOneCluster()
    {
        var points = new List<Point2D> { new(7, 9) };

        var result = EuclideanClusterer.Cluster(points);

        result.Should().Equal(new Point2D(7, 9));
    }
}
