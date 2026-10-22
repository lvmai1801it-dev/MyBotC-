using System;
using FluentAssertions;
using Moq;
using Xunit;
using MyBot.Core.Abstractions;
using MyBot.Core.AntiBan;

namespace MyBot.Core.Tests;

public class CircadianScheduleTests
{
    private static ITimeProvider MockTime(DateTime utcNow)
    {
        var mock = new Mock<ITimeProvider>();
        mock.Setup(t => t.UtcNow).Returns(utcNow);
        return mock.Object;
    }

    [Fact]
    public void Constructor_Defaults()
    {
        // Arrange
        var time = MockTime(new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc));

        // Act
        var sched = new CircadianSchedule(time);

        // Assert
        sched.ActiveStart.Should().Be(new TimeSpan(6, 0, 0));
        sched.ActiveEnd.Should().Be(new TimeSpan(23, 30, 0));
    }

    [Fact]
    public void Constructor_ParseCustom()
    {
        // Arrange
        var time = MockTime(new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc));

        // Act
        var sched = new CircadianSchedule(time, "08:15-22:45");

        // Assert
        sched.ActiveStart.Should().Be(new TimeSpan(8, 15, 0));
        sched.ActiveEnd.Should().Be(new TimeSpan(22, 45, 0));
    }

    [Fact]
    public void Constructor_InvalidFormat_Throws()
    {
        // Arrange
        var time = MockTime(new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc));

        // Act & Assert
        Action act1 = () => new CircadianSchedule(time, "abc");
        act1.Should().Throw<ArgumentException>().WithMessage("*activeRange*");

        Action act2 = () => new CircadianSchedule(time, "06:00");
        act2.Should().Throw<ArgumentException>().WithMessage("*activeRange*");

        Action act3 = () => new CircadianSchedule(time, "25:00-30:00");
        act3.Should().Throw<ArgumentException>().WithMessage("*activeRange*");
    }

    [Fact]
    public void Constructor_StartAfterEnd_Throws()
    {
        // Arrange
        var time = MockTime(new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc));

        // Act & Assert
        Action act = () => new CircadianSchedule(time, "23:00-06:00");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_StartEqualsEnd_Throws()
    {
        // Arrange
        var time = MockTime(new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc));

        // Act & Assert
        Action act = () => new CircadianSchedule(time, "12:00-12:00");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ShouldSleep_InsideActive_False()
    {
        // Arrange - UtcNow=12:00, tz=UTC, active 06:00-23:30
        var time = MockTime(new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc));
        var sched = new CircadianSchedule(time, "06:00-23:30", TimeZoneInfo.Utc);

        // Act & Assert
        sched.ShouldSleep().Should().BeFalse();
    }

    [Fact]
    public void ShouldSleep_OutsideActive_True()
    {
        // Arrange
        DateTime currentUtc = new DateTime(2026, 10, 22, 3, 0, 0, DateTimeKind.Utc);
        var mock = new Mock<ITimeProvider>();
        mock.Setup(t => t.UtcNow).Returns(() => currentUtc);
        var sched = new CircadianSchedule(mock.Object, "06:00-23:30", TimeZoneInfo.Utc);

        // Act & Assert
        sched.ShouldSleep().Should().BeTrue();

        currentUtc = new DateTime(2026, 10, 22, 23, 45, 0, DateTimeKind.Utc);
        sched.ShouldSleep().Should().BeTrue();
    }

    [Fact]
    public void ShouldSleep_BoundaryInclusive()
    {
        // Arrange
        DateTime currentUtc = new DateTime(2026, 10, 22, 6, 0, 0, DateTimeKind.Utc);
        var mock = new Mock<ITimeProvider>();
        mock.Setup(t => t.UtcNow).Returns(() => currentUtc);
        var sched = new CircadianSchedule(mock.Object, "06:00-23:30", TimeZoneInfo.Utc);

        // Act & Assert - boundary inclusive: start and end are active (not sleeping)
        sched.ShouldSleep().Should().BeFalse(); // 06:00

        currentUtc = new DateTime(2026, 10, 22, 23, 30, 0, DateTimeKind.Utc);
        sched.ShouldSleep().Should().BeFalse(); // 23:30

        currentUtc = new DateTime(2026, 10, 22, 23, 31, 0, DateTimeKind.Utc);
        sched.ShouldSleep().Should().BeTrue(); // 23:31

        currentUtc = new DateTime(2026, 10, 22, 5, 59, 0, DateTimeKind.Utc);
        sched.ShouldSleep().Should().BeTrue(); // 05:59
    }

    [Fact]
    public void ShouldWakeUp_Disabled_AlwaysFalse()
    {
        // Arrange
        var time = MockTime(new DateTime(2026, 10, 22, 3, 0, 0, DateTimeKind.Utc));
        var sched = new CircadianSchedule(time, "06:00-23:30", TimeZoneInfo.Utc, enableWakeUpCollect: false);

        // Act & Assert
        sched.ShouldWakeUp().Should().BeFalse();
    }

    [Fact]
    public void ShouldWakeUp_OncePerHour()
    {
        // Arrange
        var currentUtc = new DateTime(2026, 10, 22, 3, 0, 0, DateTimeKind.Utc);
        var mock = new Mock<ITimeProvider>();
        mock.Setup(t => t.UtcNow).Returns(() => currentUtc);

        var sched = new CircadianSchedule(mock.Object, "06:00-23:30", TimeZoneInfo.Utc, enableWakeUpCollect: true);

        // Act & Assert
        sched.ShouldWakeUp().Should().BeTrue(); // 03:00

        currentUtc = currentUtc.AddMinutes(30); // 03:30
        sched.ShouldWakeUp().Should().BeFalse();

        currentUtc = currentUtc.AddMinutes(31); // 04:01
        sched.ShouldWakeUp().Should().BeTrue();

        currentUtc = currentUtc.AddMinutes(29); // 04:30
        sched.ShouldWakeUp().Should().BeFalse();

        currentUtc = currentUtc.AddMinutes(31); // 05:01
        sched.ShouldWakeUp().Should().BeTrue();
    }
}
