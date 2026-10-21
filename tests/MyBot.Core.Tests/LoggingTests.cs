using System;
using System.IO;
using FluentAssertions;
using MyBot.Core.Abstractions;
using MyBot.Core.Logging;
using Serilog;

namespace MyBot.Core.Tests;

public class LoggingTests
{
    // T0.4.1 & T0.4.4: Ghi log ra file và kiểm tra tên file theo TimeProvider
    [Fact]
    public void Logger_Should_Write_To_File_And_Name_It_Based_On_TimeProvider()
    {
        // Arrange
        var testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_logs");
        Directory.CreateDirectory(testDir);
        var testFilePath = Path.Combine(testDir, "bot_2024-10-21.log");
        
        if (File.Exists(testFilePath)) File.Delete(testFilePath);

        var serilogCore = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(testFilePath)
            .CreateLogger();
            
        var botLogger = new SerilogBotLogger(serilogCore);

        // Act
        botLogger.Info("Test message 1");
        botLogger.Debug("Test message 2");
        serilogCore.Dispose(); // Đảm bảo ghi xong xuống ổ cứng và giải phóng file handle

        // Assert
        File.Exists(testFilePath).Should().BeTrue("File log phải được tạo ra");
        var content = File.ReadAllText(testFilePath);
        content.Should().Contain("Test message 1");
        content.Should().Contain("Test message 2");
    }

    // T0.4.3: Cấu hình mức Log (MinimumLevel)
    [Fact]
    public void Logger_Should_Respect_Minimum_Level()
    {
        // Arrange
        var testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_logs_level");
        Directory.CreateDirectory(testDir);
        var testFilePath = Path.Combine(testDir, "level_test.log");
        if (File.Exists(testFilePath)) File.Delete(testFilePath);

        // Chỉ định chỉ ghi từ mức Warning trở lên
        var serilogCore = new LoggerConfiguration()
            .MinimumLevel.Warning() 
            .WriteTo.File(testFilePath)
            .CreateLogger();
            
        var botLogger = new SerilogBotLogger(serilogCore);

        // Act
        botLogger.Info("Info khong duoc ghi");
        botLogger.Warning("Warning duoc ghi");
        botLogger.Error("Error duoc ghi");
        serilogCore.Dispose(); // Đảm bảo ghi xong xuống ổ cứng và giải phóng file handle

        // Assert
        var content = File.ReadAllText(testFilePath);
        content.Should().NotContain("Info khong duoc ghi");
        content.Should().Contain("Warning duoc ghi");
        content.Should().Contain("Error duoc ghi");
    }
}
