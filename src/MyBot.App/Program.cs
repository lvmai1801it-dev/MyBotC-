using System;
using System.IO;
using MyBot.Core.Abstractions;
using MyBot.Core.Capture;
using MyBot.Core.Input;
using OpenCvSharp;

namespace MyBot.App;

public class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        // 1. Tạo một thư mục chứa 5 ảnh mẫu (giả lập recording)
        string mockDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mock_recording");
        Directory.CreateDirectory(mockDir);
        for (int i = 1; i <= 5; i++)
        {
            using var mat = new Mat(732, 860, MatType.CV_8UC3, new Scalar(50, 50, 50 + i * 20)); // Màu xám tối dần
            Cv2.ImWrite(Path.Combine(mockDir, $"frame_{i:D3}.png"), mat);
        }

        Console.WriteLine("=== TEST MOCK LAYER (T1.3 & T1.6) ===");
        Console.WriteLine("Giả lập đang chạy 5 frames recording...\n");

        // 2. Khởi tạo Mock Capture và Mock Input với EmulatorTarget
        var mockTarget = new EmulatorTarget(MockRecordingPath: mockDir);

        using var capture = new MockReplayCaptureBackend();
        capture.Initialize(mockTarget);
        capture.SetLoop(false);

        var input = new MockInputBackend();
        input.Initialize(mockTarget);

        // 3. Chạy vòng lặp giả lập
        int frameCount = 0;
        while (true)
        {
            using var frame = capture.GrabFrame();
            if (frame.Empty()) break;

            frameCount++;
            Console.WriteLine($"[Frame {frameCount}] Chụp được {frame.Width}x{frame.Height}");

            // Giả lập bot thả 1 lính Barbarian vào mỗi frame
            input.Tap(100 + frameCount * 10, 200);
        }

        Console.WriteLine($"\nĐã chạy xong {frameCount} frames.");

        // 4. Kiểm tra xem Input có ghi nhận đủ không
        Console.WriteLine($"\n--- LỊCH SỬ CLICK (Mock Input) ---");
        Console.WriteLine($"Tổng số click: {input.RecordedActions.Count}");
        foreach (var action in input.RecordedActions)
        {
            Console.WriteLine($"- {action.Type} tại tọa độ ({action.X1}, {action.Y1})");
        }

        // 5. Dọn dẹp
        Directory.Delete(mockDir, true);

        if (Environment.UserInteractive && !Console.IsInputRedirected && args.Length == 0)
        {
            Console.WriteLine("\nNhấn phím bất kỳ để thoát...");
            try { Console.ReadKey(); } catch { }
        }
    }
}
