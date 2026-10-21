namespace MyBot.Core.Abstractions;

public interface IBotLogger
{
    void Debug(string message);
    void Info(string message);
    void Warning(string message);
    void Error(string message, Exception? ex = null);
}
