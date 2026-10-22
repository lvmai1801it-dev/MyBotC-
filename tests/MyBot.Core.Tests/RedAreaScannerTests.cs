using FluentAssertions;
using MyBot.Game.CoC.Math;
using OpenCvSharp;

namespace MyBot.Core.Tests;

public class RedAreaScannerTests
{
    private static readonly Scalar Black = new(0, 0, 0);
    private static readonly Scalar Red = new(0, 0, 255);
    private static readonly Scalar Blue = new(255, 0, 0);
    private static readonly Scalar Green = new(0, 255, 0);

    private static Mat CreateFrame(Scalar color) => new(100, 100, MatType.CV_8UC3, color);

    private static void DrawSquare(Mat frame, Scalar color, int x, int y, int size)
    {
        using Mat patch = new(size, size, MatType.CV_8UC3, color);
        using Mat roi = new(frame, new Rect(x, y, size, size));
        patch.CopyTo(roi);
    }

    [Fact]
    public void Scan_EmptyFrame_ReturnsEmptyLists()
    {
        using var frame = new Mat();
        var sut = new RedAreaScanner();

        var result = sut.Scan(frame);

        result.TopLeft.Should().BeEmpty();
        result.TopRight.Should().BeEmpty();
        result.BottomLeft.Should().BeEmpty();
        result.BottomRight.Should().BeEmpty();
    }

    [Fact]
    public void Scan_NoRedPixels_ReturnsEmptyLists()
    {
        using var frame = CreateFrame(Black);
        var sut = new RedAreaScanner();

        var result = sut.Scan(frame);

        result.TopLeft.Should().BeEmpty();
        result.TopRight.Should().BeEmpty();
        result.BottomLeft.Should().BeEmpty();
        result.BottomRight.Should().BeEmpty();
    }

    [Fact]
    public void Scan_AllRed_ReturnsPixelsInAllQuadrants()
    {
        using var frame = CreateFrame(Red);
        var sut = new RedAreaScanner();

        var result = sut.Scan(frame);

        result.TopLeft.Should().NotBeEmpty();
        result.TopRight.Should().NotBeEmpty();
        result.BottomLeft.Should().NotBeEmpty();
        result.BottomRight.Should().NotBeEmpty();
        (result.TopLeft.Count + result.TopRight.Count + result.BottomLeft.Count + result.BottomRight.Count)
            .Should().Be(100 * 100);
    }

    [Fact]
    public void Scan_RedOnlyInTopLeft_ReturnsPointsOnlyInTopLeft()
    {
        using var frame = CreateFrame(Black);
        DrawSquare(frame, Red, x: 10, y: 10, size: 20);
        var sut = new RedAreaScanner();

        var result = sut.Scan(frame);

        result.TopLeft.Should().HaveCount(400);
        result.TopRight.Should().BeEmpty();
        result.BottomLeft.Should().BeEmpty();
        result.BottomRight.Should().BeEmpty();
    }

    [Fact]
    public void Scan_RedWithDifferentColors_IgnoresNonRed()
    {
        using var frame = CreateFrame(Black);
        DrawSquare(frame, Blue, x: 10, y: 10, size: 20);
        DrawSquare(frame, Green, x: 60, y: 60, size: 20);
        var sut = new RedAreaScanner();

        var result = sut.Scan(frame);

        result.TopLeft.Should().BeEmpty();
        result.TopRight.Should().BeEmpty();
        result.BottomLeft.Should().BeEmpty();
        result.BottomRight.Should().BeEmpty();
    }
}
