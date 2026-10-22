using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
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
using MyBot.Core.Utils;
using MyBot.Game.CoC.State;
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
    private readonly IOcrEngine _ocrEngine;
    private readonly IEmulatorDriver _emulatorDriver;
    private readonly MainScreenGuard _mainScreenGuard = new();

    // CORE-LOOP-FINAL: vòng lặp bot chạy nền và khóa profile dùng chung giữa các tiến trình.
    private CancellationTokenSource? _botCts;
    private ProfileLock? _profileLock;

    // Chu kỳ chụp/kiểm tra màn hình của vòng lặp bot.
    private const int PollIntervalMs = 2000;
    private const string ProfileName = "Default";

    // Pixel mốc dùng bởi MainScreenGuard (hệ toạ độ 860x732).
    private const int MarkerPixelX = 840;
    private const int MarkerPixelY = 53;

    // Toạ độ đã hiệu chỉnh theo ảnh chụp MEmu 860x732.
    private static readonly OpenCvSharp.Rect GoldRect = new(643, 16, 200, 32);
    private static readonly OpenCvSharp.Rect ElixirRect = new(643, 53, 200, 32);
    private static readonly OpenCvSharp.Rect DarkElixirRect = new(643, 87, 205, 32);
    private static readonly OpenCvSharp.Rect GemsRect = new(752, 127, 62, 27);
    private static readonly OpenCvSharp.Rect BuilderAvailableRect = new(754, 599, 11, 24);
    private static readonly OpenCvSharp.Rect BuilderTotalRect = new(775, 599, 17, 24);

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

    // INT-3: Tài nguyên & thợ xây đọc bằng OCR.
    [ObservableProperty]
    private int _gems = 0;

    [ObservableProperty]
    private string _builderText = "0/5";

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
    /// <param name="ocrEngine">Bộ đọc chữ số từ ảnh (OCR).</param>
    /// <param name="emulatorDriver">Driver tìm cửa sổ giả lập và tạo capture/input backend.</param>
    public MainViewModel(
        BotStateMachine fsm,
        IBotLogger logger,
        IConfigManager configManager,
        IProcessRunner processRunner,
        ITemplateMatcher matcher,
        HumanDelay delay,
        IOcrEngine ocrEngine,
        IEmulatorDriver emulatorDriver)
    {
        _fsm = fsm ?? throw new ArgumentNullException(nameof(fsm));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
        _delay = delay ?? throw new ArgumentNullException(nameof(delay));
        _ocrEngine = ocrEngine ?? throw new ArgumentNullException(nameof(ocrEngine));
        _emulatorDriver = emulatorDriver ?? throw new ArgumentNullException(nameof(emulatorDriver));

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
    /// Bắt đầu bot: khóa profile, chuyển Idle -> Starting và chạy vòng lặp nền.
    /// </summary>
    [RelayCommand]
    private void StartBot()
    {
        if (_fsm.CurrentState != BotState.Idle)
        {
            return;
        }

        if (!ProfileLock.TryAcquire(ProfileName, out ProfileLock? profileLock))
        {
            AddLog("LỖI: Profile đang bị chạy bởi tiến trình khác!");
            return;
        }

        _profileLock = profileLock;

        var cts = new CancellationTokenSource();
        _botCts = cts;

        _fsm.TransitionTo(BotState.Starting);
        AddLog("Bot Started");

        _ = Task.Run(() => BotLoopAsync(cts), CancellationToken.None);
    }

    /// <summary>
    /// Yêu cầu dừng bot. FSM được đưa về Idle trong finally của <see cref="BotLoopAsync"/>.
    /// </summary>
    [RelayCommand]
    private void StopBot()
    {
        if (_botCts is null)
        {
            AddLog($"Không thể dừng bot khi đang ở trạng thái {_fsm.CurrentState}");
            return;
        }

        AddLog("Bot Stopping...");
        _botCts.Cancel();
    }

    /// <summary>
    /// Vòng lặp chính của bot: tìm cửa sổ giả lập, chuẩn hóa kích thước, chụp màn hình,
    /// kiểm tra màn hình làng chính và cập nhật tài nguyên cho tới khi bị hủy.
    /// </summary>
    /// <param name="cts">Nguồn hủy của riêng vòng lặp này.</param>
    private async Task BotLoopAsync(CancellationTokenSource cts)
    {
        CancellationToken token = cts.Token;
        ProfileLock? ownedLock = _profileLock;
        ICaptureBackend? capture = null;

        AddLog("[Loop] Khởi động Bot...");

        try
        {
            // Ưu tiên ADB vì chụp đúng độ phân giải thiết bị, không bị ảnh đen/viền cửa sổ.
            AddLog($"[Loop] Kết nối ADB tới 127.0.0.1:{AdbPort}...");
            ICaptureBackend captureBackend = new AdbScreencapCaptureBackend(_adbPath, _processRunner);
            capture = captureBackend;

            var adbTarget = new EmulatorTarget(AdbSerial: $"127.0.0.1:{AdbPort}");

            if (!captureBackend.Initialize(adbTarget))
            {
                AddLog("LỖI: ADB không kết nối được — thử chụp bằng cửa sổ (GDI)...");
                captureBackend.Dispose();
                capture = null;

                EmulatorInstance? instance = _emulatorDriver.DetectInstances().FirstOrDefault();
                if (instance is null)
                {
                    throw new InvalidOperationException(
                        "Không kết nối được ADB và không tìm thấy cửa sổ giả lập.");
                }

                AddLog($"Đã tìm thấy: {instance.Name} — ép cửa sổ về 860x732...");

                // Ép cửa sổ render về đúng khung 860x732 mà toạ độ OCR đã hiệu chỉnh.
                Win32WindowNormalizer.EnsureWindowSize(instance.WindowHandle);
                await Task.Delay(500, token);

                captureBackend = _emulatorDriver.CreateCaptureBackend();
                capture = captureBackend;

                var windowTarget = new EmulatorTarget(
                    WindowHandle: instance.WindowHandle,
                    AdbSerial: $"127.0.0.1:{AdbPort}");

                if (!captureBackend.Initialize(windowTarget))
                {
                    throw new InvalidOperationException(
                        "Không khởi tạo được bộ chụp màn hình (ADB và GDI đều thất bại).");
                }
            }

            AddLog("Đã khởi tạo bộ chụp màn hình.");
            _fsm.TransitionTo(BotState.Searching);
            AddLog($"Bot đang chạy (chu kỳ {PollIntervalMs}ms).");

            bool? lastMainScreen = null;

            while (!token.IsCancellationRequested)
            {
                // PauseBot đã chuyển FSM sang Pausing/Paused; vòng lặp chỉ chờ.
                if (_fsm.CurrentState is BotState.Pausing or BotState.Paused or BotState.Resuming)
                {
                    await Task.Delay(PollIntervalMs, token);
                    continue;
                }

                if (_fsm.CurrentState != BotState.Searching)
                {
                    await Task.Delay(200, token);
                    continue;
                }

                using Mat frame = captureBackend.GrabFrame();
                if (frame.Empty())
                {
                    AddLog("LỖI: Không chụp được khung hình.");
                    await Task.Delay(PollIntervalMs, token);
                    continue;
                }

                bool isMainScreen = _mainScreenGuard.IsMainScreen(frame);

                if (!isMainScreen)
                {
                    // FIX-GUARD: log màu thực tế + lưu ảnh crosshair để biết sai màu hay lệch toạ độ.
                    LogMarkerPixel(frame);
                    SaveMainScreenDebugImage(frame);

                    if (lastMainScreen != false)
                    {
                        AddLog("Không phải màn hình làng chính — đang chờ...");
                        lastMainScreen = false;
                    }

                    await Task.Delay(PollIntervalMs, token);
                    continue;
                }

                if (lastMainScreen != true)
                {
                    AddLog("Đã nhận diện màn hình làng chính.");
                    lastMainScreen = true;
                }

                UpdateResourcesFromFrame(frame);

                await Task.Delay(PollIntervalMs, token);
            }
        }
        catch (OperationCanceledException)
        {
            // Dừng theo yêu cầu của người dùng.
        }
        catch (Exception ex)
        {
            // Ghi đầy đủ stack trace để bắt bệnh khi chạy thật.
            AddLog($"LỖI BOT: {ex}");
            TryTransitionTo(BotState.Error);
        }
        finally
        {
            capture?.Dispose();
            FinishBotLoop();
            ReleaseProfileLock(ownedLock);

            // Chỉ dọn CTS của chính vòng lặp này (tránh xoá CTS của lần Start kế tiếp).
            if (ReferenceEquals(_botCts, cts))
            {
                _botCts = null;
                cts.Dispose();
            }

            AddLog("[Loop] Đã kết thúc.");
        }
    }

    /// <summary>
    /// In màu thực tế tại pixel mốc để chẩn đoán vì sao <see cref="MainScreenGuard"/> trả về false.
    /// </summary>
    /// <param name="frame">Khung hình vừa chụp.</param>
    private void LogMarkerPixel(Mat frame)
    {
        if (_mainScreenGuard.TryGetMarkerColor(frame, out Vec3b pixel))
        {
            AddLog(
                $"[Loop] Pixel ({MarkerPixelX},{MarkerPixelY}) - B:{pixel.Item0}, G:{pixel.Item1}, R:{pixel.Item2} " +
                "| target B:255 G:65 R:255 ±20");
        }
        else
        {
            AddLog(
                $"[Loop] Không đọc được pixel mốc: frame {frame.Width}x{frame.Height}, " +
                $"channels={frame.Channels()} (cần >= 850x60 và 3 kênh màu).");
        }
    }

    /// <summary>
    /// Vẽ chữ thập đỏ tại pixel mốc rồi lưu ra <c>loop_debug_mainscreen.png</c>.
    /// </summary>
    /// <param name="frame">Khung hình vừa chụp.</param>
    private static void SaveMainScreenDebugImage(Mat frame)
    {
        try
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "loop_debug_mainscreen.png");

            using Mat debug = frame.Clone();
            var red = new Scalar(0, 0, 255);
            var center = new OpenCvSharp.Point(MarkerPixelX, MarkerPixelY);

            Cv2.Line(debug, new OpenCvSharp.Point(MarkerPixelX - 15, MarkerPixelY),
                new OpenCvSharp.Point(MarkerPixelX + 15, MarkerPixelY), red, 1);
            Cv2.Line(debug, new OpenCvSharp.Point(MarkerPixelX, MarkerPixelY - 15),
                new OpenCvSharp.Point(MarkerPixelX, MarkerPixelY + 15), red, 1);
            Cv2.Circle(debug, center, 5, red, 1);
            Cv2.PutText(
                debug,
                $"({MarkerPixelX},{MarkerPixelY})",
                new OpenCvSharp.Point(System.Math.Max(0, MarkerPixelX - 110), System.Math.Max(12, MarkerPixelY - 8)),
                HersheyFonts.HersheySimplex,
                0.5,
                red,
                1);

            Cv2.ImWrite(path, debug);
        }
        catch (Exception ex)
        {
            // Không để lỗi ghi ảnh debug làm chết vòng lặp.
            Console.Error.WriteLine($"[Loop] Không ghi được loop_debug_mainscreen.png: {ex.Message}");
        }
    }

    /// <summary>
    /// Cập nhật Gold/Elixir/DE/Gems/Builders từ một khung hình bằng OCR.
    /// </summary>
    /// <param name="frame">Khung hình màn hình làng chính.</param>
    private void UpdateResourcesFromFrame(Mat frame)
    {
        int? gold = TryReadNumber(frame, GoldRect);
        int? elixir = TryReadNumber(frame, ElixirRect);
        int? darkElixir = TryReadNumber(frame, DarkElixirRect);
        int? gems = TryReadNumber(frame, GemsRect);

        int? builderAvailable = TryReadNumber(frame, BuilderAvailableRect, "coc-Builders");
        int? builderTotal = TryReadNumber(frame, BuilderTotalRect, "coc-Builders");

        string? builderText = builderAvailable is not null && builderTotal is not null
            ? $"{builderAvailable.Value}/{builderTotal.Value}"
            : builderAvailable?.ToString();

        void Apply()
        {
            if (gold is not null) Gold = gold.Value;
            if (elixir is not null) Elixir = elixir.Value;
            if (darkElixir is not null) DarkElixir = darkElixir.Value;
            if (gems is not null) Gems = gems.Value;
            if (builderText is not null) BuilderText = builderText;
        }

        // Thuộc tính bind với WPF phải được cập nhật trên UI thread.
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(Apply);
        }
        else
        {
            Apply();
        }
    }

    /// <summary>
    /// Cắt một vùng ảnh, chạy OCR và trả về số nguyên đầu tiên đọc được.
    /// </summary>
    /// <param name="frame">Ảnh chụp màn hình gốc.</param>
    /// <param name="rect">Vùng cần đọc.</param>
    /// <param name="model">Model OCR cần dùng; null = model mặc định.</param>
    /// <returns>Giá trị đọc được, hoặc null nếu vùng ngoài khung/OCR thất bại.</returns>
    private int? TryReadNumber(Mat frame, OpenCvSharp.Rect rect, string? model = null)
    {
        if (rect.X < 0 || rect.Y < 0 || rect.Right > frame.Width || rect.Bottom > frame.Height)
        {
            return null;
        }

        try
        {
            using var crop = new Mat(frame, rect);
            string rawText = model is null ? _ocrEngine.Recognize(crop) : _ocrEngine.Recognize(crop, model);
            string cleanText = Regex.Replace(rawText, @"[^\d]", "");
            return int.TryParse(cleanText, out int value) ? value : null;
        }
        catch (Exception ex)
        {
            _logger.Warning($"[OCR] Lỗi đọc vùng {rect}: {ex.Message}");
            return null;
        }
    }

    /// <summary>Chuyển trạng thái FSM, bỏ qua nếu phép chuyển không hợp lệ.</summary>
    private void TryTransitionTo(BotState newState)
    {
        try
        {
            _fsm.TransitionTo(newState);
        }
        catch (InvalidOperationException ex)
        {
            AddLog($"Không chuyển được trạng thái: {ex.Message}");
        }
    }

    /// <summary>Đưa FSM về Idle theo đúng các phép chuyển hợp lệ khi vòng lặp kết thúc.</summary>
    private void FinishBotLoop()
    {
        BotState state = _fsm.CurrentState;
        if (state == BotState.Idle)
        {
            return;
        }

        switch (state)
        {
            case BotState.Searching:
            case BotState.Pausing:
            case BotState.Paused:
                TryTransitionTo(BotState.Stopping);
                break;

            case BotState.Resuming:
                TryTransitionTo(BotState.Searching);
                TryTransitionTo(BotState.Stopping);
                break;

            case BotState.Recovering:
                TryTransitionTo(BotState.Error);
                break;
        }

        TryTransitionTo(BotState.Idle);
        AddLog("Bot Stopped");
    }

    /// <summary>
    /// Nhả khóa profile trên đúng luồng đã lấy khóa (UI thread).
    /// </summary>
    /// <param name="profileLock">Khóa do vòng lặp này sở hữu.</param>
    private void ReleaseProfileLock(ProfileLock? profileLock)
    {
        if (profileLock is null)
        {
            return;
        }

        if (ReferenceEquals(_profileLock, profileLock))
        {
            _profileLock = null;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess() && !dispatcher.HasShutdownStarted)
        {
            try
            {
                dispatcher.Invoke(profileLock.Dispose);
                return;
            }
            catch (OperationCanceledException)
            {
                // Ứng dụng đang tắt; kernel dọn mutex khi tiến trình kết thúc.
            }
            catch (InvalidOperationException)
            {
                // Dispatcher đã ngừng hoạt động.
            }
        }

        profileLock.Dispose();
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

                string debugPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug_frame.png");
                Cv2.ImWrite(debugPath, frame);
                AddLog($"[DEBUG] Đã lưu ảnh chụp màn hình tại: {debugPath}");

                string templatePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "gold_mine.png");
                if (!File.Exists(templatePath))
                {
                    AddLog("[LỖI] KHÔNG TÌM THẤY FILE gold_mine.png trong thư mục bin!");
                    return;
                }

                using var templateMat = Cv2.ImRead(templatePath);
                if (templateMat.Empty())
                {
                    AddLog("[LỖI] File gold_mine.png bị hỏng hoặc không đọc được!");
                    return;
                }

                AddLog($"[DEBUG] Đã load ảnh mẫu: {templateMat.Width}x{templateMat.Height} pixels.");

                // FIX-INT2: log điểm khớp cao nhất kể cả khi dưới ngưỡng, để biết
                // ảnh mẫu kém hay ngưỡng còn quá cao.
                MatchResult? bestMatch = _matcher.Find(frame, templateMat, -1.0);
                if (bestMatch is not null)
                {
                    AddLog($"[DEBUG] Điểm khớp cao nhất: {bestMatch.Score:F3} tại ({bestMatch.X},{bestMatch.Y}).");
                }

                using var input = new AdbInputBackend(_adbPath, _processRunner);
                if (!input.Initialize(target))
                {
                    AddLog("LỖI: Không thể khởi tạo ADB Input.");
                    return;
                }

                var harvester = new CollectorHarvester(_matcher, input, _delay);
                int count = await harvester.HarvestAllAsync(frame, templateMat);
                AddLog($"[DEBUG] Tìm thấy {count} mỏ vàng.");

                foreach (MatchResult match in harvester.LastMatches)
                {
                    AddLog($"[DEBUG] Khớp tại ({match.X},{match.Y}) score={match.Score:F3}.");
                }

                string boxedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug_frame_with_box.png");
                using (var boxed = frame.Clone())
                {
                    foreach (MatchResult match in harvester.LastMatches)
                    {
                        var matchRect = new OpenCvSharp.Rect(match.X, match.Y, match.Width, match.Height);
                        Cv2.Rectangle(boxed, matchRect, new Scalar(0, 0, 255), 2);
                        Cv2.PutText(
                            boxed,
                            match.Score.ToString("F2"),
                            new OpenCvSharp.Point(match.X, Math.Max(12, match.Y - 4)),
                            HersheyFonts.HersheySimplex,
                            0.5,
                            new Scalar(0, 255, 255),
                            1);
                    }

                    Cv2.ImWrite(boxedPath, boxed);
                }

                AddLog($"[DEBUG] Đã lưu ảnh khoanh vùng khớp tại: {boxedPath}");
                AddLog($"Đã thu hoạch xong {count} mỏ vàng.");
            });
        }
        catch (Exception ex)
        {
            AddLog($"LỖI: {ex.Message}");
        }
    }

    /// <summary>
    /// INT-3: Chụp màn hình MEmu và đọc toàn bộ tài nguyên (Gold, Elixir, DE, Gems, Builders).
    /// </summary>
    [RelayCommand]
    private async Task ReadAllResourcesAsync()
    {
        AddLog("Đang đọc toàn bộ tài nguyên...");

        try
        {
            await Task.Run(() =>
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
                    AddLog("LỖI: Không thể chụp màn hình.");
                    return;
                }

                ReadResourceRegion(frame, GoldRect, "Gold", val => Gold = val);
                ReadResourceRegion(frame, ElixirRect, "Elixir", val => Elixir = val);
                ReadResourceRegion(frame, DarkElixirRect, "DarkElixir", val => DarkElixir = val);
                ReadResourceRegion(frame, GemsRect, "Gems", val => Gems = val);

                // Bộ đếm "1/4" góc dưới phải: model mặc định đọc nhầm dấu "/" nên tách 2 ô số.
                string builderAvailable = string.Empty;
                string builderTotal = string.Empty;
                ReadResourceRegion(frame, BuilderAvailableRect, "BuilderAvailable", null,
                    text => builderAvailable = text, model: "coc-Builders");
                ReadResourceRegion(frame, BuilderTotalRect, "BuilderTotal", null,
                    text => builderTotal = text, model: "coc-Builders");
                BuilderText = builderAvailable.Length > 0 && builderTotal.Length > 0
                    ? $"{builderAvailable}/{builderTotal}"
                    : builderAvailable;

                Cv2.Rectangle(frame, GoldRect, new Scalar(0, 255, 255), 2);       // Gold - vàng
                Cv2.Rectangle(frame, ElixirRect, new Scalar(255, 0, 255), 2);     // Elixir - hồng
                Cv2.Rectangle(frame, DarkElixirRect, new Scalar(128, 0, 128), 2); // DE - tím
                Cv2.Rectangle(frame, GemsRect, new Scalar(0, 255, 0), 2);         // Gems - xanh lá
                Cv2.Rectangle(frame, BuilderAvailableRect, new Scalar(255, 0, 0), 2);
                Cv2.Rectangle(frame, BuilderTotalRect, new Scalar(255, 0, 0), 2);

                string debugPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug_all_resources.png");
                Cv2.ImWrite(debugPath, frame);
                AddLog($"Đã đọc xong. Xem ảnh kiểm tra tại: {debugPath}");
            });
        }
        catch (Exception ex)
        {
            AddLog($"LỖI OCR: {ex.Message}");
        }
    }

    /// <summary>
    /// INT-3: Cắt một vùng tài nguyên, đọc OCR, làm sạch chuỗi rồi cập nhật UI.
    /// </summary>
    /// <param name="frame">Ảnh chụp màn hình gốc.</param>
    /// <param name="rect">Vùng cần đọc.</param>
    /// <param name="resourceName">Tên tài nguyên dùng cho log và tên file debug.</param>
    /// <param name="updateAction">Hàm cập nhật giá trị số.</param>
    /// <param name="updateTextAction">Hàm cập nhật giá trị dạng chữ (ví dụ "1/4").</param>
    /// <param name="model">Model OCR cần dùng; null = model mặc định của engine.</param>
    private void ReadResourceRegion(
        Mat frame,
        OpenCvSharp.Rect rect,
        string resourceName,
        Action<int>? updateAction,
        Action<string>? updateTextAction = null,
        string? model = null)
    {
        if (rect.X < 0 || rect.Y < 0 || rect.Right > frame.Width || rect.Bottom > frame.Height)
        {
            AddLog($"[{resourceName}] LỖI: vùng {rect} vượt quá khung hình {frame.Width}x{frame.Height}.");
            return;
        }

        using var crop = new Mat(frame, rect);
        string rawText = model is null ? _ocrEngine.Recognize(crop) : _ocrEngine.Recognize(crop, model);
        string cleanText = System.Text.RegularExpressions.Regex.Replace(rawText, @"[^\d]", "");

        // Lưu ảnh để debug
        string safeName = resourceName.ToLower().Replace(" ", "_");
        Cv2.ImWrite(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{safeName}_crop.png"), crop);

        AddLog($"[{resourceName}] Raw: '{rawText.Trim()}' -> Parsed: '{cleanText}'");

        if (cleanText.Length == 0)
        {
            return;
        }

        updateTextAction?.Invoke(cleanText);
        if (int.TryParse(cleanText, out int val))
        {
            updateAction?.Invoke(val);
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

    /// <summary>Xóa toàn bộ nhật ký trên giao diện.</summary>
    [RelayCommand]
    private void ClearLog() => LogMessages.Clear();

    /// <summary>Sao chép toàn bộ nhật ký vào clipboard.</summary>
    [RelayCommand]
    private void CopyLog()
    {
        if (!LogMessages.Any())
        {
            return;
        }

        Clipboard.SetText(string.Join(Environment.NewLine, LogMessages));
        AddLog("Đã copy log vào Clipboard.");
    }

    /// <summary>
    /// Thêm thông điệp nhật ký vào đầu danh sách (mới nhất hiển thị trên cùng).
    /// </summary>
    /// <param name="msg">Nội dung thông điệp.</param>
    public void AddLog(string msg)
    {
        string formatted = $"[{DateTime.Now:HH:mm:ss}] {msg}";
        _logger.Info(msg);

        // FIX-CRASH: in ra console để theo dõi khi chạy app bằng terminal.
        Console.WriteLine(formatted);

        // ObservableCollection chỉ được sửa trên UI thread.
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            LogMessages.Insert(0, formatted);
        }
        else
        {
            _ = dispatcher.InvokeAsync(() => LogMessages.Insert(0, formatted));
        }
    }
}
