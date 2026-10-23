namespace MyBot.Core.Abstractions;

/// <summary>Kết quả một lệnh <c>adb shell</c>.</summary>
/// <param name="Success">True nếu adb trả về mã thoát 0.</param>
/// <param name="StdOut">Nội dung stdout.</param>
/// <param name="StdErr">Nội dung stderr.</param>
public readonly record struct AdbShellResult(bool Success, string StdOut, string StdErr);

/// <summary>
/// Truy cập shell của một thiết bị Android qua adb. Tương đương các hàm
/// <c>AndroidAdbSendShellCommand</c> / push file của <c>Android.au3</c>.
/// </summary>
public interface IAdbShell
{
    /// <summary>Serial thiết bị (<c>127.0.0.1:21503</c>), có thể null nếu adb chỉ có một thiết bị.</summary>
    string? Serial { get; }

    /// <summary>Chạy một lệnh trong <c>adb shell</c>.</summary>
    /// <param name="command">Lệnh shell (không kèm tiền tố adb).</param>
    /// <param name="timeoutMs">Thời gian chờ tối đa (ms).</param>
    /// <returns>Kết quả lệnh.</returns>
    AdbShellResult Run(string command, int timeoutMs = 20_000);

    /// <summary>Đẩy một file từ máy host lên thiết bị.</summary>
    /// <param name="localPath">Đường dẫn file trên host.</param>
    /// <param name="remotePath">Đường dẫn đích trên thiết bị.</param>
    /// <param name="timeoutMs">Thời gian chờ tối đa (ms).</param>
    /// <returns>True nếu <c>adb push</c> thành công.</returns>
    bool Push(string localPath, string remotePath, int timeoutMs = 20_000);

    /// <summary>Đổi quyền file trên thiết bị.</summary>
    /// <param name="remotePath">Đường dẫn file trên thiết bị.</param>
    /// <param name="mode">Quyền dạng octal, mặc định <c>755</c>.</param>
    /// <returns>True nếu lệnh chmod thành công.</returns>
    bool Chmod(string remotePath, string mode = "755");

    /// <summary>
    /// Dò thiết bị input cảm ứng bằng <c>getevent -pl</c>, giống
    /// <c>$g_sAndroidMouseDevice</c> của AutoIt.
    /// </summary>
    /// <param name="namePattern">
    /// Regex tên thiết bị cần tìm; null dùng mẫu mặc định của MEmu/BlueStacks.
    /// </param>
    /// <returns>Đường dẫn <c>/dev/input/eventN</c> hoặc null nếu không dò được.</returns>
    string? ResolveMouseDevice(string? namePattern = null);
}
