namespace MyBot.Core.Abstractions;

/// <summary>
/// Điều khiển ứng dụng/thiết bị Android qua ADB để phục hồi khi bot bị kẹt:
/// khởi động lại Clash of Clans và reboot giả lập.
/// </summary>
public interface IAndroidDeviceController
{
    /// <summary>
    /// Đóng ứng dụng (force-stop) rồi mở lại qua launcher.
    /// </summary>
    /// <param name="packageName">Tên package Android, ví dụ <c>com.supercell.clashofclans</c>.</param>
    /// <returns>True nếu lệnh mở ứng dụng thành công.</returns>
    bool RestartApp(string packageName);

    /// <summary>
    /// Gửi lệnh reboot tới thiết bị/giả lập Android.
    /// </summary>
    /// <returns>True nếu lệnh reboot được chấp nhận.</returns>
    bool RebootDevice();
}
