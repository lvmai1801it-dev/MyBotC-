using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using MyBot.Core.Abstractions;
using MyBot.Core.FSM;

namespace MyBot.Core.Tests;

public class FsmTests
{
    [Fact]
    public void TransitionGuard_Should_Allow_Valid_Transitions()
    {
        TransitionGuard.IsValid(BotState.Idle, BotState.Starting).Should().BeTrue();
        TransitionGuard.IsValid(BotState.Searching, BotState.Pausing).Should().BeTrue();
        TransitionGuard.IsValid(BotState.Error, BotState.Idle).Should().BeTrue();
    }

    [Fact]
    public void TransitionGuard_Should_Block_Invalid_Transitions()
    {
        TransitionGuard.IsValid(BotState.Idle, BotState.Searching).Should().BeFalse(); // Nhảy cóc (phải qua Starting)
        TransitionGuard.IsValid(BotState.Paused, BotState.Starting).Should().BeFalse();
    }

    [Fact]
    public void StateMachine_Should_Throw_On_Invalid_Transition()
    {
        var loggerMock = new Mock<IBotLogger>();
        var fsm = new BotStateMachine(loggerMock.Object);

        // Từ Uninitialized không thể nhảy thẳng sang Searching
        Action act = () => fsm.TransitionTo(BotState.Searching);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task Watchdog_Should_Fire_Event_On_Timeout()
    {
        // Dùng Mock ITimeProvider để ép thời gian trôi qua
        var timeProviderMock = new Mock<ITimeProvider>();

        // Setup trước khi start để khi Task.Run chạy sẽ hoàn thành ngay
        timeProviderMock.Setup(t => t.DelayAsync(5000, It.IsAny<CancellationToken>()))
                        .Returns(Task.CompletedTask);

        var watchdog = new StateWatchdog(timeProviderMock.Object);
        var tcs = new TaskCompletionSource<BotState>();

        watchdog.WatchdogTimeout += (sender, state) =>
        {
            tcs.TrySetResult(state);
        };

        // Đặt timeout 5000ms
        watchdog.Start(BotState.Searching, 5000);

        // Đợi sự kiện bắn ra trong tối đa 1000ms
        var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(1000));
        completedTask.Should().Be(tcs.Task, "WatchdogTimeout event should be fired");

        var timedOutState = await tcs.Task;
        timedOutState.Should().Be(BotState.Searching);
    }
}
