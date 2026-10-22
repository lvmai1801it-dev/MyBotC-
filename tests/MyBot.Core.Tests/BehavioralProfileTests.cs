using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Moq;
using Xunit;
using MyBot.Core.Abstractions;
using MyBot.Core.AntiBan;

namespace MyBot.Core.Tests;

public class BehavioralProfileTests
{
    private static ITimeProvider CreateMockTime()
    {
        var mock = new Mock<ITimeProvider>();
        mock.Setup(t => t.UtcNow).Returns(new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc));
        return mock.Object;
    }

    [Fact]
    public void Constructor_InitializesAllComponents()
    {
        // Arrange
        var time = CreateMockTime();

        // Act
        var profile = new BehavioralProfile(42, time);

        // Assert
        profile.Seed.Should().Be(42);
        profile.Delays.Should().NotBeNull();
        profile.BoxClick.Should().NotBeNull();
        profile.Bezier.Should().NotBeNull();
        profile.ClickHistory.Should().NotBeNull();
    }

    [Fact]
    public void SameSeed_ProducesSameBehavior()
    {
        // Arrange
        var time = CreateMockTime();
        var profile1 = new BehavioralProfile(42, time);
        var profile2 = new BehavioralProfile(42, time);

        // Act
        var delays1 = Enumerable.Range(0, 10)
            .Select(_ => profile1.Delays.NextDelay(80, 15, 50, 130))
            .ToList();

        var delays2 = Enumerable.Range(0, 10)
            .Select(_ => profile2.Delays.NextDelay(80, 15, 50, 130))
            .ToList();

        // Assert
        delays1.Should().Equal(delays2);
    }

    [Fact]
    public void DifferentSeed_ProducesDifferentBehavior()
    {
        // Arrange
        var time = CreateMockTime();
        var profile1 = new BehavioralProfile(42, time);
        var profile2 = new BehavioralProfile(99, time);

        // Act
        var delays1 = Enumerable.Range(0, 10)
            .Select(_ => profile1.Delays.NextDelay(80, 15, 50, 130))
            .ToList();

        var delays2 = Enumerable.Range(0, 10)
            .Select(_ => profile2.Delays.NextDelay(80, 15, 50, 130))
            .ToList();

        // Assert
        delays1.Should().NotEqual(delays2);
    }

    [Fact]
    public void ComponentSeeds_AreDifferent()
    {
        // Arrange
        var time = CreateMockTime();
        var profile = new BehavioralProfile(42, time);

        // Act - Reflection to inspect internal Random instances
        var rngDelay = typeof(HumanDelay).GetField("_rng", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(profile.Delays);
        var rngBox = typeof(BoundingBoxClick).GetField("_rng", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(profile.BoxClick);
        var rngBezier = typeof(BezierPath).GetField("_rng", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(profile.Bezier);

        // Assert
        rngDelay.Should().NotBeNull();
        rngBox.Should().NotBeNull();
        rngBezier.Should().NotBeNull();

        rngDelay.Should().NotBeSameAs(rngBox);
        rngDelay.Should().NotBeSameAs(rngBezier);
        rngBox.Should().NotBeSameAs(rngBezier);

        // Verify independent output sequences
        var expectedBoxFromSeed2 = new BoundingBoxClick(42 + 2);
        (int X, int Y) actualBox = profile.BoxClick.NextClick(0, 0, 100, 100);
        (int X, int Y) expectedBox = expectedBoxFromSeed2.NextClick(0, 0, 100, 100);
        actualBox.Should().Be(expectedBox);
    }

    [Fact]
    public void CoordinateHistory_IsInitializedWithSeed()
    {
        // Arrange
        var time = CreateMockTime();
        var profile1 = new BehavioralProfile(42, time);
        var profile2 = new BehavioralProfile(42, time);

        // Act
        (int X, int Y) click1_1 = profile1.ClickHistory.GetNextClick(10, 10, 100, 50);
        (int X, int Y) click1_2 = profile1.ClickHistory.GetNextClick(10, 10, 100, 50);

        (int X, int Y) click2_1 = profile2.ClickHistory.GetNextClick(10, 10, 100, 50);
        (int X, int Y) click2_2 = profile2.ClickHistory.GetNextClick(10, 10, 100, 50);

        // Assert - Deterministic behavior with same seed
        click1_1.Should().Be(click2_1);
        click1_2.Should().Be(click2_2);
    }
}
