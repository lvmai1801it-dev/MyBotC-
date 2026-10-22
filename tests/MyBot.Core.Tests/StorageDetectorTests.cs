using FluentAssertions;
using Moq;
using MyBot.Core.Abstractions;
using MyBot.Game.CoC.Detection;
using OpenCvSharp;

namespace MyBot.Core.Tests;

public class StorageDetectorTests
{
    private readonly Mock<ITemplateMatcher> _matcher = new();

    [Fact]
    public void HasFullStorage_FrameOrTemplateNull_ReturnsFalse()
    {
        var sut = new StorageDetector(_matcher.Object);
        using var frame = new Mat(10, 10, MatType.CV_8UC3);
        using var template = new Mat(10, 10, MatType.CV_8UC3);
        using var empty = new Mat();

        sut.HasFullStorage(null!, template).Should().BeFalse();
        sut.HasFullStorage(frame, null!).Should().BeFalse();
        sut.HasFullStorage(empty, template).Should().BeFalse();
        sut.HasFullStorage(frame, empty).Should().BeFalse();
    }

    [Fact]
    public void HasFullStorage_MatchFound_ReturnsTrue()
    {
        _matcher
            .Setup(m => m.Find(It.IsAny<Mat>(), It.IsAny<Mat>(), It.IsAny<double>()))
            .Returns(new MatchResult(1, 2, 3, 4, 0.9));

        var sut = new StorageDetector(_matcher.Object);
        using var frame = new Mat(100, 100, MatType.CV_8UC3);
        using var template = new Mat(10, 10, MatType.CV_8UC3);

        sut.HasFullStorage(frame, template).Should().BeTrue();
    }

    [Fact]
    public void HasFullStorage_NoMatch_ReturnsFalse()
    {
        _matcher
            .Setup(m => m.Find(It.IsAny<Mat>(), It.IsAny<Mat>(), It.IsAny<double>()))
            .Returns((MatchResult?)null);

        var sut = new StorageDetector(_matcher.Object);
        using var frame = new Mat(100, 100, MatType.CV_8UC3);
        using var template = new Mat(10, 10, MatType.CV_8UC3);

        sut.HasFullStorage(frame, template).Should().BeFalse();
    }
}
