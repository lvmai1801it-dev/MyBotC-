using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;
using MyBot.Core.AntiBan;

namespace MyBot.Core.Tests
{
    public class BezierPathTests
    {
        [Fact]
        public void Generate_BasicPath()
        {
            // Arrange
            var bezier = new BezierPath(); // default random
            int x1 = 0, y1 = 0, x2 = 100, y2 = 100;
            int numPoints = 20;
            double chance = 0.0;

            // Act
            var path = bezier.Generate(x1, y1, x2, y2, numPoints, chance);

            // Assert
            path.Count.Should().Be(20);
            path[0].Should().BeEquivalentTo((x1, y1));
            path[^1].Should().BeEquivalentTo((x2, y2));
        }

        [Fact]
        public void Generate_NotStraightLine()
        {
            // Arrange
            var bezier = new BezierPath(42); // deterministic seed
            int x1 = 0, y1 = 0, x2 = 100, y2 = 100;
            int numPoints = 20;
            double chance = 0.0;

            // Act
            var path = bezier.Generate(x1, y1, x2, y2, numPoints, chance);
            var midpoint = path[10]; // index 10 is midpoint for 20 points

            // Calculate distance from midpoint to line (0,0)-(100,100)
            double distance = Math.Abs((100 * midpoint.Y - 100 * midpoint.X) / Math.Sqrt(100 * 100 + 100 * 100));

            // Assert
            distance.Should().BeGreaterThan(1.0);
        }

        [Fact]
        public void Generate_OvershootRate()
        {
            // Arrange
            var bezier = new BezierPath(42); // deterministic seed
            int x1 = 0, y1 = 0, x2 = 100, y2 = 100;
            int numPoints = 20;
            double chance = 0.08;
            int callCount = 1000;
            double length = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));

            // Act
            int overshootCount = 0;
            for (int i = 0; i < callCount; i++)
            {
                var path = bezier.Generate(x1, y1, x2, y2, numPoints, chance);
                // Check if second-to-last point is outside destination
                var secondLast = path[^2];
                double ux = (x2 - x1) / length;
                double uy = (y2 - y1) / length;
                double projection = (secondLast.X - x1) * ux + (secondLast.Y - y1) * uy;
                if (projection > length + 2)
                {
                    overshootCount++;
                }
            }

            // Assert
            overshootCount.Should().BeInRange(60, 100);
        }

        [Fact]
        public void Generate_DeterministicWithSeed()
        {
            // Arrange
            var bezier1 = new BezierPath(42);
            var bezier2 = new BezierPath(42);
            int x1 = 0, y1 = 0, x2 = 100, y2 = 100;
            int numPoints = 20;
            double chance = 0.5;

            // Act
            var path1 = bezier1.Generate(x1, y1, x2, y2, numPoints, chance);
            var path2 = bezier2.Generate(x1, y1, x2, y2, numPoints, chance);

            // Assert
            path1.Should().BeEquivalentTo(path2);
        }

        [Fact]
        public void Generate_OvershootDisabled()
        {
            // Arrange
            var bezier = new BezierPath(123); // deterministic seed
            int x1 = 0, y1 = 0, x2 = 100, y2 = 100;
            int numPoints = 20;
            double chance = 0.0;
            double length = Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));

            // Act
            int okCount = 0;
            for (int i = 0; i < 100; i++)
            {
                var path = bezier.Generate(x1, y1, x2, y2, numPoints, chance);
                // Check if second-to-last point is outside destination
                var secondLast = path[^2];
                double ux = (x2 - x1) / length;
                double uy = (y2 - y1) / length;
                double projection = (secondLast.X - x1) * ux + (secondLast.Y - y1) * uy;
                if (projection <= length + 1)
                {
                    okCount++;
                }
            }

            // Assert
            okCount.Should().Be(100);
        }

        [Fact]
        public void Generate_OvershootAlways()
        {
            // Arrange
            var bezier = new BezierPath(456); // deterministic seed
            int x1 = 0, y1 = 0, x2 = 100, y2 = 100;
            int numPoints = 20;
            double chance = 1.0;

            // Act
            for (int i = 0; i < 100; i++)
            {
                var path = bezier.Generate(x1, y1, x2, y2, numPoints, chance);
                // Check if second-to-last point is outside destination
                var secondLast = path[^2];
                double ux = (x2 - x1) / Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
                double uy = (y2 - y1) / Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
                double projection = (secondLast.X - x1) * ux + (secondLast.Y - y1) * uy;
                projection.Should().BeGreaterThan(100 + 2);
            }
        }

        [Fact]
        public void Generate_NumPointsParam()
        {
            // Arrange
            var bezier = new BezierPath();

            // Act & Assert
            var path5 = bezier.Generate(0, 0, 100, 100, 5, 0.0);
            path5.Count.Should().Be(5);

            var path100 = bezier.Generate(0, 0, 100, 100, 100, 0.0);
            path100.Count.Should().Be(100);
        }

        [Fact]
        public void Generate_NumPointsLessThan2_Throws()
        {
            // Arrange
            var bezier = new BezierPath();

            // Act
            Action act = () => bezier.Generate(0, 0, 100, 100, 1, 0.0);

            // Assert
            act.Should().Throw<ArgumentException>()
               .WithMessage("*numPoints*");
        }

        [Fact]
        public void Generate_InvalidOvershootChance_Throws()
        {
            // Arrange
            var bezier = new BezierPath();

            // Act & Assert
            Action negChance = () => bezier.Generate(0, 0, 100, 100, 20, -0.1);
            Action highChance = () => bezier.Generate(0, 0, 100, 100, 20, 1.1);

            negChance.Should().Throw<ArgumentException>()
                     .WithMessage("*overshootChance*");

            highChance.Should().Throw<ArgumentException>()
                      .WithMessage("*overshootChance*");
        }

        [Fact]
        public void Generate_FirstLastPointExactWithOvershoot()
        {
            // Arrange
            var bezier = new BezierPath(99); // deterministic seed
            int x1 = 0, y1 = 0, x2 = 100, y2 = 100;
            int numPoints = 20;
            double chance = 1.0;

            // Act
            var path = bezier.Generate(x1, y1, x2, y2, numPoints, chance);

            // Assert
            path[0].Should().BeEquivalentTo((x1, y1));
            path[^1].Should().BeEquivalentTo((x2, y2));
        }
    }
}
