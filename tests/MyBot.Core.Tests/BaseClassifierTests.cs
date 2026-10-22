using FluentAssertions;
using MyBot.Game.CoC.Enums;
using MyBot.Game.CoC.Math;

namespace MyBot.Core.Tests;

public class BaseClassifierTests
{
    [Fact]
    public void Classify_THOutside_ReturnsTHOutside()
    {
        var result = BaseClassifier.Classify(uniqueCollectorCount: 0, hasFullStorage: false, isTownHallOutside: true);

        result.Should().Be(BaseType.THOutside);
    }

    [Fact]
    public void Classify_DeadBase_ReturnsDeadBase()
    {
        var result = BaseClassifier.Classify(uniqueCollectorCount: 6, hasFullStorage: false, isTownHallOutside: false);

        result.Should().Be(BaseType.DeadBase);
    }

    [Fact]
    public void Classify_LiveBase_ReturnsLiveBase()
    {
        var result = BaseClassifier.Classify(uniqueCollectorCount: 2, hasFullStorage: true, isTownHallOutside: false);

        result.Should().Be(BaseType.LiveBase);
    }

    [Fact]
    public void Classify_ProgressBase_ReturnsProgressBase()
    {
        var result = BaseClassifier.Classify(uniqueCollectorCount: 0, hasFullStorage: false, isTownHallOutside: false);

        result.Should().Be(BaseType.ProgressBase);
    }

    [Fact]
    public void Classify_Ambiguous_ReturnsUnknown()
    {
        var result = BaseClassifier.Classify(uniqueCollectorCount: 4, hasFullStorage: false, isTownHallOutside: false);

        result.Should().Be(BaseType.Unknown);
    }
}
