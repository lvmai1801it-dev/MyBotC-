using System;
using MyBot.Core.Abstractions;

namespace MyBot.Core.FSM;

public class BotStateMachine
{
    private readonly IBotLogger _logger;
    private BotState _currentState = BotState.Uninitialized;

    public BotState CurrentState => _currentState;
    
    // Sự kiện phát ra khi trạng thái thay đổi (để UI hoặc Watchdog bắt tín hiệu)
    public event EventHandler<BotState>? StateChanged;

    public BotStateMachine(IBotLogger logger)
    {
        _logger = logger;
    }

    public void TransitionTo(BotState newState)
    {
        if (_currentState == newState) return;

        if (!TransitionGuard.IsValid(_currentState, newState))
        {
            string errorMsg = $"Chuyển trạng thái không hợp lệ: {_currentState} -> {newState}";
            _logger.Error(errorMsg);
            throw new InvalidOperationException(errorMsg);
        }

        _logger.Info($"STATE: {_currentState} -> {newState}");
        _currentState = newState;
        
        // Kích hoạt sự kiện
        StateChanged?.Invoke(this, newState);
    }
}
