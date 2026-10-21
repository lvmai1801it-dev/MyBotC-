using System;
using System.Threading;
using System.Threading.Tasks;
using MyBot.Core.Abstractions;

namespace MyBot.Core.FSM;

public class StateWatchdog : IDisposable
{
    private readonly ITimeProvider _timeProvider;
    private CancellationTokenSource? _cts;
    
    public event EventHandler<BotState>? WatchdogTimeout;

    public StateWatchdog(ITimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    // Bắt đầu đếm ngược cho một trạng thái
    public void Start(BotState state, int timeoutMs)
    {
        Stop(); // Hủy bộ đếm cũ nếu có
        
        if (timeoutMs <= 0) return; // Không giới hạn thời gian

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        Task.Run(async () =>
        {
            try
            {
                await _timeProvider.DelayAsync(timeoutMs, token);
                
                // Nếu chờ xong mà không bị hủy -> Timeout!
                if (!token.IsCancellationRequested)
                {
                    WatchdogTimeout?.Invoke(this, state);
                }
            }
            catch (TaskCanceledException)
            {
                // Bị hủy bình thường, không làm gì cả
            }
        }, token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    public void Dispose() => Stop();
}
