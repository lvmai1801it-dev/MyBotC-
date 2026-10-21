namespace MyBot.Core.Abstractions;

public enum NotificationSeverity { Info, Warning, Error, Success }

public interface INotifier
{
    bool IsEnabled { get; }
    Task NotifyAsync(NotificationSeverity severity, string title, string message, string? attachmentPath = null);
}
