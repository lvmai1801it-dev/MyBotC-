using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NJsonSchema;
using NJsonSchema.NewtonsoftJson.Generation;
using MyBot.Core.Config.Models;

namespace MyBot.Core.Config;

public class ConfigManager : IConfigManager
{
    private readonly string _configFilePath;
    private readonly string _schemaFilePath;

    public ConfigManager(string configFilePath = "config.json", string schemaFilePath = "config-schema.json")
    {
        _configFilePath = configFilePath;
        _schemaFilePath = schemaFilePath;
    }

    public AppConfig Load()
    {
        // T0.5.1: Nếu file không tồn tại -> Tự tạo file mặc định
        if (!File.Exists(_configFilePath))
        {
            var defaultConfig = new AppConfig();
            Save(defaultConfig);
            return defaultConfig;
        }

        var jsonContent = File.ReadAllText(_configFilePath);

        // T0.5.4: Kiểm tra JSON hỏng (syntax)
        try
        {
            JsonConvert.DeserializeObject(jsonContent);
        }
        catch (JsonReaderException ex)
        {
            throw new ConfigValidationException($"File JSON bị lỗi cú pháp tại dòng {ex.LineNumber}, vị trí {ex.LinePosition}. Chi tiết: {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            throw new ConfigValidationException($"File JSON bị lỗi cú pháp. Chi tiết: {ex.Message}", ex);
        }

        // T0.5.3: Nâng cấp cấu hình nếu là bản cũ như v1.0
        var rawObj = JsonConvert.DeserializeObject<dynamic>(jsonContent);
        string version = rawObj?.config_version?.ToString() ?? "1.0";
        if (version == "1.0")
        {
            var migratedConfig = Migrate(jsonContent);
            Save(migratedConfig);
            return migratedConfig;
        }

        // T0.5.2: Kiểm tra Schema cho phiên bản hiện tại
        Validate(jsonContent);

        return JsonConvert.DeserializeObject<AppConfig>(jsonContent)!;
    }

    public void Save(AppConfig config)
    {
        var json = JsonConvert.SerializeObject(config, Formatting.Indented);
        File.WriteAllText(_configFilePath, json);
    }

    public void Validate(string jsonContent)
    {
        // Tạo schema từ class C# dùng NewtonsoftJson settings
        if (!File.Exists(_schemaFilePath))
        {
            var settings = new NewtonsoftJsonSchemaGeneratorSettings();
            var schema = JsonSchema.FromType<AppConfig>(settings);
            File.WriteAllText(_schemaFilePath, schema.ToJson());
        }

        var schemaFromFile = JsonSchema.FromFileAsync(_schemaFilePath).GetAwaiter().GetResult();
        var errors = schemaFromFile.Validate(jsonContent);

        if (errors.Count > 0)
        {
            var errorMessages = string.Join("\n", errors.Select(e => $"- {e.Property}: {e.Kind}"));
            throw new ConfigValidationException($"Cấu hình không hợp lệ:\n{errorMessages}");
        }
    }

    public AppConfig Migrate(string jsonContent)
    {
        // Giả lập cấu hình cũ v1.0 thiếu trường InstanceId
        var legacyConfig = JsonConvert.DeserializeObject<dynamic>(jsonContent);
        string version = legacyConfig?.config_version?.ToString() ?? "1.0";

        if (version == "1.0")
        {
            var migrated = new AppConfig
            {
                ConfigVersion = "2.0",
                Emulator =
                {
                    Type = legacyConfig?.emulator?.type?.ToString() ?? "LDPlayer",
                    AdbPort = legacyConfig?.emulator?.adb_port != null ? (int)legacyConfig.emulator.adb_port : 5555,
                    InstanceId = 0
                }
            };
            return migrated;
        }

        return JsonConvert.DeserializeObject<AppConfig>(jsonContent)!;
    }
}
