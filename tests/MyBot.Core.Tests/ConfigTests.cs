using System;
using System.IO;
using FluentAssertions;
using MyBot.Core.Config;
using MyBot.Core.Config.Models;

namespace MyBot.Core.Tests;

public class ConfigTests
{
    private readonly string _testConfigPath = "test-config.json";
    private readonly string _testSchemaPath = "test-schema.json";

    public ConfigTests()
    {
        // Dọn dẹp trước khi test
        if (File.Exists(_testConfigPath)) File.Delete(_testConfigPath);
        if (File.Exists(_testSchemaPath)) File.Delete(_testSchemaPath);
    }

    // T0.5.1: Load file mặc định nếu không tồn tại
    [Fact]
    public void Load_Should_Create_Default_Config_If_Not_Exists()
    {
        var manager = new ConfigManager(_testConfigPath, _testSchemaPath);

        var config = manager.Load();

        config.Should().NotBeNull();
        config.Emulator.Type.Should().Be("LDPlayer"); // Đúng dữ liệu mặc định
        File.Exists(_testConfigPath).Should().BeTrue(); // Phải tự tạo file
    }

    // Profile lưu emulator + instance để lần sau không phải dò lại.
    [Fact]
    public void Load_Should_Read_Android_Instance_From_Profile()
    {
        File.WriteAllText(_testConfigPath, @"{
            ""config_version"": ""2.0"",
            ""emulator"": { ""type"": ""MEmu"", ""instance_id"": 0, ""adb_port"": 21503 },
            ""emulator_type"": ""MEmu"",
            ""android_instance"": ""MEmu-2"",
            ""adb_port"": 21503
        }");

        var manager = new ConfigManager(_testConfigPath, _testSchemaPath);
        var config = manager.Load();

        config.AndroidInstance.Should().Be("MEmu-2");
    }

    // T0.5.2: Ném lỗi nếu thiếu trường bắt buộc
    [Fact]
    public void Load_Should_Throw_Exception_On_Missing_Required_Field()
    {
        // Tạo file config thiếu trường AdbPort
        File.WriteAllText(_testConfigPath, @"{ 
            ""config_version"": ""2.0"",
            ""emulator"": { ""type"": ""LDPlayer"" } 
        }");

        var manager = new ConfigManager(_testConfigPath, _testSchemaPath);

        // Act & Assert
        Action act = () => manager.Load();
        act.Should().Throw<ConfigValidationException>()
           .WithMessage("*Cấu hình không hợp lệ*"); // Bắt đúng exception
    }

    // T0.5.3: Nâng cấp cấu hình từ v1.0 lên v2.0
    [Fact]
    public void Migrate_Should_Upgrade_V1_To_V2()
    {
        // File cấu hình cũ không có instance_id
        File.WriteAllText(_testConfigPath, @"{ 
            ""config_version"": ""1.0"",
            ""emulator"": { ""type"": ""MEmu"", ""adb_port"": 21503 } 
        }");

        var manager = new ConfigManager(_testConfigPath, _testSchemaPath);
        var config = manager.Load();

        // Kiểm tra xem đã lên v2 chưa và dữ liệu cũ có được giữ lại không
        config.ConfigVersion.Should().Be("2.0");
        config.Emulator.Type.Should().Be("MEmu");
        config.Emulator.AdbPort.Should().Be(21503);

        // File vật lý phải được ghi đè lên v2.0
        File.ReadAllText(_testConfigPath).Should().Contain("\"config_version\": \"2.0\"");
    }

    // T0.5.4: Ném lỗi khi file JSON bị hỏng (thiếu ngoặc, sai cú pháp)
    [Fact]
    public void Load_Should_Throw_Exception_On_Corrupt_Json()
    {
        // File JSON hỏng (thiếu dấu ngoặc nhọn)
        File.WriteAllText(_testConfigPath, @"{ ""emulator"": { ""type"": ""LDPlayer"" ");

        var manager = new ConfigManager(_testConfigPath, _testSchemaPath);

        // Act & Assert
        Action act = () => manager.Load();
        act.Should().Throw<ConfigValidationException>()
           .WithMessage("*File JSON bị lỗi cú pháp*");
    }
}
