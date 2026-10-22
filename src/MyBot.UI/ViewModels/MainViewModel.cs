using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyBot.Core.Abstractions;
using MyBot.Core.AntiBan;
using MyBot.Core.Capture;
using MyBot.Core.Config;
using MyBot.Core.FSM;
using MyBot.Core.Input;
using MyBot.Game.CoC.Village;
using OpenCvSharp;

namespace MyBot.UI.ViewModels;

/// <summary>
/// Trạng thái của Hero: Gray (không mang/chưa mở), Green (sẵn sàng), Blue (đang hồi máu), Red (bị thương/chết).
/// </summary>
public enum HeroState
{
    Gray,
    Green,
    Blue,
    Red
}

/// <summary>
/// ViewModel chính của ứng dụng, điều phối trạng thái FSM (Start, Stop, Pause),
/// hiển thị tài nguyên, hero, timer và danh sách nhật ký trên giao diện.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly BotStateMachine _fsm;
    private readonly IBotLogger _logger;
    private readonly IConfigManager _configManager;
    private readonly IProcessRunner _processRunner;
    private readonly ITemplateMatcher _matcher;
    private readonly HumanDelay _delay;

    // T7.27: Đường dẫn ADB mặc định của MEmu (có thể chuyển sang ConfigManager sau).
    private readonly string _adbPath = @"F:\Microvirt\MEmu\adb.exe";

    [ObservableProperty]
    private string _statusText = "Idle";

    // T7.28: Trạng thái FSM hiện tại — mọi computed property bên dưới tự cập nhật theo.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStartEnabled))]
    [NotifyPropertyChangedFor(nameof(IsStopEnabled))]
    [NotifyPropertyChangedFor(nameof(IsPauseEnabled))]
    [NotifyPropertyChangedFor(nameof(IsResumeEnabled))]
    [NotifyPropertyChangedFor(nameof(StartButtonText))]
    private BotState _currentState = BotState.Idle;

    public bool IsStartEnabled => CurrentState == BotState.Idle;

    public bool IsStopEnabled => CurrentState != BotState.Idle && CurrentState != BotState.Stopping;

    public bool IsPauseEnabled => CurrentState == BotState.Searching;

    public bool IsResumeEnabled => CurrentState == BotState.Paused;

    public string StartButtonText => CurrentState == BotState.Starting ? "Starting..." : "START";

    [ObservableProperty]
    private ObservableCollection<string> _logMessages = new();

    // Tài nguyên & Thông số làng
    [ObservableProperty]
    private int _gold = 1234567;

    [ObservableProperty]
    private int _elixir = 850000;

    [ObservableProperty]
    private int _darkElixir = 12345;

    [ObservableProperty]
    private int _trophies = 3200;

    [ObservableProperty]
    private int _builders = 3;

    [ObservableProperty]
    private string _labTime = "1d 4h";

    [ObservableProperty]
    private string _petTime = "Finished";

    // Trạng thái 6 Heroes
    [ObservableProperty]
    private HeroState _kingState = HeroState.Green;

    [ObservableProperty]
    private HeroState _queenState = HeroState.Blue;

    [ObservableProperty]
    private HeroState _princeState = HeroState.Gray;

    [ObservableProperty]
    private HeroState _wardenState = HeroState.Gray;

    [ObservableProperty]
    private HeroState _championState = HeroState.Gray;

    [ObservableProperty]
    private HeroState _dukeState = HeroState.Gray;

    // Cấu hình Tab Village
    [ObservableProperty]
    private bool _enableCollector = true;

    [ObservableProperty]
    private bool _enableWallUpgrade = true;

    [ObservableProperty]
    private string _minGoldReserve = "100000";

    [ObservableProperty]
    private string _telegramToken = "";

    [ObservableProperty]
    private bool _clearObstacles = true;

    [ObservableProperty]
    private string _keepBuilders = "1";

    [ObservableProperty]
    private bool _requestCc = true;

    [ObservableProperty]
    private string _requestMsg = "any";

    [ObservableProperty]
    private ObservableCollection<string> _wallResourceOptions = new() { "Gold", "Elixir", "Both" };

    [ObservableProperty]
    private string _selectedWallResource = "Both";

    // Cấu hình Tab Attack
    [ObservableProperty]
    private bool _enableDeadbase = true;

    [ObservableProperty]
    private bool _enableLivebase = false;

    [ObservableProperty]
    private string _minGoldDB = "400000";

    [ObservableProperty]
    private string _minElixirDB = "400000";

    [ObservableProperty]
    private ObservableCollection<string> _algorithmOptions = new() { "Standard", "CSV Scripted" };

    [ObservableProperty]
    private string _selectedAlgorithm = "Standard";

    [ObservableProperty]
    private bool _requireHeroes = false;

    [ObservableProperty]
    private ObservableCollection<string> _strategyFiles = new() { "Barch_Standard.csv", "Goblin_Knife.csv", "Zap_Dragon.csv" };

    [ObservableProperty]
    private string _selectedStrategyFile = "Barch_Standard.csv";

    // Cấu hình Tab Bot
    [ObservableProperty]
    private string _activeLanguage = "English";

    [ObservableProperty]
    private int _deleteLogsDays = 7;

    [ObservableProperty]
    private string _emulatorType = "MEmu";

    [ObservableProperty]
    private int _adbPort = 21503;

    [ObservableProperty]
    private bool _enableDebugLog = false;

    // Tab Stats — Lịch sử trận đánh
    [ObservableProperty]
    private ObservableCollection<BattleRecord> _battleHistory = new();

    /// <summary>
    /// Khởi tạo ViewModel với máy trạng thái FSM và logger.
    /// </summary>
    /// <param name="fsm">Máy trạng thái hữu hạn của Bot.</param>
    /// <param name="logger">Logger ghi nhận sự kiện.</param>
    /// <param name="configManager">Bộ quản lý cấu hình (config.json).</param>
    /// <param name="processRunner">Bộ chạy tiến trình dùng cho ADB.</param>
    /// <param name="matcher">Bộ so khớp ảnh mẫu dùng cho harvest.</param>
    /// <param name="delay">Bộ sinh độ trễ mô phỏng hành vi người.</param>
    public MainViewModel(
        BotStateMachine fsm,
        IBotLogger logger,
        IConfigManager configManager,
        IProcessRunner processRunner,
        ITemplateMatcher matcher,
        HumanDelay delay)
    {
        _fsm = fsm ?? throw new ArgumentNullException(nameof(fsm));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
        _delay = delay ?? throw new ArgumentNullException(nameof(delay));

        CurrentState = _fsm.CurrentState;
        StatusText = $"Status : {_fsm.CurrentState}";

        // T7.25: Nạp cấu hình thật từ config.json thay vì Mock Data.
        LoadConfig();

        // Mock data cho Stats DataGrid
        BattleHistory = new ObservableCollection<BattleRecord>
        {
            new("14:01:22", "Deadbase", 412000, 389000, 12),
            new("14:05:47", "Deadbase", 520000, 415000, 8),
            new("14:10:03", "Livebase", 280000, 310000, -5),
            new("14:14:18", "Deadbase", 610000, 570000, 15),
            new("14:19:55", "Deadbase", 350000, 420000, 10),
        };

        _fsm.StateChanged += (s, newState) =>
        {
            void OnStateChanged()
            {
                CurrentState = newState;
                StatusText = $"Status : {newState}";
            }

            if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
            {
                _ = dispatcher.InvokeAsync(OnStateChanged);
            }
            else
            {
                OnStateChanged();
            }
        };
    }

    /// <summary>
    /// Bắt đầu bot: Idle -> Starting, sau 2 giây chuyển sang Searching.
    /// </summary>
    [RelayCommand]
    private async Task StartBot()
    {
        if (_fsm.CurrentState != BotState.Idle) return;

        _fsm.TransitionTo(BotState.Starting);
        AddLog("Bot Started");

        await Task.Delay(2000);

        if (_fsm.CurrentState == BotState.Starting)
        {
            _fsm.TransitionTo(BotState.Searching);
        }
    }

    /// <summary>
    /// Dừng bot: Starting/Error -> Idle ngay; Searching/Pausing/Paused -> Stopping, sau 1 giây về Idle.
    /// </summary>
    [RelayCommand]
    private async Task StopBot()
    {
        switch (_fsm.CurrentState)
        {
            case BotState.Starting:
            case BotState.Uninitialized:
            case BotState.Error:
                _fsm.TransitionTo(BotState.Idle);
                AddLog("Bot Stopped");
                break;

            case BotState.Searching:
            case BotState.Pausing:
            case BotState.Paused:
                _fsm.TransitionTo(BotState.Stopping);
                AddLog("Bot Stopping...");

                await Task.Delay(1000);

                if (_fsm.CurrentState == BotState.Stopping)
                {
                    _fsm.TransitionTo(BotState.Idle);
                    AddLog("Bot Stopped");
                }
                break;

            default:
                AddLog($"Không thể dừng bot khi đang ở trạng thái {_fsm.CurrentState}");
                break;
        }
    }

    /// <summary>
    /// Tạm dừng bot: Searching -> Pausing, sau 1 giây chuyển sang Paused.
    /// </summary>
    [RelayCommand]
    private async Task PauseBot()
    {
        if (_fsm.CurrentState != BotState.Searching) return;

        _fsm.TransitionTo(BotState.Pausing);
        AddLog("Bot Pausing...");

        await Task.Delay(1000);

        if (_fsm.CurrentState == BotState.Pausing)
        {
            _fsm.TransitionTo(BotState.Paused);
            AddLog("Bot Paused");
        }
    }

    /// <summary>
    /// Tiếp tục bot: Paused -> Resuming, sau 1 giây chuyển sang Searching.
    /// </summary>
    [RelayCommand]
    private async Task Resume()
    {
        if (_fsm.CurrentState != BotState.Paused) return;

        _fsm.TransitionTo(BotState.Resuming);
        AddLog("Bot Resuming...");

        await Task.Delay(1000);

        if (_fsm.CurrentState == BotState.Resuming)
        {
            _fsm.TransitionTo(BotState.Searching);
            AddLog("Bot Resumed");
        }
    }

    /// <summary>
    /// Bắn thông báo thử nghiệm qua Telegram.
    /// </summary>
    [RelayCommand]
    private void TestNotify()
    {
        AddLog("Telegram test notification triggered.");
    }

    /// <summary>
    /// Mở trình chỉnh sửa thứ tự thả quân.
    /// </summary>
    [RelayCommand]
    private void EditDropOrder()
    {
        AddLog($"Opening drop order editor for: {SelectedStrategyFile}");
    }

    /// <summary>
    /// Chụp thử một khung hình từ MEmu qua ADB, lưu thành screenshot.png và mở ảnh.
    /// </summary>
    [RelayCommand]
    private async Task TestCaptureAsync()
    {
        AddLog("Đang kết nối tới MEmu để chụp ảnh...");

        (bool Success, string Message, string? Path) result;
        try
        {
            result = await Task.Run(() =>
            {
                using var capture = new AdbScreencapCaptureBackend(_adbPath, _processRunner);

                var target = new EmulatorTarget(AdbSerial: $"127.0.0.1:{AdbPort}");
                if (!capture.Initialize(target))
                {
                    return (false, "LỖI: Không thể kết nối ADB tới MEmu. Hãy kiểm tra giả lập đã bật chưa.", (string?)null);
                }

                using var mat = capture.GrabFrame();
                if (mat.Empty())
                {
                    return (false, "LỖI: Chụp ảnh thất bại (Mat rỗng).", (string?)null);
                }

                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "screenshot.png");
                if (!Cv2.ImWrite(path, mat))
                {
                    return (false, $"LỖI: Không ghi được file ảnh tại {path}.", (string?)null);
                }

                return (true, $"Chụp thành công: {mat.Width}x{mat.Height}. Đã lưu tại {path}", (string?)path);
            });
        }
        catch (Exception ex)
        {
            AddLog($"LỖI: {ex.Message}");
            return;
        }

        AddLog(result.Message);
        if (!result.Success || result.Path is null) return;

        try
        {
            Process.Start(new ProcessStartInfo(result.Path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AddLog($"Không mở được ảnh: {ex.Message}");
        }
    }

    /// <summary>
    /// INT-1: Chụp màn hình MEmu, quét mỏ vàng theo template và click thu hoạch.
    /// </summary>
    [RelayCommand]
    private async Task TestHarvestAsync()
    {
        AddLog("Bắt đầu quét mỏ vàng...");

        string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gold_mine.png");
        if (!File.Exists(templatePath))
        {
            AddLog("LỖI: Không tìm thấy file gold_mine.png. Hãy chụp và lưu vào thư mục bin.");
            return;
        }

        try
        {
            await Task.Run(async () =>
            {
                using var capture = new AdbScreencapCaptureBackend(_adbPath, _processRunner);
                var target = new EmulatorTarget(AdbSerial: $"127.0.0.1:{AdbPort}");

                if (!capture.Initialize(target))
                {
                    AddLog("LỖI: Không thể kết nối ADB tới MEmu. Hãy kiểm tra giả lập đã bật chưa.");
                    return;
                }

                using var frame = capture.GrabFrame();
                if (frame.Empty())
                {
                    AddLog("LỖI: Chụp ảnh thất bại (Mat rỗng).");
                    return;
                }

                using var templateMat = Cv2.ImRead(templatePath);
                if (templateMat.Empty())
                {
                    AddLog("LỖI: Không đọc được file gold_mine.png.");
                    return;
                }

                using var input = new AdbInputBackend(_adbPath, _processRunner);
                if (!input.Initialize(target))
                {
                    AddLog("LỖI: Không thể khởi tạo ADB Input.");
                    return;
                }

                var harvester = new CollectorHarvester(_matcher, input, _delay);
                int count = await harvester.HarvestAllAsync(frame, templateMat);
                AddLog($"Đã thu hoạch xong {count} mỏ vàng.");
            });
        }
        catch (Exception ex)
        {
            AddLog($"LỖI: {ex.Message}");
        }
    }

    /// <summary>
    /// Nạp cấu hình từ file config.json vào các trường của Tab Bot.
    /// </summary>
    [RelayCommand]
    private void LoadConfig()
    {
        try
        {
            var config = _configManager.Load();

            ActiveLanguage = config.ActiveLanguage;
            DeleteLogsDays = config.DeleteLogsDays;
            EmulatorType = config.EmulatorType;
            AdbPort = config.AdbPort;
            EnableDebugLog = config.EnableDebugLog;

            AddLog("Config loaded from config.json");
        }
        catch (Exception ex)
        {
            AddLog($"Failed to load config: {ex.Message}");
        }
    }

    /// <summary>
    /// Lưu các trường đang bind trên Tab Bot xuống file config.json.
    /// </summary>
    [RelayCommand]
    private void SaveConfig()
    {
        try
        {
            // Nạp lại trước để giữ nguyên các trường không bind trên UI.
            var config = _configManager.Load();

            config.ActiveLanguage = ActiveLanguage;
            config.DeleteLogsDays = DeleteLogsDays;
            config.EmulatorType = EmulatorType;
            config.AdbPort = AdbPort;
            config.EnableDebugLog = EnableDebugLog;

            // Đồng bộ khối cấu hình emulator lồng nhau (định dạng cũ) để tránh lệch dữ liệu.
            if (config.Emulator is not null)
            {
                config.Emulator.Type = EmulatorType;
                config.Emulator.AdbPort = AdbPort;
            }

            _configManager.Save(config);

            AddLog("Config saved to config.json");
        }
        catch (Exception ex)
        {
            AddLog($"Failed to save config: {ex.Message}");
        }
    }

    /// <summary>
    /// Thêm thông điệp nhật ký vào đầu danh sách (mới nhất hiển thị trên cùng).
    /// </summary>
    /// <param name="msg">Nội dung thông điệp.</param>
    public void AddLog(string msg)
    {
        string formatted = $"[{DateTime.Now:HH:mm:ss}] {msg}";
        _logger.Info(msg);

        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(() => LogMessages.Insert(0, formatted));
        }
        else
        {
            LogMessages.Insert(0, formatted);
        }
    }
}
