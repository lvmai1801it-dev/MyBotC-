using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;
using MyBot.Core.AntiBan;

namespace MyBot.Core.Tests;

public class BoundingBoxClickTests
{
    [Fact]
    public void NextClick_BasicInBounds()
    {
        // Arrange
        var clicker = new BoundingBoxClick(42);
        int x = 100, y = 100, width = 200, height = 200;

        // Act & Assert
        for (int i = 0; i < 100; i++)
        {
            var point = clicker.NextClick(x, y, width, height);
            point.X.Should().BeInRange(140, 260);
            point.Y.Should().BeInRange(140, 260);
        }
    }

    [Fact]
    public void NextClick_StatisticalMean()
    {
        // Arrange
        var clicker = new BoundingBoxClick(42);
        int x = 0, y = 0, width = 100, height = 100;
        int n = 10000;
        double sumX = 0;
        double sumY = 0;

        // Act
        for (int i = 0; i < n; i++)
        {
            var point = clicker.NextClick(x, y, width, height);
            sumX += point.X;
            sumY += point.Y;
        }

        double meanX = sumX / n;
        double meanY = sumY / n;

        // Assert
        meanX.Should().BeInRange(48, 52);
        meanY.Should().BeInRange(48, 52);
    }

    [Fact]
    public void NextClick_StatisticalStd()
    {
        // Arrange
        var clicker = new BoundingBoxClick(42);
        int x = 0, y = 0, width = 100, height = 100;
        int n = 10000;
        var points = new List<(int X, int Y)>(n);
        double sumX = 0;
        double sumY = 0;

        // Act
        for (int i = 0; i < n; i++)
        {
            var point = clicker.NextClick(x, y, width, height);
            points.Add(point);
            sumX += point.X;
            sumY += point.Y;
        }

        double meanX = sumX / n;
        double meanY = sumY / n;

        double sumSqDiffX = points.Sum(p => (p.X - meanX) * (p.X - meanX));
        double sumSqDiffY = points.Sum(p => (p.Y - meanY) * (p.Y - meanY));

        double stdX = Math.Sqrt(sumSqDiffX / n);
        double stdY = Math.Sqrt(sumSqDiffY / n);

        // Assert
        stdX.Should().BeInRange(10, 14);
        stdY.Should().BeInRange(10, 14);
    }

    [Fact]
    public void NextClick_NeverOutsideBox()
    {
        // Arrange
        var clicker = new BoundingBoxClick(99);
        int x = 500, y = 300, width = 80, height = 60;
        int n = 10000;

        // Act & Assert
        for (int i = 0; i < n; i++)
        {
            var point = clicker.NextClick(x, y, width, height);
            point.X.Should().BeInRange(500, 579);
            point.Y.Should().BeInRange(300, 359);
        }
    }

    [Fact]
    public void NextClick_DeterministicWithSeed()
    {
        // Arrange
        var clicker1 = new BoundingBoxClick(42);
        var clicker2 = new BoundingBoxClick(42);
        int x = 50, y = 50, width = 120, height = 80;
        int n = 100;

        // Act
        var list1 = new List<(int X, int Y)>(n);
        var list2 = new List<(int X, int Y)>(n);

        for (int i = 0; i < n; i++)
        {
            list1.Add(clicker1.NextClick(x, y, width, height));
            list2.Add(clicker2.NextClick(x, y, width, height));
        }

        // Assert
        list1.Should().Equal(list2);
    }

    [Fact]
    public void NextClick_NeverAtExactCenter()
    {
        // Arrange
        var clicker = new BoundingBoxClick(42);
        int x = 0, y = 0, width = 100, height = 100;
        int n = 1000;
        int exactCenterCount = 0;

        // Act
        for (int i = 0; i < n; i++)
        {
            var point = clicker.NextClick(x, y, width, height);
            if (point.X == 50 && point.Y == 50)
            {
                exactCenterCount++;
            }
        }

        // Assert - at least 995/1000 different from (50, 50), so at most 5 exact center
        exactCenterCount.Should().BeLessThanOrEqualTo(5);
    }

    [Fact]
    public void NextClick_InvalidWidth_Throws()
    {
        // Arrange
        var clicker = new BoundingBoxClick(42);

        // Act
        Action act = () => clicker.NextClick(0, 0, 0, 100);

        // Assert
        act.Should().Throw<ArgumentException>()
           .WithMessage("*width*");
    }

    [Fact]
    public void NextClick_InvalidHeight_Throws()
    {
        // Arrange
        var clicker = new BoundingBoxClick(42);

        // Act
        Action act = () => clicker.NextClick(0, 0, 100, -5);

        // Assert
        act.Should().Throw<ArgumentException>()
           .WithMessage("*height*");
    }

    [Fact]
    public void NextClick_InvalidBoth_Throws()
    {
        // Arrange
        var clicker = new BoundingBoxClick(42);

        // Act
        Action act = () => clicker.NextClick(0, 0, 0, 0);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void NextClick_SmallBox()
    {
        // Arrange
        var clicker = new BoundingBoxClick(42);
        int x = 0, y = 0, width = 3, height = 3;
        int n = 100;

        // Act & Assert
        for (int i = 0; i < n; i++)
        {
            var point = clicker.NextClick(x, y, width, height);
            point.X.Should().BeInRange(0, 2);
            point.Y.Should().BeInRange(0, 2);
        }
    }
}
