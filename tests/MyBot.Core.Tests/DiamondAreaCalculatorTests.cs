using FluentAssertions;
using MyBot.Game.CoC.Math;

namespace MyBot.Core.Tests;

public class DiamondAreaCalculatorTests
{
    [Fact]
    public void Calculate_ValidInput_ReturnsCorrectInnerAndOuterPoints()
    {
        var (inner, outer) = DiamondAreaCalculator.Calculate(
            innerLeft: 100, innerRight: 300, innerTop: 50, innerBottom: 250,
            offsetLeft: 10, offsetRight: 10, offsetTop: 10, offsetBottom: 10);

        outer.Left.Should().Be(new Point2D(90, 150));
        outer.TopLeft.Should().Be(new Point2D(145, 95));
        inner.Left.Should().Be(new Point2D(100, 150));
        inner.Top.Should().Be(new Point2D(200, 50));
    }

    [Fact]
    public void Calculate_ZeroOffsets_OuterEqualsInner()
    {
        var (inner, outer) = DiamondAreaCalculator.Calculate(
            innerLeft: 100, innerRight: 300, innerTop: 50, innerBottom: 250,
            offsetLeft: 0, offsetRight: 0, offsetTop: 0, offsetBottom: 0);

        outer.Should().Be(inner);
    }

    [Fact]
    public void Calculate_InvalidLeftRight_Throws()
    {
        Action act = () => DiamondAreaCalculator.Calculate(
            innerLeft: 300, innerRight: 300, innerTop: 50, innerBottom: 250,
            offsetLeft: 10, offsetRight: 10, offsetTop: 10, offsetBottom: 10);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Calculate_InvalidTopBottom_Throws()
    {
        Action act = () => DiamondAreaCalculator.Calculate(
            innerLeft: 100, innerRight: 300, innerTop: 250, innerBottom: 250,
            offsetLeft: 10, offsetRight: 10, offsetTop: 10, offsetBottom: 10);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Calculate_AsymmetricOffsets_ReturnsCorrectPoints()
    {
        var (_, outer) = DiamondAreaCalculator.Calculate(
            innerLeft: 100, innerRight: 300, innerTop: 50, innerBottom: 250,
            offsetLeft: 10, offsetRight: 20, offsetTop: 5, offsetBottom: 15);

        outer.Left.Should().Be(new Point2D(90, 155));
        outer.Right.Should().Be(new Point2D(320, 155));
        outer.Top.Should().Be(new Point2D(205, 45));
        outer.Bottom.Should().Be(new Point2D(205, 265));
    }
}
