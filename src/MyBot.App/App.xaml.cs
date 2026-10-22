using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using MyBot.UI;

namespace MyBot.App;

/// <summary>
/// Entry point của ứng dụng WPF MyBot.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // FIX-CRASH: đăng ký handler trước khi khởi tạo DI để bắt cả lỗi lúc startup.
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        base.OnStartup(e);

        try
        {
            var services = MyBot.App.Startup.ConfigureServices();
            var mainWindow = services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            ShowCrash("LỖI KHỞI ĐỘNG", ex);
            Shutdown(1);
        }
    }

    /// <summary>Bắt exception không được xử lý trên bất kỳ luồng nào.</summary>
    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        ShowCrash("LỖI NGHIÊM TRỌNG", e.ExceptionObject as Exception);
    }

    /// <summary>Bắt exception không được xử lý trên UI thread và giữ app sống để xem log.</summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ShowCrash("LỖI UI", e.Exception);
        e.Handled = true;
    }

    /// <summary>
    /// Bắt exception từ Task bị bỏ quên. TaskCanceledException khi dừng bot là bình thường
    /// nên chỉ đánh dấu đã quan sát, không hiện hộp thoại.
    /// </summary>
    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();

        if (e.Exception.InnerExceptions.All(ex => ex is OperationCanceledException))
        {
            Console.Error.WriteLine("[Task] Đã bỏ qua TaskCanceledException khi dừng bot.");
            return;
        }

        ShowCrash("LỖI TÁC VỤ NỀN", e.Exception);
    }

    /// <summary>In lỗi ra console và hiển thị hộp thoại để người dùng thấy ngay.</summary>
    private static void ShowCrash(string title, Exception? ex)
    {
        string message = ex is null
            ? "Đã xảy ra lỗi không xác định."
            : $"{ex.Message}\n\n{ex.StackTrace}";

        Console.Error.WriteLine($"[{title}] {message}");

        try
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch
        {
            // Không thể hiển thị hộp thoại (VD: tiến trình đang tắt) — log console là đủ.
        }
    }
}
