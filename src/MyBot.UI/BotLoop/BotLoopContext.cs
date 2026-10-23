using System;
using System.Threading;
using System.Threading.Tasks;
using MyBot.Core.Abstractions;
using MyBot.Core.FSM.Maintenance;
using MyBot.Game.CoC.Village;
using OpenCvSharp;

// Namespace "BotRuntime" thay cho "BotLoop" để tránh trùng tên với class BotLoop
// (namespace MyBot.UI.BotLoop sẽ che kiểu BotLoop khi dùng từ MyBot.UI.ViewModels).
namespace MyBot.UI.BotRuntime;

/// <summary>
/// Trạng thái và phụ thuộc của một phiên chạy <see cref="BotLoop"/>.
///
/// parity: các biến toàn cục của <c>runBot</c> — <c>$g_bRunState</c>, <c>$g_bRestart</c>,
/// <c>$g_bRestartRunBot</c>, <c>$g_bBotPaused</c>, <c>$g_bFirstStart</c> @ MyBot.run.au3:930.
/// </summary>
public sealed class BotLoopContext
{
    /// <summary>Nguồn thời gian cho mọi delay và hẹn giờ bảo trì.</summary>
    public required ITimeProvider Time { get; init; }

    /// <summary>Logger của bot.</summary>
    public required IBotLogger Logger { get; init; }

    /// <summary>Bộ hẹn giờ bảo trì dùng chung cả phiên.</summary>
    public required MaintenanceScheduler Scheduler { get; init; }

    /// <summary>Bộ khớp template dùng cho thu hoạch.</summary>
    public required ITemplateMatcher Matcher { get; init; }

    /// <summary>Backend tap; null khi chưa khởi tạo được input.</summary>
    public IInputBackend? Input { get; init; }

    /// <summary>Chụp một khung hình mới; bot sở hữu và dispose <see cref="Mat"/> trả về.</summary>
    public required Func<CancellationToken, Task<Mat>> GrabFrameAsync { get; init; }

    /// <summary>Kiểm tra khung hình có phải màn hình làng chính (<c>checkMainScreen</c>).</summary>
    public required Func<Mat, bool> IsMainScreen { get; init; }

    /// <summary>Kiểm tra Builder Base; null để bỏ qua.</summary>
    public Func<Mat, bool>? IsBuilderBase { get; init; }

    /// <summary>
    /// Phục hồi khi không thấy màn hình làng chính. Trả false để dừng vòng bot.
    /// parity: <c>checkMainScreen</c> + <c>MainScreenRecovery.LocateAsync</c>.
    /// </summary>
    public Func<Mat, CancellationToken, Task<bool>>? EnsureMainScreenAsync { get; init; }

    /// <summary>
    /// Việc chạy một lần khi vừa nhận diện màn hình làng chính (zoom, Town Hall, OCR).
    /// Trả false để dừng vòng bot.
    /// </summary>
    public Func<CancellationToken, Task<bool>>? OnMainScreenSeenAsync { get; init; }

    /// <summary>Đọc tài nguyên (OCR) từ khung hình; được gọi theo nhịp <see cref="ResourceReadInterval"/>.</summary>
    public Func<Mat, CancellationToken, Task>? UpdateResourcesAsync { get; init; }

    /// <summary>Kiểm tra FSM đang tạm dừng; null nếu bot không hỗ trợ pause.</summary>
    public Func<bool>? IsPaused { get; init; }

    /// <summary>Bộ thu hoạch mỏ; null khi chưa nạp được template.</summary>
    public CollectorHarvester? Collector { get; init; }

    /// <summary>Khoảng cách tối thiểu giữa hai lần đọc tài nguyên.</summary>
    public TimeSpan ResourceReadInterval { get; init; } = TimeSpan.FromSeconds(2);

    /// <summary>Thời gian chờ mỗi vòng khi bot đang tạm dừng.</summary>
    public int PauseIntervalMs { get; init; } = 2000;

    /// <summary>Thời gian idle ngẫu nhiên tối thiểu (giây); 0 = tắt.</summary>
    public int AddIdleDelayMinSeconds { get; init; }

    /// <summary>Thời gian idle ngẫu nhiên tối đa (giây); 0 = tắt.</summary>
    public int AddIdleDelayMaxSeconds { get; init; }

    /// <summary>Seed cho xáo trộn idle; null = ngẫu nhiên.</summary>
    public int? Seed { get; init; }

    /// <summary><c>$g_bRunState</c> — bot còn được phép chạy.</summary>
    public bool RunState { get; set; } = true;

    /// <summary><c>$g_bRestart</c> — yêu cầu khởi động lại vòng hiện tại.</summary>
    public bool Restart { get; set; }

    /// <summary><c>$g_bRestartRunBot</c> — yêu cầu thoát hẳn <c>runBot</c>.</summary>
    public bool RestartRunBot { get; set; }

    /// <summary><c>$g_bBotPaused</c> — bot đang tạm dừng.</summary>
    public bool Paused { get; set; }

    /// <summary><c>$g_bFirstStart</c> — vòng chạy đầu tiên sau khi khởi động.</summary>
    public bool FirstStart { get; set; } = true;

    /// <summary>Có bật tìm trận và đánh không (<c>IsSearchAttackEnabled</c>).</summary>
    public bool IsSearchAttackEnabled { get; set; } = true;

    /// <summary>
    /// Điều kiện tiếp tục của <c>__RunBotCanContinue</c>:
    /// <c>$g_bRunState And Not $g_bRestart And Not $g_bRestartRunBot</c> @ MyBot.run.au3:832.
    /// </summary>
    public bool CanContinue => RunState && !Restart && !RestartRunBot;
}
