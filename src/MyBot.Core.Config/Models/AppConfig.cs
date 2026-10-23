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

    /// <summary>
    /// Tên instance đã chọn trong profile (ví dụ <c>MEmu</c>).
    /// Chuỗi rỗng = lần chạy đầu, bot tự dò giả lập đang chạy/đã cài.
    /// </summary>
    [JsonProperty("android_instance")]
    public string AndroidInstance { get; set; } = string.Empty;

    [JsonProperty("adb_port")]
    public int AdbPort { get; set; } = 21503;

    [JsonProperty("enable_debug_log")]
    public bool EnableDebugLog { get; set; } = false;

    /// <summary>
    /// Cấp Town Hall đã nhận diện (<c>$g_iTownHallLevel</c>); 0 khi chưa nhận diện.
    /// </summary>
    [JsonProperty("town_hall_level")]
    public int TownHallLevel { get; set; }

    /// <summary>
    /// Toạ độ X Town Hall trong hệ tham chiếu của làng (<c>$g_aiTownHallPos[0]</c>);
    /// -1 khi chưa nhận diện. Lần chạy sau dùng để tìm cục bộ ±90 px.
    /// </summary>
    [JsonProperty("town_hall_x")]
    public int TownHallX { get; set; } = -1;

    /// <summary>
    /// Toạ độ Y Town Hall trong hệ tham chiếu của làng (<c>$g_aiTownHallPos[1]</c>);
    /// -1 khi chưa nhận diện.
    /// </summary>
    [JsonProperty("town_hall_y")]
    public int TownHallY { get; set; } = -1;
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
