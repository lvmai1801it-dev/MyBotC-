using System;
using FluentAssertions;
using MyBot.Game.CoC.State;
using OpenCvSharp;

namespace MyBot.Core.Tests;

public class MainScreenGuardTests
{
    private const int Width = 860;
    private const int Height = 60;
    private const int MarkerX = 840;
    private const int MarkerY = 53;

    private readonly MainScreenGuard _guard = new();

    // CORE-LOOP-FINAL: frame rỗng không bao giờ là màn hình làng.
    [Fact]
    public void IsMainScreen_Should_Return_False_For_Empty_Frame()
    {
        using var frame = new Mat();

        _guard.IsMainScreen(frame).Should().BeFalse();
    }

    // CORE-LOOP-FINAL: frame nhỏ hơn 850x60 không chứa pixel mốc.
    [Fact]
    public void IsMainScreen_Should_Return_False_For_Small_Frame()
    {
        using var frame = CreateFrame(Width: 800, Height: 59);

        _guard.IsMainScreen(frame).Should().BeFalse();
    }

    // CORE-LOOP-FINAL: pixel mốc đúng màu BGR (255, 65, 255) trong dung sai 20.
    [Fact]
    public void IsMainScreen_Should_Return_True_When_Marker_Matches()
    {
        using var frame = CreateFrame(Width, Height, markerBgr: new byte[] { 250, 60, 240 });

        _guard.IsMainScreen(frame).Should().BeTrue();
    }

    // CORE-LOOP-FINAL: pixel thật đo từ ảnh MEmu 860x732 là RGB(255,67,255) → BGR(255,67,255).
    [Fact]
    public void IsMainScreen_Should_Return_True_For_Real_Captured_Pixel()
    {
        using var frame = CreateFrame(Width, Height, markerBgr: new byte[] { 255, 67, 255 });

        _guard.IsMainScreen(frame).Should().BeTrue();
    }

    // CORE-LOOP-FINAL: lệch quá dung sai 20 ở bất kỳ kênh nào đều trả false.
    [Fact]
    public void IsMainScreen_Should_Return_False_When_Marker_Is_Out_Of_Tolerance()
    {
        using var frame = CreateFrame(Width, Height, markerBgr: new byte[] { 230, 65, 255 });

        _guard.IsMainScreen(frame).Should().BeFalse();
    }

    // CORE-LOOP-FINAL: đọc nhầm thứ tự kênh (G=255 thay vì 65) phải bị từ chối.
    [Fact]
    public void IsMainScreen_Should_Return_False_When_Green_Channel_Is_Swapped()
    {
        using var frame = CreateFrame(Width, Height, markerBgr: new byte[] { 255, 255, 67 });

        _guard.IsMainScreen(frame).Should().BeFalse();
    }

    // CORE-LOOP-FINAL: frame ảnh xám (1 kênh) không đọc được pixel BGR.
    [Fact]
    public void IsMainScreen_Should_Return_False_For_Grayscale_Frame()
    {
        using var frame = new Mat(Height, Width, MatType.CV_8UC1, Scalar.All(128));

        _guard.IsMainScreen(frame).Should().BeFalse();
    }

    private static Mat CreateFrame(int Width, int Height, byte[]? markerBgr = null)
    {
        var pixels = new byte[Width * Height * 3];
        Array.Fill(pixels, (byte)10);

        if (markerBgr is not null)
        {
            int offset = (MarkerY * Width + MarkerX) * 3;
            pixels[offset] = markerBgr[0];
            pixels[offset + 1] = markerBgr[1];
            pixels[offset + 2] = markerBgr[2];
        }

        return new Mat(Height, Width, MatType.CV_8UC3, pixels);
    }
}
