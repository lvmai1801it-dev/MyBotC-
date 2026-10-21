using Newtonsoft.Json;

namespace MyBot.Core.Config.Models;

public class AppConfig
{
    [JsonProperty("config_version", Required = Required.Always)]
    public string ConfigVersion { get; set; } = "2.0";

    [JsonProperty("emulator", Required = Required.Always)]
    public EmulatorConfig Emulator { get; set; } = new();
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
