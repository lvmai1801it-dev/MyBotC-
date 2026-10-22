using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MyBot.Core.Abstractions;
using MyBot.Core.Input;
using MyBot.Core.Tests.Mocks;
using MyBot.Game.CoC.State;

namespace MyBot.Core.Tests;

public class CameraZoomerTests
{
    // ZoomOut() của AutoIt: lăn xuống 5 nấc tại tâm khung 860x732.
    [Fact]
    public async Task ZoomOut_Should_Send_Wheel_Ticks_At_Center()
    {
        var input = new MockInputBackend();
        var zoomer = new CameraZoomer(new FakeTimeProvider());

        bool result = await zoomer.ZoomOutAsync(input);

        result.Should().BeTrue();
        input.ScrollActions.Should().HaveCount(CameraZoomer.Ticks);

        foreach (MockInputBackend.ScrollAction action in input.ScrollActions)
        {
            action.X.Should().Be(CameraZoomer.CenterX);
            action.Y.Should().Be(CameraZoomer.CenterY);
            action.Delta.Should().Be(CameraZoomer.WheelDelta);
        }
    }

    // Một nấc lăn thất bại thì dừng ngay, không gửi tiếp.
    [Fact]
    public async Task ZoomOut_Should_Stop_When_A_Tick_Fails()
    {
        var input = new FailingScrollInputBackend();
        var zoomer = new CameraZoomer(new FakeTimeProvider());

        bool result = await zoomer.ZoomOutAsync(input);

        result.Should().BeFalse();
        input.Attempts.Should().Be(1);
    }

    // Token đã huỷ → ném OperationCanceledException, không gửi input.
    [Fact]
    public async Task ZoomOut_Should_Throw_When_Cancelled()
    {
        var input = new MockInputBackend();
        var zoomer = new CameraZoomer(new FakeTimeProvider());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = () => zoomer.ZoomOutAsync(input, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        input.ScrollActions.Should().BeEmpty();
    }

    private sealed class FailingScrollInputBackend : IInputBackend
    {
        public int Attempts { get; private set; }

        public bool Initialize(EmulatorTarget target) => true;

        public void Tap(int x, int y)
        {
        }

        public void Swipe(int x1, int y1, int x2, int y2, int durationMs)
        {
        }

        public bool Scroll(int x, int y, int delta)
        {
            Attempts++;
            return false;
        }

        public void Dispose()
        {
        }
    }
}
