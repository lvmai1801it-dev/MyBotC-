using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyBot.Core.Abstractions;
using MyBot.Core.FSM;

namespace MyBot.UI.ViewModels;

/// <summary>
/// ViewModel chính của ứng dụng, điều phối trạng thái FSM (Start, Stop, Pause)
/// và hiển thị danh sách nhật ký trên giao diện.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly BotStateMachine _fsm;
    private readonly IBotLogger _logger;

    [ObservableProperty]
    private string _statusText = "Idle";

    [ObservableProperty]
    private ObservableCollection<string> _logMessages = new();

    /// <summary>
    /// Khởi tạo ViewModel với máy trạng thái FSM và logger.
    /// </summary>
    /// <param name="fsm">Máy trạng thái hữu hạn của Bot.</param>
    /// <param name="logger">Logger ghi nhận sự kiện.</param>
    public MainViewModel(BotStateMachine fsm, IBotLogger logger)
    {
        _fsm = fsm ?? throw new ArgumentNullException(nameof(fsm));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _statusText = _fsm.CurrentState.ToString();

        _fsm.StateChanged += (s, newState) =>
        {
            void OnStateChanged()
            {
                StatusText = newState.ToString();
                UpdateCommandCanExecute();
            }

            if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
            {
                dispatcher.InvokeAsync(OnStateChanged);
            }
            else
            {
                OnStateChanged();
            }
        };
    }

    private bool CanStart => _fsm.CurrentState == BotState.Idle;
    private bool CanStop => _fsm.CurrentState is BotState.Starting or BotState.Searching or BotState.Paused;
    private bool CanPause => _fsm.CurrentState == BotState.Searching;

    /// <summary>
    /// Bắt đầu chạy bot: chuyển từ Idle -> Starting -> Searching sau 2 giây.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStart))]
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
    /// Dừng bot: chuyển từ Searching/Starting -> Stopping -> Idle.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanStop))]
    private void StopBot()
    {
        if (_fsm.CurrentState is BotState.Searching or BotState.Starting or BotState.Paused)
        {
            _fsm.TransitionTo(BotState.Stopping);
            AddLog("Bot Stopped");
            _fsm.TransitionTo(BotState.Idle);
        }
    }

    /// <summary>
    /// Tạm dừng bot: chuyển từ Searching -> Pausing -> Paused.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPause))]
    private void PauseBot()
    {
        if (_fsm.CurrentState == BotState.Searching)
        {
            _fsm.TransitionTo(BotState.Pausing);
            _fsm.TransitionTo(BotState.Paused);
            AddLog("Bot Paused");
        }
    }

    /// <summary>
    /// Cập nhật trạng thái CanExecute của các lệnh điều khiển bot.
    /// </summary>
    public void UpdateCommandCanExecute()
    {
        void Update()
        {
            StartBotCommand.NotifyCanExecuteChanged();
            StopBotCommand.NotifyCanExecuteChanged();
            PauseBotCommand.NotifyCanExecuteChanged();
        }

        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.InvokeAsync(Update);
        }
        else
        {
            Update();
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
            dispatcher.InvokeAsync(() => LogMessages.Insert(0, formatted));
        }
        else
        {
            LogMessages.Insert(0, formatted);
        }
    }
}
