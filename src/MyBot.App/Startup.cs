using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyBot.Core.Abstractions;
using MyBot.Core.Config;
using MyBot.Core.FSM;
using MyBot.Core.Logging;
using MyBot.Core.Utils;
using MyBot.UI;
using MyBot.UI.Tabs.Log;
using MyBot.UI.ViewModels;
using Serilog;

namespace MyBot.App;

public static class Startup
{
    public static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Hỗ trợ ILogger<T>
        services.AddLogging();

        // Đăng ký TimeProvider trước để dùng cho việc đặt tên file log
        services.AddSingleton<ITimeProvider, SystemTimeProvider>();

        // Đăng ký IConfigManager
        services.AddSingleton<IConfigManager>(new ConfigManager("config.json", "config-schema.json"));

        // T7.27: Bộ chạy tiến trình dùng cho ADB capture
        services.AddSingleton<IProcessRunner, ProcessRunner>();

        // Cấu hình Serilog
        var logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        Directory.CreateDirectory(logDirectory); // Đảm bảo thư mục tồn tại

        // T0.4.2 & T0.4.4: Serilog tự động nối ngày yyyyMMdd vào tiền tố bot-
        var logFilePath = Path.Combine(logDirectory, "bot-.log");

        var serilogLogger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                path: logFilePath,
                rollingInterval: RollingInterval.Day, // Tự động tạo file mới khi sang ngày mới
                retainedFileCountLimit: 14,           // Giữ lại 14 ngày (T0.4.2)
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
            )
            .CreateLogger();

        // Đăng ký vào DI
        services.AddSingleton<Serilog.ILogger>(serilogLogger);
        services.AddSingleton<IBotLogger, SerilogBotLogger>();

        // Đăng ký FSM với trạng thái khởi tạo là Idle
        services.AddSingleton<BotStateMachine>(sp =>
        {
            var logger = sp.GetRequiredService<IBotLogger>();
            var fsm = new BotStateMachine(logger);
            fsm.TransitionTo(BotState.Idle);
            return fsm;
        });

        // Đăng ký MainWindow và ViewModel chính
        services.AddTransient<MainViewModel>();
        services.AddTransient<MainWindow>();

        // Đăng ký Tabs
        services.AddTransient<LogTab>();
        services.AddTransient<ITab, LogTab>();

        return services.BuildServiceProvider();
    }
}
