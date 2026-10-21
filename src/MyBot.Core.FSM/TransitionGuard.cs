using System;
using System.Collections.Generic;

namespace MyBot.Core.FSM;

public static class TransitionGuard
{
    // Danh sách các cặp (Từ trạng thái -> Đến trạng thái) được phép
    private static readonly HashSet<(BotState From, BotState To)> ValidTransitions = new()
    {
        (BotState.Uninitialized, BotState.Idle),
        
        (BotState.Idle, BotState.Starting),
        
        (BotState.Starting, BotState.Searching),
        (BotState.Starting, BotState.Error),
        (BotState.Starting, BotState.Idle), // Hủy lúc đang start
        
        (BotState.Searching, BotState.Pausing),
        (BotState.Searching, BotState.Stopping),
        (BotState.Searching, BotState.Recovering),
        (BotState.Searching, BotState.Error),
        
        (BotState.Pausing, BotState.Paused),
        (BotState.Pausing, BotState.Stopping), // Ép dừng khi đang chờ pause
        
        (BotState.Paused, BotState.Resuming),
        (BotState.Paused, BotState.Stopping),
        
        (BotState.Resuming, BotState.Searching),
        (BotState.Resuming, BotState.Error),
        
        (BotState.Recovering, BotState.Searching),
        (BotState.Recovering, BotState.Error),
        
        (BotState.Stopping, BotState.Idle),
        (BotState.Stopping, BotState.Error),
        
        (BotState.Error, BotState.Idle) // Reset từ lỗi
    };

    public static bool IsValid(BotState from, BotState to)
    {
        // Cho phép tự chuyển sang chính nó (không làm gì cả)
        if (from == to) return true; 
        return ValidTransitions.Contains((from, to));
    }
}
