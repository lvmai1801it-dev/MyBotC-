namespace MyBot.Core.FSM;

public enum BotState
{
    Uninitialized,
    Idle,
    Starting,
    Searching, // Bao gồm tìm trận và đánh trận
    Pausing,   // Đang chờ thả xong wave lính để pause an toàn
    Paused,
    Resuming,
    Stopping,  // Đang dọn dẹp tài nguyên để tắt
    Recovering,// Đang xử lý popup hoặc lỗi mạng
    Error
}
