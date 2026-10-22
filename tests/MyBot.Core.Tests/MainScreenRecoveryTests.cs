using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using MyBot.Core.Abstractions;
using MyBot.Core.CV;
using MyBot.Core.Tests.Mocks;
using MyBot.Game.CoC.State;
using OpenCvSharp;

namespace MyBot.Core.Tests;

public class MainScreenRecoveryTests
{
    private const int Width = 860;
    private const int Height = 732;

    private static readonly MainScreenRecoveryOptions FastOptions = new()
    {
        CheckIntervalMs = 0,
        WaitIntervalMs = 0,
        RebootDelayMs = 0,
        MaxChecksBeforeRestart = 2,
        MaxChecksBeforeGiveUp = 5,
        WaitAttempts = 2,
        MaxRecoveryCycles = 1,
        MaxAndroidReboots = 1,
    };

    // Frame đầu tiên đã là làng chính → không restart/reboot gì.
    [Fact]
    public async Task Locate_Should_Return_True_When_First_Frame_Is_Main_Screen()
    {
        var capture = new QueueCaptureBackend();
        using var main = CreateMainFrame();
        capture.Enqueue(main);

        var device = new FakeAndroidDeviceController();
        var recovery = CreateRecovery(device, FastOptions);

        MainScreenLocationResult result = await recovery.LocateAsync(capture);

        result.Located.Should().BeTrue();
        result.Cancelled.Should().BeFalse();
        device.RestartCount.Should().Be(0);
        device.RebootCount.Should().Be(0);
        capture.GrabCount.Should().Be(1);
    }

    // Thất bại tới ngưỡng 20 (test dùng 2) → restart CoC rồi chờ màn hình làng.
    [Fact]
    public async Task Locate_Should_Restart_CoC_At_Threshold_Then_Find_Main_Screen()
    {
        var capture = new QueueCaptureBackend();
        capture.EnqueueEmpty();
        capture.EnqueueEmpty();
        using var main = CreateMainFrame();
        capture.Enqueue(main);

        var device = new FakeAndroidDeviceController();
        var recovery = CreateRecovery(device, FastOptions);

        MainScreenLocationResult result = await recovery.LocateAsync(capture);

        result.Located.Should().BeTrue();
        device.RestartCount.Should().Be(1);
        device.RestartedPackages.Should().Contain(MainScreenRecovery.CoCPackage);
        capture.GrabCount.Should().Be(3);
    }

    // Không bao giờ tìm thấy → restart CoC, chờ, reboot Android, rồi bỏ cuộc.
    [Fact]
    public async Task Locate_Should_Give_Up_After_Recovery_Cycles()
    {
        var capture = new QueueCaptureBackend();

        var device = new FakeAndroidDeviceController();
        var options = new MainScreenRecoveryOptions
        {
            CheckIntervalMs = 0,
            WaitIntervalMs = 0,
            RebootDelayMs = 0,
            MaxChecksBeforeRestart = 2,
            MaxChecksBeforeGiveUp = 2,
            WaitAttempts = 1,
            MaxRecoveryCycles = 1,
            MaxAndroidReboots = 1,
        };
        var recovery = CreateRecovery(device, options);

        MainScreenLocationResult result = await recovery.LocateAsync(capture);

        result.Located.Should().BeFalse();
        result.Cancelled.Should().BeFalse();
        result.Reason.Should().NotBeNullOrWhiteSpace();
        device.RestartCount.Should().Be(1);
        device.RebootCount.Should().Be(1);
    }

    // Token đã huỷ trước khi gọi → trả Cancelled, không chụp frame nào.
    [Fact]
    public async Task Locate_Should_Return_Cancelled_When_Token_Is_Cancelled()
    {
        var capture = new QueueCaptureBackend();
        var device = new FakeAndroidDeviceController();
        var recovery = CreateRecovery(device, FastOptions);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        MainScreenLocationResult result = await recovery.LocateAsync(capture, cancellationToken: cts.Token);

        result.Located.Should().BeFalse();
        result.Cancelled.Should().BeTrue();
        capture.GrabCount.Should().Be(0);
    }

    // Overlay reconnect quá timeout → restart CoC (checkObstacles_Network).
    [Fact]
    public async Task Locate_Should_Restart_CoC_When_Reconnect_Lasts_Too_Long()
    {
        using var template = CreatePatternTemplate();

        var capture = new QueueCaptureBackend();
        using var reconnect1 = CreateReconnectFrame(template);
        using var reconnect2 = CreateReconnectFrame(template);
        using var reconnect3 = CreateReconnectFrame(template);
        using var main = CreateMainFrame();
        capture.Enqueue(reconnect1);
        capture.Enqueue(reconnect2);
        capture.Enqueue(reconnect3);
        capture.Enqueue(main);

        var device = new FakeAndroidDeviceController();
        var guard = new MainScreenGuard(new TemplateMatcher(), reconnectTemplate: template);
        var options = new MainScreenRecoveryOptions
        {
            CheckIntervalMs = 600,
            ReconnectTimeoutMs = 1000,
            MaxReconnectRestarts = 1,
            MaxChecksBeforeGiveUp = 10,
            MaxChecksBeforeRestart = 50,
            WaitAttempts = 1,
            WaitIntervalMs = 0,
            MaxRecoveryCycles = 1,
            MaxAndroidReboots = 1,
            RebootDelayMs = 0,
        };

        var recovery = new MainScreenRecovery(guard, new FakeTimeProvider(), new Mock<IBotLogger>().Object, device, options);

        MainScreenLocationResult result = await recovery.LocateAsync(capture);

        result.Located.Should().BeTrue();
        device.RestartCount.Should().Be(1);
        device.RebootCount.Should().Be(0);
    }

    // waitMainScreen hết số lần chờ → trả false, không tự restart.
    [Fact]
    public async Task WaitForMainScreen_Should_Return_False_After_All_Attempts()
    {
        var capture = new QueueCaptureBackend();
        var device = new FakeAndroidDeviceController();
        var recovery = CreateRecovery(device, FastOptions);

        bool found = await recovery.WaitForMainScreenAsync(capture, builderBase: false, CancellationToken.None);

        found.Should().BeFalse();
        capture.GrabCount.Should().Be(FastOptions.WaitAttempts);
        device.RestartCount.Should().Be(0);
        device.RebootCount.Should().Be(0);
    }

    private static MainScreenRecovery CreateRecovery(
        IAndroidDeviceController device,
        MainScreenRecoveryOptions options)
    {
        return new MainScreenRecovery(
            new MainScreenGuard(),
            new FakeTimeProvider(),
            new Mock<IBotLogger>().Object,
            device,
            options);
    }

    private static Mat CreateMainFrame()
    {
        var pixels = new byte[Width * Height * 3];
        Array.Fill(pixels, (byte)10);

        int offset = (53 * Width + 840) * 3;
        pixels[offset] = 255;
        pixels[offset + 1] = 65;
        pixels[offset + 2] = 255;

        return new Mat(Height, Width, MatType.CV_8UC3, pixels);
    }

    private static Mat CreateReconnectFrame(Mat template)
    {
        var frame = CreateMainFrame();

        using var roi = new Mat(
            frame,
            new Rect(MainScreenGuard.ReconnectRegion.X + 4, MainScreenGuard.ReconnectRegion.Y + 4, template.Width, template.Height));
        template.CopyTo(roi);

        return frame;
    }

    private static Mat CreatePatternTemplate(int size = 8)
    {
        var pixels = new byte[size * size * 3];
        for (int i = 0; i < size * size; i++)
        {
            byte v = (byte)((i * 37 + 17) % 255);
            pixels[i * 3] = v;
            pixels[i * 3 + 1] = (byte)(255 - v);
            pixels[i * 3 + 2] = (byte)((v * 3) % 255);
        }

        return new Mat(size, size, MatType.CV_8UC3, pixels);
    }
}
