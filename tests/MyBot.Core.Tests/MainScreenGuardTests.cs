using System;
using FluentAssertions;
using MyBot.Core.CV;
using MyBot.Game.CoC.State;
using OpenCvSharp;

namespace MyBot.Core.Tests;

public class MainScreenGuardTests
{
    private const int Width = 860;
    private const int Height = 732;
    private const int MarkerX = 840;
    private const int MarkerY = 53;
    private const int BuilderMarkerX = 844;
    private const int BuilderMarkerY = 65;

    private readonly MainScreenGuard _guard = new();

    // CORE-LOOP-FINAL: frame rỗng không bao giờ là màn hình làng.
    [Fact]
    public void IsMainScreen_Should_Return_False_For_Empty_Frame()
    {
        using var frame = new Mat();

        _guard.IsMainScreen(frame).Should().BeFalse();
    }

    // CORE-LOOP-FINAL: frame nhỏ hơn toạ độ pixel mốc không chứa pixel mốc.
    [Fact]
    public void IsMainScreen_Should_Return_False_For_Small_Frame()
    {
        using var frame = CreateFrame(800, 59);

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

    // Đúng biên dung sai ±20 phải được chấp nhận (AutoIt dùng phép so >).
    [Fact]
    public void IsMainScreen_Should_Return_True_At_Tolerance_Boundary()
    {
        using var frame = CreateFrame(Width, Height, markerBgr: new byte[] { 235, 65, 255 });

        _guard.IsMainScreen(frame).Should().BeTrue();
    }

    // Chỉ cần lệch 21 ở một kênh là đã ngoài dung sai.
    [Fact]
    public void IsMainScreen_Should_Return_False_Just_Outside_Tolerance()
    {
        using var frame = CreateFrame(Width, Height, markerBgr: new byte[] { 234, 65, 255 });

        _guard.IsMainScreen(frame).Should().BeFalse();
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

    // FIX-GUARD: đọc được màu pixel mốc để log debug.
    [Fact]
    public void TryGetMarkerColor_Should_Return_Real_Bgr_Value()
    {
        using var frame = CreateFrame(Width, Height, markerBgr: new byte[] { 255, 67, 255 });

        bool found = _guard.TryGetMarkerColor(frame, out Vec3b color);

        found.Should().BeTrue();
        color.Item0.Should().Be(255);
        color.Item1.Should().Be(67);
        color.Item2.Should().Be(255);
    }

    // FIX-GUARD: frame quá nhỏ thì không đọc pixel mốc.
    [Fact]
    public void TryGetMarkerColor_Should_Return_False_For_Small_Frame()
    {
        using var frame = CreateFrame(100, 30);

        _guard.TryGetMarkerColor(frame, out _).Should().BeFalse();
    }

    // P0: pixel mốc Builder Base (844,65) = 0x992EFC → BGR(252,46,153), dung sai 15.
    [Fact]
    public void IsBuilderBase_Should_Return_True_When_Marker_Matches()
    {
        using var frame = CreateFrame(
            Width,
            Height,
            markerBgr: new byte[] { 252, 46, 153 },
            markerX: BuilderMarkerX,
            markerY: BuilderMarkerY);

        _guard.IsBuilderBase(frame).Should().BeTrue();
    }

    // Pixel của Main Village không được nhận nhầm thành Builder Base.
    [Fact]
    public void IsBuilderBase_Should_Return_False_On_Main_Village_Marker()
    {
        using var frame = CreateFrame(Width, Height, markerBgr: new byte[] { 255, 65, 255 });

        _guard.IsBuilderBase(frame).Should().BeFalse();
    }

    // isOnBuilderBase(): template match vùng 750,570 → 830,630.
    [Fact]
    public void IsBuilderBase_Should_Detect_Template_In_Bottom_Right_Region()
    {
        using var template = CreatePatternTemplate();
        using var frame = CreateFrame(Width, Height);

        Paste(frame, template, MainScreenGuard.BuilderBaseRegion.X + 10, MainScreenGuard.BuilderBaseRegion.Y + 10);

        var guard = new MainScreenGuard(new TemplateMatcher(), reconnectTemplate: null, builderBaseTemplate: template);

        guard.BuilderBaseTemplateEnabled.Should().BeTrue();
        guard.IsBuilderBase(frame).Should().BeTrue();
    }

    // P0: pixel mốc đúng nhưng có overlay "reconnecting" thì KHÔNG phải màn hình làng.
    [Fact]
    public void IsMainScreen_Should_Return_False_When_Reconnecting_Overlay_Is_Visible()
    {
        using var template = CreatePatternTemplate();
        using var frame = CreateFrame(Width, Height, markerBgr: new byte[] { 255, 65, 255 });

        Paste(
            frame,
            template,
            MainScreenGuard.ReconnectRegion.X + 4,
            MainScreenGuard.ReconnectRegion.Y + 4);

        var guard = new MainScreenGuard(new TemplateMatcher(), reconnectTemplate: template);

        guard.ReconnectDetectionEnabled.Should().BeTrue();
        guard.IsReconnecting(frame).Should().BeTrue();
        guard.IsMainScreen(frame).Should().BeFalse();
    }

    // Có ảnh mẫu reconnect nhưng overlay không xuất hiện → vẫn là màn hình làng.
    [Fact]
    public void IsMainScreen_Should_Return_True_When_Reconnect_Overlay_Is_Absent()
    {
        using var template = CreatePatternTemplate();
        using var frame = CreateFrame(Width, Height, markerBgr: new byte[] { 255, 65, 255 });

        // Vùng reconnect chứa pattern khác để tránh trường hợp suy biến của CCoeffNormed.
        using var other = CreatePatternTemplate(seed: 91);
        Paste(
            frame,
            other,
            MainScreenGuard.ReconnectRegion.X + 4,
            MainScreenGuard.ReconnectRegion.Y + 4);

        var guard = new MainScreenGuard(new TemplateMatcher(), reconnectTemplate: template);

        guard.IsReconnecting(frame).Should().BeFalse();
        guard.IsMainScreen(frame).Should().BeTrue();
    }

    // Không có ảnh mẫu → không bao giờ báo reconnect (tránh chặn bot khi thiếu asset).
    [Fact]
    public void IsReconnecting_Should_Return_False_Without_Template()
    {
        using var frame = CreateFrame(Width, Height, markerBgr: new byte[] { 255, 65, 255 });

        _guard.ReconnectDetectionEnabled.Should().BeFalse();
        _guard.IsReconnecting(frame).Should().BeFalse();
    }

    // Cảnh báo sớm khi giả lập không chạy đúng độ phân giải chuẩn.
    [Fact]
    public void IsExpectedFrameSize_Should_Validate_860x732()
    {
        using var correct = CreateFrame(Width, Height);
        using var wrong = CreateFrame(Width, Height - 1);
        using var empty = new Mat();

        MainScreenGuard.IsExpectedFrameSize(correct).Should().BeTrue();
        MainScreenGuard.IsExpectedFrameSize(wrong).Should().BeFalse();
        MainScreenGuard.IsExpectedFrameSize(empty).Should().BeFalse();
    }

    private static Mat CreateFrame(
        int width,
        int height,
        byte[]? markerBgr = null,
        int markerX = MarkerX,
        int markerY = MarkerY)
    {
        var pixels = new byte[width * height * 3];

        // Nền có gradient nhẹ để template matching không rơi vào trường hợp ảnh phẳng.
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = (byte)(20 + (i * 7) % 200);
        }

        if (markerBgr is not null && markerX >= 0 && markerY >= 0
            && markerX < width && markerY < height)
        {
            int offset = (markerY * width + markerX) * 3;
            pixels[offset] = markerBgr[0];
            pixels[offset + 1] = markerBgr[1];
            pixels[offset + 2] = markerBgr[2];
        }

        return new Mat(height, width, MatType.CV_8UC3, pixels);
    }

    private static Mat CreatePatternTemplate(int size = 8, int seed = 17)
    {
        var pixels = new byte[size * size * 3];
        for (int i = 0; i < size * size; i++)
        {
            byte v = (byte)((i * 37 + seed) % 255);
            pixels[i * 3] = v;
            pixels[i * 3 + 1] = (byte)(255 - v);
            pixels[i * 3 + 2] = (byte)((v * 3 + seed) % 255);
        }

        return new Mat(size, size, MatType.CV_8UC3, pixels);
    }

    private static void Paste(Mat frame, Mat patch, int x, int y)
    {
        using var roi = new Mat(frame, new Rect(x, y, patch.Width, patch.Height));
        patch.CopyTo(roi);
    }
}
