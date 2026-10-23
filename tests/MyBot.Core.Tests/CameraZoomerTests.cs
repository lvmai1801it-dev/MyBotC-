using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MyBot.Core.Abstractions;
using MyBot.Core.Input;
using MyBot.Core.Tests.Mocks;
using MyBot.Game.CoC.State;
using MyBot.Game.CoC.State.Zoom;
using OpenCvSharp;

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

    // Frame còn đổi mạnh (zoom chưa hết) → tiếp tục; nấc thường không đổi →
    // thử lại bằng Ctrl + lăn xuống; Ctrl cũng không đổi → dừng.
    [Fact]
    public async Task ZoomOutAndVerify_Should_Stop_When_Frames_Stop_Changing()
    {
        var input = new MockInputBackend();
        var capture = new QueueCaptureBackend();
        using Mat first = CreateFrame(0);
        using Mat second = CreateFrame(200);
        capture.Enqueue(first);
        capture.Enqueue(second);
        capture.Enqueue(second);
        capture.Enqueue(second);
        capture.Enqueue(second);
        capture.Enqueue(second);

        var zoomer = new CameraZoomer(new FakeTimeProvider());

        CameraZoomResult result = await zoomer.ZoomOutAndVerifyAsync(input, capture);

        result.Success.Should().BeTrue();
        result.Ticks.Should().Be(3);
        result.UsedControl.Should().BeTrue();
        result.LastChangeRatio.Should().BeLessThan(0.01);
        input.ScrollActions.Should().HaveCount(3);
        input.ScrollActions[0].Control.Should().BeFalse();
        input.ScrollActions[1].Control.Should().BeFalse();
        input.ScrollActions[2].Control.Should().BeTrue();
        capture.GrabCount.Should().Be(6);
    }

    // Đã ở mức zoom xa nhất → 1 nấc thường + 1 nấc Ctrl đều không đổi thì dừng,
    // không lăn thêm làm camera trôi.
    [Fact]
    public async Task ZoomOutAndVerify_Should_Send_One_Tick_When_Already_Saturated()
    {
        var input = new MockInputBackend();
        var capture = new QueueCaptureBackend();
        using Mat frame = CreateFrame(150);
        capture.Enqueue(frame);
        capture.Enqueue(frame);
        capture.Enqueue(frame);
        capture.Enqueue(frame);

        var zoomer = new CameraZoomer(new FakeTimeProvider());

        CameraZoomResult result = await zoomer.ZoomOutAndVerifyAsync(input, capture);

        result.Success.Should().BeTrue();
        result.Ticks.Should().Be(2);
        result.UsedControl.Should().BeTrue();
        result.LastChangeRatio.Should().BeLessThan(0.01);
        input.ScrollActions.Should().HaveCount(2);
        input.ScrollActions[0].Control.Should().BeFalse();
        input.ScrollActions[1].Control.Should().BeTrue();
    }

    // Lăn thường không làm camera đổi nhưng Ctrl + lăn xuống thì có → tiếp tục zoom
    // bằng Ctrl cho tới khi hết mức.
    [Fact]
    public async Task ZoomOutAndVerify_Should_Fall_Back_To_Control_Wheel()
    {
        var input = new MockInputBackend();
        var capture = new QueueCaptureBackend();
        using Mat zoomed = CreateFrame(60);
        using Mat outFurther = CreateFrame(220);
        capture.Enqueue(zoomed);      // before tick 1
        capture.Enqueue(zoomed);      // normal tick không đổi
        capture.Enqueue(zoomed);      // before tick 2
        capture.Enqueue(outFurther);  // Ctrl tick có đổi
        capture.Enqueue(outFurther);  // before tick 3
        capture.Enqueue(outFurther);  // Ctrl tick không đổi → dừng

        var zoomer = new CameraZoomer(new FakeTimeProvider());

        CameraZoomResult result = await zoomer.ZoomOutAndVerifyAsync(input, capture);

        result.Success.Should().BeTrue();
        result.Ticks.Should().Be(3);
        result.UsedControl.Should().BeTrue();
        input.ScrollActions.Should().HaveCount(3);
        input.ScrollActions[0].Control.Should().BeFalse();
        input.ScrollActions[1].Control.Should().BeTrue();
        input.ScrollActions[1].Delta.Should().Be(CameraZoomer.WheelDelta);
        input.ScrollActions[2].Control.Should().BeTrue();
    }

    // Không chụp được frame để xác nhận → gửi tới hạn mức rồi coi như xong.
    [Fact]
    public async Task ZoomOutAndVerify_Should_Use_Max_Ticks_When_Frames_Unavailable()
    {
        var input = new MockInputBackend();
        var capture = new QueueCaptureBackend();
        var zoomer = new CameraZoomer(
            new FakeTimeProvider(),
            new ZoomOptions { MaxScrollTicks = 3 });

        CameraZoomResult result = await zoomer.ZoomOutAndVerifyAsync(input, capture);

        result.Success.Should().BeTrue();
        result.Ticks.Should().Be(3);
        result.UsedControl.Should().BeFalse();
        input.ScrollActions.Should().HaveCount(3);
    }

    // Input từ chối lệnh lăn → báo thất bại ngay.
    [Fact]
    public async Task ZoomOutAndVerify_Should_Fail_When_Scroll_Fails()
    {
        var input = new FailingScrollInputBackend();
        var zoomer = new CameraZoomer(new FakeTimeProvider());

        CameraZoomResult result = await zoomer.ZoomOutAndVerifyAsync(
            input,
            new QueueCaptureBackend());

        result.Success.Should().BeFalse();
        result.Ticks.Should().Be(0);
        input.Attempts.Should().Be(1);
    }

    [Fact]
    public async Task ZoomOutAndVerify_Should_Throw_When_Cancelled()
    {
        var zoomer = new CameraZoomer(new FakeTimeProvider());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = () => zoomer.ZoomOutAndVerifyAsync(
            new MockInputBackend(),
            new QueueCaptureBackend(),
            cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // Frame 860x732 đồng màu, có thêm một vùng sáng để đổi nội dung.
    private static Mat CreateFrame(byte background)
    {
        var frame = new Mat(732, 860, MatType.CV_8UC3, new Scalar(background, background, background));
        using var patch = new Mat(200, 200, MatType.CV_8UC3, new Scalar(background / 2, background / 2, background / 2));
        using Mat roi = new(frame, new Rect(300, 250, 200, 200));
        patch.CopyTo(roi);
        return frame;
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
