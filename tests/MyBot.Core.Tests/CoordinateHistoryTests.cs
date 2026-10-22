using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;
using MyBot.Core.AntiBan;

namespace MyBot.Core.Tests;

public class CoordinateHistoryTests
{
    [Fact]
    public void FirstClick_NoHistoryConstraint()
    {
        // Arrange
        var history = new CoordinateHistory(42);

        // Act
        var p = history.GetNextClick(100, 100, 200, 200);

        // Assert
        p.X.Should().BeInRange(140, 260);
        p.Y.Should().BeInRange(140, 260);
        history.Count.Should().Be(1);
    }

    [Fact]
    public void SecondClick_FarFromFirst()
    {
        // Act & Assert across 20 samples
        for (int i = 0; i < 20; i++)
        {
            var history = new CoordinateHistory(42 + i);
            var p1 = history.GetNextClick(100, 100, 200, 200);
            var p2 = history.GetNextClick(100, 100, 200, 200);

            double dist = Math.Sqrt((p1.X - p2.X) * (p1.X - p2.X) + (p1.Y - p2.Y) * (p1.Y - p2.Y));
            dist.Should().BeGreaterThanOrEqualTo(4.0);
        }
    }

    [Fact]
    public void ConsecutiveClicks_RespectMinDistance()
    {
        // Arrange
        var history = new CoordinateHistory(42);
        var points = new List<(int X, int Y)>(100);

        // Act
        for (int i = 0; i < 100; i++)
        {
            points.Add(history.GetNextClick(100, 100, 200, 200));
        }

        // Assert - every adjacent pair (i, i+1) must be >= 4.0px
        for (int i = 0; i < 99; i++)
        {
            var p1 = points[i];
            var p2 = points[i + 1];
            double dist = Math.Sqrt((p1.X - p2.X) * (p1.X - p2.X) + (p1.Y - p2.Y) * (p1.Y - p2.Y));
            dist.Should().BeGreaterThanOrEqualTo(4.0);
        }
    }

    [Fact]
    public void HistorySizeCap()
    {
        // Arrange
        var history = new CoordinateHistory(42);

        // Act
        for (int i = 0; i < 7; i++)
        {
            history.GetNextClick(100, 100, 200, 200);
        }

        // Assert
        history.RecentPoints.Count.Should().Be(5);
        history.Count.Should().Be(5);
    }

    [Fact]
    public void Clear_ResetsHistory()
    {
        // Arrange
        var history = new CoordinateHistory(42);

        // Act
        for (int i = 0; i < 3; i++)
        {
            history.GetNextClick(100, 100, 200, 200);
        }

        history.Clear();

        // Assert
        history.Count.Should().Be(0);

        history.GetNextClick(100, 100, 200, 200);
        history.Count.Should().Be(1);
    }

    [Fact]
    public void AllClicksInBounds()
    {
        // Arrange
        var history = new CoordinateHistory(42);

        // Act & Assert
        for (int i = 0; i < 100; i++)
        {
            var p = history.GetNextClick(0, 0, 100, 100);
            p.X.Should().BeInRange(20, 80);
            p.Y.Should().BeInRange(20, 80);
        }
    }

    [Fact]
    public void DeterministicWithSeed()
    {
        // Arrange
        var h1 = new CoordinateHistory(42);
        var h2 = new CoordinateHistory(42);
        var list1 = new List<(int X, int Y)>(50);
        var list2 = new List<(int X, int Y)>(50);

        // Act
        for (int i = 0; i < 50; i++)
        {
            list1.Add(h1.GetNextClick(50, 50, 150, 150));
            list2.Add(h2.GetNextClick(50, 50, 150, 150));
        }

        // Assert
        list1.Should().Equal(list2);
    }

    [Fact]
    public void SmallBox_FallbackTriggered()
    {
        // Arrange
        var history = new CoordinateHistory(42);

        // Act
        var p1 = history.GetNextClick(0, 0, 2, 2);
        var p2 = history.GetNextClick(0, 0, 2, 2);

        // Assert - fallback accepts dist < 4.0 without throwing, points in bounds [0,1]
        p1.X.Should().BeInRange(0, 1);
        p1.Y.Should().BeInRange(0, 1);
        p2.X.Should().BeInRange(0, 1);
        p2.Y.Should().BeInRange(0, 1);
    }

    [Fact]
    public void RecentPoints_ReturnsSnapshot()
    {
        // Arrange
        var history = new CoordinateHistory(42);
        for (int i = 0; i < 3; i++)
        {
            history.GetNextClick(100, 100, 200, 200);
        }

        // Act
        var snap = history.RecentPoints;
        snap.Count.Should().Be(3);

        history.GetNextClick(100, 100, 200, 200);

        // Assert
        snap.Count.Should().Be(3);
        history.RecentPoints.Count.Should().Be(4);
    }

    [Fact]
    public void InvalidBox_Throws()
    {
        // Arrange
        var history = new CoordinateHistory(42);

        // Act & Assert
        Action actWidth = () => history.GetNextClick(0, 0, 0, 100);
        actWidth.Should().Throw<ArgumentException>();

        Action actHeight = () => history.GetNextClick(0, 0, 100, -1);
        actHeight.Should().Throw<ArgumentException>();
    }
}
