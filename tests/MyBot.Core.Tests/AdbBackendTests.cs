using System;
using FluentAssertions;
using MyBot.Core.Abstractions;
using MyBot.Core.Capture;
using MyBot.Core.Input;
using MyBot.Core.Tests.Mocks;
using OpenCvSharp;

namespace MyBot.Core.Tests;

public class AdbBackendTests
{
    [Fact]
    public void ProcessResult_IsSuccess()
    {
        var successResult = new ProcessResult(0, "ok");
        successResult.IsSuccess.Should().BeTrue();

        var failResult = new ProcessResult(1, "fail");
        failResult.IsSuccess.Should().BeFalse();

        var timeoutResult = new ProcessResult(-1, Array.Empty<byte>(), "timeout");
        timeoutResult.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void AdbCapture_Initialize_DeviceOnline()
    {
        var mock = new MockProcessRunner();
        mock.QueueResult(0, "device\n");

        var backend = new AdbScreencapCaptureBackend("adb", mock);
        bool ok = backend.Initialize(IntPtr.Zero, "127.0.0.1:5555");

        ok.Should().BeTrue();
        backend.IsRunning.Should().BeTrue();
    }

    [Fact]
    public void AdbCapture_Initialize_DeviceOffline()
    {
        var mock = new MockProcessRunner();
        mock.QueueResult(0, "offline\n");

        var backend = new AdbScreencapCaptureBackend("adb", mock);
        bool ok = backend.Initialize(IntPtr.Zero, "127.0.0.1:5555");

        ok.Should().BeFalse();
        backend.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void AdbCapture_Initialize_ProcessFails()
    {
        var mock = new MockProcessRunner();
        mock.QueueResult(-1, Array.Empty<byte>(), "timeout");

        var backend = new AdbScreencapCaptureBackend("adb", mock);
        bool ok = backend.Initialize(IntPtr.Zero, "127.0.0.1:5555");

        ok.Should().BeFalse();
        backend.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void AdbCapture_GrabFrame_ValidPng()
    {
        using var redMat = new Mat(100, 100, MatType.CV_8UC3, Scalar.Red);
        Cv2.ImEncode(".png", redMat, out byte[] pngBytes);

        var mock = new MockProcessRunner();
        mock.QueueResult(0, "device\n");
        mock.QueueResult(0, pngBytes);

        var backend = new AdbScreencapCaptureBackend("adb", mock);
        backend.Initialize(IntPtr.Zero, "127.0.0.1:5555");

        using var frame = backend.GrabFrame();
        frame.Empty().Should().BeFalse();
        frame.Width.Should().Be(100);
        frame.Height.Should().Be(100);
    }

    [Fact]
    public void AdbCapture_GrabFrame_ProcessFail()
    {
        var mock = new MockProcessRunner();
        mock.QueueResult(0, "device\n");
        mock.QueueResult(-1, Array.Empty<byte>(), "timeout");

        var backend = new AdbScreencapCaptureBackend("adb", mock);
        backend.Initialize(IntPtr.Zero, "127.0.0.1:5555");

        using var frame = backend.GrabFrame();
        frame.Empty().Should().BeTrue();
    }

    [Fact]
    public void AdbCapture_GrabFrame_EmptyBytes()
    {
        var mock = new MockProcessRunner();
        mock.QueueResult(0, "device\n");
        mock.QueueResult(0, Array.Empty<byte>());

        var backend = new AdbScreencapCaptureBackend("adb", mock);
        backend.Initialize(IntPtr.Zero, "127.0.0.1:5555");

        using var frame = backend.GrabFrame();
        frame.Empty().Should().BeTrue();
    }

    [Fact]
    public void AdbInput_Tap_SendsCorrectCommand()
    {
        var mock = new MockProcessRunner();
        mock.QueueResult(0, "device\n");
        mock.QueueResult(0, "");

        var backend = new AdbInputBackend("adb", mock);
        backend.Initialize(IntPtr.Zero, "127.0.0.1:5555");
        backend.Tap(150, 250);

        mock.LastArguments.Should().HaveCount(2);
        mock.LastArguments[1].Arguments.Should().Be("-s 127.0.0.1:5555 shell input tap 150 250");
    }

    [Fact]
    public void AdbInput_Swipe_SendsCorrectCommand()
    {
        var mock = new MockProcessRunner();
        mock.QueueResult(0, "device\n");
        mock.QueueResult(0, "");

        var backend = new AdbInputBackend("adb", mock);
        backend.Initialize(IntPtr.Zero, "127.0.0.1:5555");
        backend.Swipe(10, 20, 30, 40, 500);

        mock.LastArguments.Should().HaveCount(2);
        mock.LastArguments[1].Arguments.Should().Be("-s 127.0.0.1:5555 shell input swipe 10 20 30 40 500");
    }

    [Fact]
    public void AdbInput_TapBeforeInitialize_Throws()
    {
        var mock = new MockProcessRunner();
        var backend = new AdbInputBackend("adb", mock);

        Action act = () => backend.Tap(100, 100);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MockProcessRunner_ThrowsWhenQueueEmpty()
    {
        var mock = new MockProcessRunner();

        Action act = () => mock.Run("adb", "devices");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AdbCapture_InitializeUsesTimeout()
    {
        var mock = new MockProcessRunner();
        mock.QueueResult(0, "device\n");

        var backend = new AdbScreencapCaptureBackend("adb", mock);
        backend.Initialize(IntPtr.Zero, "127.0.0.1:5555");

        mock.LastArguments.Should().HaveCount(1);
        mock.LastArguments[0].TimeoutMs.Should().Be(2000);
    }
}
