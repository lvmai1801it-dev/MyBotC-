using System;
using System.Collections.Generic;
using MyBot.Core.Abstractions;

namespace MyBot.Core.Input;

/// <summary>
/// Backend input giả lập dùng cho unit test: không tương tác với OS thật mà chỉ
/// ghi lại danh sách hành động (tap/swipe) để kiểm tra.
/// </summary>
public class MockInputBackend : IInputBackend
{
    /// <summary>Một hành động input đã được ghi lại.</summary>
    /// <param name="Type">Loại hành động ("Tap" hoặc "Swipe").</param>
    /// <param name="X1">Toạ độ X điểm bắt đầu.</param>
    /// <param name="Y1">Toạ độ Y điểm bắt đầu.</param>
    /// <param name="X2">Toạ độ X điểm kết thúc (chỉ dùng cho Swipe).</param>
    /// <param name="Y2">Toạ độ Y điểm kết thúc (chỉ dùng cho Swipe).</param>
    /// <param name="DurationMs">Thời gian vuốt tính bằng ms (chỉ dùng cho Swipe).</param>
    /// <param name="Timestamp">Thời điểm ghi nhận hành động.</param>
    public record RecordedAction(string Type, int X1, int Y1, int X2 = 0, int Y2 = 0, int DurationMs = 0, DateTime Timestamp = default);

    /// <summary>Danh sách các hành động đã được ghi lại theo thứ tự.</summary>
    public List<RecordedAction> RecordedActions { get; } = new();

    /// <summary>Mock luôn khởi tạo thành công.</summary>
    /// <param name="target">Thông tin thiết bị đích (bị bỏ qua).</param>
    /// <returns>Luôn trả về True.</returns>
    public bool Initialize(EmulatorTarget target)
    {
        return true; // Mock luôn sẵn sàng
    }

    /// <summary>Ghi lại một hành động chạm.</summary>
    /// <param name="x">Toạ độ X.</param>
    /// <param name="y">Toạ độ Y.</param>
    public void Tap(int x, int y)
    {
        RecordedActions.Add(new RecordedAction("Tap", x, y, Timestamp: DateTime.UtcNow));
    }

    /// <summary>Ghi lại một hành động vuốt.</summary>
    /// <param name="x1">Toạ độ X điểm bắt đầu.</param>
    /// <param name="y1">Toạ độ Y điểm bắt đầu.</param>
    /// <param name="x2">Toạ độ X điểm kết thúc.</param>
    /// <param name="y2">Toạ độ Y điểm kết thúc.</param>
    /// <param name="durationMs">Thời gian vuốt (ms).</param>
    public void Swipe(int x1, int y1, int x2, int y2, int durationMs)
    {
        RecordedActions.Add(new RecordedAction("Swipe", x1, y1, x2, y2, durationMs, DateTime.UtcNow));
    }

    /// <summary>Xoá toàn bộ danh sách hành động đã ghi.</summary>
    public void Clear()
    {
        RecordedActions.Clear();
    }

    /// <summary>Không có tài nguyên nào cần giải phóng.</summary>
    public void Dispose() { }
}
