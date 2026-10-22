namespace MyBot.Core.ActionContract;

/// <summary>
/// Chế độ thực thi hành động: <see cref="Live"/> chạy thật có kiểm tra thị giác,
/// <see cref="Replay"/> chỉ phát lại input mà bỏ qua kiểm tra hình ảnh.
/// </summary>
public enum ExecutionMode
{
    /// <summary>Chạy thật trên thiết bị/giả lập, có xác minh thị giác sau mỗi hành động.</summary>
    Live,

    /// <summary>Phát lại kịch bản đã ghi, chỉ gửi input và bỏ qua xác minh thị giác.</summary>
    Replay
}
