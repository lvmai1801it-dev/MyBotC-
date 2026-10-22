using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using MyBot.Core.Abstractions;
using MyBot.Core.AntiBan;

namespace MyBot.Core.Tests;

public class MicroBreakSchedulerTests
{
    [Fact]
    public void Constructor_DefaultsInitialized()
    {
        // Arrange
        var time = new Mock<ITimeProvider>();
        var logger = new Mock<IBotLogger>();

        // Act
        var scheduler = new MicroBreakScheduler(time.Object, logger.Object, seed: 42);

        // Assert
        scheduler.NextBreakAtRaid.Should().BeInRange(15, 25);
        scheduler.RaidCountSinceBreak.Should().Be(0);
        scheduler.IsOnBreak.Should().BeFalse();
    }

    [Fact]
    public void Constructor_InvalidRaidsRange_Throws()
    {
        // Arrange
        var time = new Mock<ITimeProvider>();
        var logger = new Mock<IBotLogger>();

        // Act & Assert
        Action actZero = () => new MicroBreakScheduler(time.Object, logger.Object, minRaids: 0, maxRaids: 25, minDurationMin: 5, maxDurationMin: 15, seed: 42);
        actZero.Should().Throw<ArgumentException>().WithMessage("*raids range*");

        Action actInverted = () => new MicroBreakScheduler(time.Object, logger.Object, minRaids: 20, maxRaids: 15, minDurationMin: 5, maxDurationMin: 15, seed: 42);
        actInverted.Should().Throw<ArgumentException>().WithMessage("*raids range*");
    }

    [Fact]
    public void Constructor_InvalidDurationRange_Throws()
    {
        // Arrange
        var time = new Mock<ITimeProvider>();
        var logger = new Mock<IBotLogger>();

        // Act & Assert
        Action actZero = () => new MicroBreakScheduler(time.Object, logger.Object, minRaids: 15, maxRaids: 25, minDurationMin: 0, maxDurationMin: 15, seed: 42);
        actZero.Should().Throw<ArgumentException>().WithMessage("*duration range*");

        Action actInverted = () => new MicroBreakScheduler(time.Object, logger.Object, minRaids: 15, maxRaids: 25, minDurationMin: 20, maxDurationMin: 15, seed: 42);
        actInverted.Should().Throw<ArgumentException>().WithMessage("*duration range*");
    }

    [Fact]
    public void RecordRaid_IncrementsCounter()
    {
        // Arrange
        var time = new Mock<ITimeProvider>();
        var logger = new Mock<IBotLogger>();
        var scheduler = new MicroBreakScheduler(time.Object, logger.Object, minRaids: 20, maxRaids: 20, minDurationMin: 5, maxDurationMin: 15, seed: 42);

        // Act
        for (int i = 0; i < 10; i++)
        {
            scheduler.RecordRaid();
        }

        // Assert
        scheduler.RaidCountSinceBreak.Should().Be(10);
        scheduler.IsOnBreak.Should().BeFalse();
    }

    [Fact]
    public void RecordRaid_TriggersBreakAtThreshold()
    {
        // Arrange
        var time = new Mock<ITimeProvider>();
        var logger = new Mock<IBotLogger>();
        var scheduler = new MicroBreakScheduler(time.Object, logger.Object, minRaids: 20, maxRaids: 20, minDurationMin: 5, maxDurationMin: 15, seed: 42);

        // Act
        for (int i = 0; i < 20; i++)
        {
            scheduler.RecordRaid();
        }

        // Assert
        scheduler.IsOnBreak.Should().BeTrue();
        scheduler.ShouldBreak().Should().BeTrue();
    }

    [Fact]
    public void ShouldBreak_FalseBeforeThreshold()
    {
        // Arrange
        var time = new Mock<ITimeProvider>();
        var logger = new Mock<IBotLogger>();
        var scheduler = new MicroBreakScheduler(time.Object, logger.Object, minRaids: 20, maxRaids: 20, minDurationMin: 5, maxDurationMin: 15, seed: 42);

        // Act
        for (int i = 0; i < 19; i++)
        {
            scheduler.RecordRaid();
        }

        // Assert
        scheduler.ShouldBreak().Should().BeFalse();
    }

    [Fact]
    public async Task TakeBreakAsync_ResetsCounters()
    {
        // Arrange
        var time = new Mock<ITimeProvider>();
        var logger = new Mock<IBotLogger>();
        time.Setup(t => t.DelayAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var scheduler = new MicroBreakScheduler(time.Object, logger.Object, minRaids: 15, maxRaids: 25, minDurationMin: 5, maxDurationMin: 15, seed: 42);
        int target = scheduler.NextBreakAtRaid;
        for (int i = 0; i < target; i++)
        {
            scheduler.RecordRaid();
        }

        scheduler.ShouldBreak().Should().BeTrue();

        // Act
        await scheduler.TakeBreakAsync();

        // Assert
        scheduler.RaidCountSinceBreak.Should().Be(0);
        scheduler.IsOnBreak.Should().BeFalse();
        scheduler.NextBreakAtRaid.Should().BeInRange(15, 25);
    }

    [Fact]
    public async Task TakeBreakAsync_NotOnBreak_Throws()
    {
        // Arrange
        var time = new Mock<ITimeProvider>();
        var logger = new Mock<IBotLogger>();
        var scheduler = new MicroBreakScheduler(time.Object, logger.Object, seed: 42);

        // Act
        Func<Task> act = async () => await scheduler.TakeBreakAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task TakeBreakAsync_DoubleCallWhileBreaking_NoOp()
    {
        // Arrange
        var time = new Mock<ITimeProvider>();
        var logger = new Mock<IBotLogger>();
        var tcs = new TaskCompletionSource<bool>();
        time.Setup(t => t.DelayAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);

        var scheduler = new MicroBreakScheduler(time.Object, logger.Object, minRaids: 20, maxRaids: 20, minDurationMin: 5, maxDurationMin: 15, seed: 42);
        for (int i = 0; i < 20; i++)
        {
            scheduler.RecordRaid();
        }
        scheduler.IsOnBreak.Should().BeTrue();

        // Act
        var task1 = scheduler.TakeBreakAsync();
        var task2 = scheduler.TakeBreakAsync();

        tcs.SetResult(true);
        await Task.WhenAll(task1, task2);

        // Assert - DelayAsync called only once
        time.Verify(t => t.DelayAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TakeBreakAsync_CancellationPropagates()
    {
        // Arrange
        var time = new Mock<ITimeProvider>();
        var logger = new Mock<IBotLogger>();
        using var cts = new CancellationTokenSource();
        time.Setup(t => t.DelayAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException(cts.Token));

        var scheduler = new MicroBreakScheduler(time.Object, logger.Object, minRaids: 20, maxRaids: 20, minDurationMin: 5, maxDurationMin: 15, seed: 42);
        for (int i = 0; i < 20; i++)
        {
            scheduler.RecordRaid();
        }
        scheduler.IsOnBreak.Should().BeTrue();

        // Act
        Func<Task> act = async () => await scheduler.TakeBreakAsync(cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        scheduler.IsOnBreak.Should().BeTrue();
        scheduler.RaidCountSinceBreak.Should().Be(20);
    }
}
