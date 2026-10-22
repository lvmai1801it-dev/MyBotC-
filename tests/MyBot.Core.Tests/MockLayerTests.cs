using System.IO;
using FluentAssertions;
using MyBot.Core.Abstractions;
using MyBot.Core.Capture;
using MyBot.Core.Input;
using OpenCvSharp;

namespace MyBot.Core.Tests;

public class MockLayerTests
{
    // === Test cho T1.6: Mock Input Backend ===
    [Fact]
    public void MockInput_Should_Record_Actions_Correctly()
    {
        var mock = new MockInputBackend();
        mock.Initialize(new EmulatorTarget());

        mock.Tap(100, 200);
        mock.Swipe(10, 20, 30, 40, 500);

        mock.RecordedActions.Should().HaveCount(2);

        mock.RecordedActions[0].Type.Should().Be("Tap");
        mock.RecordedActions[0].X1.Should().Be(100);
        mock.RecordedActions[0].Y1.Should().Be(200);

        mock.RecordedActions[1].Type.Should().Be("Swipe");
        mock.RecordedActions[1].DurationMs.Should().Be(500);

        mock.Clear();
        mock.RecordedActions.Should().BeEmpty();
    }

    // === Test cho T1.3: Mock Replay Capture Backend ===
    [Fact]
    public void MockCapture_Should_Read_Images_From_Directory()
    {
        // 1. Tạo thư mục tạm và 3 file ảnh trắng giả lập
        string tempDir = Path.Combine(Path.GetTempPath(), "MyBotMockTest");
        Directory.CreateDirectory(tempDir);

        for (int i = 1; i <= 3; i++)
        {
            using var mat = new Mat(100, 100, MatType.CV_8UC3, Scalar.White);
            Cv2.ImWrite(Path.Combine(tempDir, $"frame_{i:D3}.png"), mat);
        }

        // 2. Khởi tạo Mock với EmulatorTarget
        using var mock = new MockReplayCaptureBackend();
        bool init = mock.Initialize(new EmulatorTarget(MockRecordingPath: tempDir));

        // 3. Test đọc ảnh
        init.Should().BeTrue();

        using var f1 = mock.GrabFrame();
        f1.Width.Should().Be(100);
        f1.Height.Should().Be(100);

        using var f2 = mock.GrabFrame();
        using var f3 = mock.GrabFrame();

        // Đọc lần 4 (hết ảnh) -> Trả về Mat rỗng
        using var f4 = mock.GrabFrame();
        f4.Empty().Should().BeTrue();

        // 4. Dọn dẹp
        Directory.Delete(tempDir, true);
    }
}
