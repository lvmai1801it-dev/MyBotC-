using FluentAssertions;
using Moq;
using MyBot.Core.Abstractions;
using MyBot.Core.Enums;
using MyBot.Game.CoC;
using OpenCvSharp;

namespace MyBot.Core.Tests;

public class StateClassifierTests
{
    private const double MatchScore = 0.95;
    private readonly Mock<ITemplateMatcher> _matcher = new();

    public StateClassifierTests()
    {
        _matcher
            .Setup(m => m.Find(It.IsAny<Mat>(), It.IsAny<Mat>(), It.IsAny<double>()))
            .Returns((MatchResult?)null);
    }

    [Fact]
    public void Classify_EmptyFrame_ReturnsUnknown()
    {
        using var frame = new Mat();
        var sut = new StateClassifier(_matcher.Object);

        var result = sut.Classify(frame);

        result.Should().Be(GameState.Unknown);
    }

    [Fact]
    public void Classify_PopupDetected_ReturnsPopup()
    {
        _matcher
            .SetupSequence(m => m.Find(It.IsAny<Mat>(), It.IsAny<Mat>(), It.IsAny<double>()))
            .Returns(new MatchResult(1, 2, 3, 4, MatchScore));

        using var frame = new Mat(10, 10, MatType.CV_8UC3);
        var sut = new StateClassifier(_matcher.Object);

        var result = sut.Classify(frame);

        result.Should().Be(GameState.Popup);
        sut.GetLastConfidence().Should().Be(MatchScore);
    }

    [Fact]
    public void Classify_CloudsDetected_ReturnsClouds()
    {
        _matcher
            .SetupSequence(m => m.Find(It.IsAny<Mat>(), It.IsAny<Mat>(), It.IsAny<double>()))
            .Returns((MatchResult?)null)
            .Returns(new MatchResult(1, 2, 3, 4, MatchScore));

        using var frame = new Mat(10, 10, MatType.CV_8UC3);
        var sut = new StateClassifier(_matcher.Object);

        var result = sut.Classify(frame);

        result.Should().Be(GameState.Clouds);
        sut.GetLastConfidence().Should().Be(MatchScore);
    }

    [Fact]
    public void Classify_BattleDetected_ReturnsBattle()
    {
        _matcher
            .SetupSequence(m => m.Find(It.IsAny<Mat>(), It.IsAny<Mat>(), It.IsAny<double>()))
            .Returns((MatchResult?)null)
            .Returns((MatchResult?)null)
            .Returns(new MatchResult(1, 2, 3, 4, MatchScore));

        using var frame = new Mat(10, 10, MatType.CV_8UC3);
        var sut = new StateClassifier(_matcher.Object);

        var result = sut.Classify(frame);

        result.Should().Be(GameState.Battle);
        sut.GetLastConfidence().Should().Be(MatchScore);
    }

    [Fact]
    public void Classify_VillageDetected_ReturnsVillage()
    {
        _matcher
            .SetupSequence(m => m.Find(It.IsAny<Mat>(), It.IsAny<Mat>(), It.IsAny<double>()))
            .Returns((MatchResult?)null)
            .Returns((MatchResult?)null)
            .Returns((MatchResult?)null)
            .Returns(new MatchResult(1, 2, 3, 4, MatchScore));

        using var frame = new Mat(10, 10, MatType.CV_8UC3);
        var sut = new StateClassifier(_matcher.Object);

        var result = sut.Classify(frame);

        result.Should().Be(GameState.MainVillage);
        sut.GetLastConfidence().Should().Be(MatchScore);
    }

    [Fact]
    public void Classify_NothingMatches_ReturnsUnknown()
    {
        using var frame = new Mat(10, 10, MatType.CV_8UC3);
        var sut = new StateClassifier(_matcher.Object);

        var result = sut.Classify(frame);

        result.Should().Be(GameState.Unknown);
        sut.GetLastConfidence().Should().Be(0);
    }
}
