using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using MyBot.Core.Abstractions;
using MyBot.Core.AntiBan;

namespace MyBot.Core.Tests;

public class AntiBanTests
{
    [Fact]
    public void NextDelay_StatisticalMean()
    {
        var timeMock = new Mock<ITimeProvider>();
        var delay = new HumanDelay(timeMock.Object, seed: 42);

        int sampleCount = 10_000;
        var samples = new int[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            samples[i] = delay.NextDelay(80, 15, 50, 130);
        }

        double mean = samples.Average();
        double variance = samples.Select(x => Math.Pow(x - mean, 2)).Average();
        double std = Math.Sqrt(variance);

        mean.Should().BeInRange(78, 82);
        std.Should().BeInRange(13, 17);
    }

    [Fact]
    public void NextDelay_ClampBounds()
    {
        var timeMock = new Mock<ITimeProvider>();
        var delay = new HumanDelay(timeMock.Object, seed: 42);

        int sampleCount = 10_000;
        for (int i = 0; i < sampleCount; i++)
        {
            int val = delay.NextDelay(80, 15, 50, 130);
            val.Should().BeInRange(50, 130);
        }
    }

    [Fact]
    public void NextDelay_SeedReproducibility()
    {
        var timeMock = new Mock<ITimeProvider>();
        var delay1 = new HumanDelay(timeMock.Object, seed: 42);
        var delay2 = new HumanDelay(timeMock.Object, seed: 42);

        int count = 100;
        var list1 = new int[count];
        var list2 = new int[count];

        for (int i = 0; i < count; i++)
        {
            list1[i] = delay1.NextDelay(80, 15, 50, 130);
            list2[i] = delay2.NextDelay(80, 15, 50, 130);
        }

        list1.Should().Equal(list2);
    }

    [Fact]
    public void NextDelay_SigmaZero()
    {
        var timeMock = new Mock<ITimeProvider>();
        var delay = new HumanDelay(timeMock.Object, seed: 42);

        for (int i = 0; i < 100; i++)
        {
            int val = delay.NextDelay(80, 0, 50, 130);
            val.Should().Be(80);
        }
    }

    [Fact]
    public void NextDelay_MinEqualsMax()
    {
        var timeMock = new Mock<ITimeProvider>();
        var delay = new HumanDelay(timeMock.Object, seed: 42);

        for (int i = 0; i < 100; i++)
        {
            int val = delay.NextDelay(100, 10, 100, 100);
            val.Should().Be(100);
        }
    }

    [Fact]
    public void NextDelay_MeanBelowMin_Throws()
    {
        var timeMock = new Mock<ITimeProvider>();
        var delay = new HumanDelay(timeMock.Object, seed: 42);

        Action act = () => delay.NextDelay(40, 10, 50, 130);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void NextDelay_MeanAboveMax_Throws()
    {
        var timeMock = new Mock<ITimeProvider>();
        var delay = new HumanDelay(timeMock.Object, seed: 42);

        Action act = () => delay.NextDelay(140, 10, 50, 130);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void NextDelay_MinGreaterThanMax_Throws()
    {
        var timeMock = new Mock<ITimeProvider>();
        var delay = new HumanDelay(timeMock.Object, seed: 42);

        Action act = () => delay.NextDelay(80, 15, 130, 50);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task DelayAsync_CallsTimeProvider()
    {
        var timeMock = new Mock<ITimeProvider>();
        timeMock.Setup(t => t.DelayAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var delay = new HumanDelay(timeMock.Object, seed: 42);
        await delay.DelayAsync(HumanDelay.DropTroopTiming);

        timeMock.Verify(t => t.DelayAsync(
            It.Is<int>(ms => ms >= 50 && ms <= 130),
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }

    [Fact]
    public async Task DelayAsync_CancellationRespected()
    {
        var timeMock = new Mock<ITimeProvider>();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        timeMock.Setup(t => t.DelayAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var delay = new HumanDelay(timeMock.Object, seed: 42);

        Func<Task> act = async () => await delay.DelayAsync(HumanDelay.DropTroopTiming, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
