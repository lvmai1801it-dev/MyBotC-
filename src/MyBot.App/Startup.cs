using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyBot.Core.Abstractions;
using MyBot.Core.AntiBan;
using MyBot.Core.Capture;
using MyBot.Core.Config;
using MyBot.Core.CV;
using MyBot.Core.FSM;
using MyBot.Core.Logging;
using MyBot.Core.Utils;
using MyBot.Localization;
using MyBot.UI;
using MyBot.UI.Gui;
using MyBot.UI.Icons;
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

        // CORE-LOOP-FINAL: driver tìm cửa sổ giả lập và tạo capture/input backend.
        services.AddSingleton<IEmulatorDriver, WindowsEmulatorDriver>();

        // INT-1: Template matcher và bộ delay cho test harness harvest
        services.AddSingleton<ITemplateMatcher, TemplateMatcher>();
        services.AddTransient<HumanDelay>();

        // OCR engine: ưu tiên MyBot.run.dll gốc (cùng engine/model với bản AutoIt),
        // fallback về engine template số nếu thiếu DLL native.
        string nativeOcrPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib");
        string digitTemplatesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "templates", "digits");
        services.AddSingleton<IOcrEngine>(sp =>
        {
            var logger = sp.GetRequiredService<IBotLogger>();
            var nativeEngine = new MyBotNativeOcrEngine();
            if (nativeEngine.Initialize(nativeOcrPath))
            {
                logger.Info($"OCR: dùng MyBot.run.dll gốc tại {nativeOcrPath}");
                return nativeEngine;
            }

            nativeEngine.Dispose();

            logger.Warning($"OCR: không nạp được MyBot.run.dll tại {nativeOcrPath}, dùng engine template số");
            var fallback = new DigitOcrEngine();
            fallback.Initialize(digitTemplatesPath);
            return fallback;
        });

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

        // GUI dựng lại từ bản AutoIt: bộ dịch i18n, mô tả màn hình và nguồn icon.
        services.AddSingleton<IMyBotTranslator>(_ => new IniFileTranslator(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Languages")));
        services.AddSingleton(_ => GuiSpecCatalog.Load());
        services.AddSingleton(_ => new MbrBotIconProvider(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib", "MBRBOT.dll")));

        // Đăng ký MainWindow và ViewModel chính
        services.AddTransient<MainViewModel>();
        services.AddTransient<MainWindow>();

        // Đăng ký Tabs
        services.AddTransient<LogTab>();
        services.AddTransient<ITab, LogTab>();

        return services.BuildServiceProvider();
    }
}
