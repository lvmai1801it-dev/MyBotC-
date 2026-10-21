using Serilog;
using MyBot.Core.Abstractions;

namespace MyBot.Core.Logging;

public class SerilogBotLogger : IBotLogger
{
    private readonly ILogger _logger;

    public SerilogBotLogger(ILogger logger)
    {
        _logger = logger;
    }

    public void Debug(string message) => _logger.Debug(message);
    public void Info(string message) => _logger.Information(message);
    public void Warning(string message) => _logger.Warning(message);
    public void Error(string message, Exception? ex = null) => _logger.Error(ex, message);
}
