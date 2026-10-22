using Newtonsoft.Json;

namespace MyBot.Core.Config.Models;

public class AppConfig
{
    [JsonProperty("config_version", Required = Required.Always)]
    public string ConfigVersion { get; set; } = "2.0";

    [JsonProperty("emulator", Required = Required.Always)]
    public EmulatorConfig Emulator { get; set; } = new();

    // T7.25: Các trường phẳng được bind trực tiếp trên Tab Bot (UI).
    [JsonProperty("active_language")]
    public string ActiveLanguage { get; set; } = "English";

    [JsonProperty("delete_logs_days")]
    public int DeleteLogsDays { get; set; } = 7;

    [JsonProperty("emulator_type")]
    public string EmulatorType { get; set; } = "MEmu";

    [JsonProperty("adb_port")]
    public int AdbPort { get; set; } = 21503;

    [JsonProperty("enable_debug_log")]
    public bool EnableDebugLog { get; set; } = false;
}

public class EmulatorConfig
{
    [JsonProperty("type", Required = Required.Always)]
    public string Type { get; set; } = "LDPlayer";

    [JsonProperty("instance_id", Required = Required.Always)]
    public int InstanceId { get; set; } = 0;

    [JsonProperty("adb_port", Required = Required.Always)]
    public int AdbPort { get; set; } = 5555;
}
