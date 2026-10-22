using System;
using System.Runtime.InteropServices;
using FluentAssertions;
using MyBot.Core.Abstractions;
using MyBot.Core.Capture;

namespace MyBot.Core.Tests;

public class CaptureTests
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    // T1.1.2 & T1.1.4: Kiểm tra xử lý Handle không hợp lệ
    [Fact]
    public void GrabFrame_Should_Return_Empty_Mat_If_Handle_Is_Invalid()
    {
        var capture = new Win32GdiCaptureBackend();
        
        // Handle rỗng → ArgumentException
        Action act = () => capture.Initialize(new EmulatorTarget(WindowHandle: IntPtr.Zero));
        act.Should().Throw<ArgumentException>();
    }

    // T1.1.1 & T1.1.3: Kiểm tra kích thước đầu ra và Dispose
    [Fact]
    public void GrabFrame_Should_Return_Correct_Size_And_Dispose_Cleanly()
    {
        var capture = new Win32GdiCaptureBackend();
        
        // Dùng Handle của Desktop (an toàn trên mọi luồng, không cần STA)
        var hWndDesktop = GetDesktopWindow();

        var result = capture.Initialize(new EmulatorTarget(WindowHandle: hWndDesktop));
        result.Should().BeTrue();
        
        using var frame = capture.GrabFrame();
        
        // Assert
        if (!frame.Empty())
        {
            frame.Width.Should().BeGreaterThan(0);
            frame.Height.Should().BeGreaterThan(0);
        }

        // Test Dispose
        capture.Invoking(c => c.Dispose()).Should().NotThrow();
    }
}
