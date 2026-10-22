using System;
using System.Collections.Generic;

namespace MyBot.Core.Abstractions;

/// <summary>
/// Một instance giả lập Android đang chạy trên máy.
/// </summary>
/// <param name="Id">Định danh duy nhất của cửa sổ (chuỗi hex của handle).</param>
/// <param name="Name">Tên hiển thị, ví dụ <c>MEmu - MEmu</c>.</param>
/// <param name="AdbPort">Cổng ADB mặc định của giả lập.</param>
/// <param name="WindowHandle">Handle cửa sổ render dùng để chụp/gửi input.</param>
/// <param name="EmulatorType">Loại giả lập, ví dụ <c>MEmu</c> hoặc <c>BlueStacks5</c>.</param>
public record EmulatorInstance(
    string Id,
    string Name,
    int AdbPort,
    IntPtr WindowHandle,
    string EmulatorType = "");

/// <summary>
/// Một bản giả lập Android đã cài trên máy nhưng có thể chưa chạy.
/// </summary>
/// <param name="EmulatorType">Loại giả lập, ví dụ <c>MEmu</c>.</param>
/// <param name="InstanceName">Tên instance mặc định (theo cấu hình của giả lập).</param>
/// <param name="ExecutablePath">Đường dẫn tuyệt đối tới file chạy của giả lập.</param>
/// <param name="AdbPort">Cổng ADB mặc định.</param>
public record EmulatorInstallation(
    string EmulatorType,
    string InstanceName,
    string ExecutablePath,
    int AdbPort);

/// <summary>
/// Driver nhận diện và khởi động giả lập Android trên Windows.
/// </summary>
public interface IEmulatorDriver
{
    /// <summary>Gets tên driver, dùng cho log.</summary>
    string DriverName { get; }

    /// <summary>
    /// Dò các instance giả lập đang chạy (có cửa sổ).
    /// Tương đương <c>DetectRunningAndroid()</c> của bản AutoIt.
    /// </summary>
    /// <returns>Danh sách instance đang chạy; rỗng nếu không có.</returns>
    IReadOnlyList<EmulatorInstance> DetectRunningInstances();

    /// <summary>
    /// Dò các bản giả lập đã cài nhưng chưa chạy.
    /// Tương đương <c>DetectInstalledAndroid()</c> của bản AutoIt.
    /// </summary>
    /// <param name="adbPath">
    /// Đường dẫn adb đang cấu hình; dùng làm gợi ý để tìm bản cài cùng thư mục
    /// (ví dụ <c>F:\Microvirt\MEmu\adb.exe</c> → <c>MEmu.exe</c>).
    /// </param>
    /// <returns>Danh sách bản cài tìm thấy; rỗng nếu không có.</returns>
    IReadOnlyList<EmulatorInstallation> DetectInstalled(string? adbPath = null);

    /// <summary>Khởi động giả lập theo tên instance hoặc loại giả lập.</summary>
    /// <param name="instanceId">Tên instance (ví dụ <c>MEmu</c>) hoặc loại giả lập.</param>
    /// <param name="adbPath">
    /// Đường dẫn adb đang cấu hình; dùng làm gợi ý tìm file chạy cùng thư mục.
    /// </param>
    /// <returns>True nếu tiến trình khởi động được.</returns>
    bool Launch(string instanceId, string? adbPath = null);

    /// <summary>Tạo backend chụp màn hình cho giả lập.</summary>
    /// <returns>Backend chụp màn hình mới.</returns>
    ICaptureBackend CreateCaptureBackend();

    /// <summary>Tạo backend gửi input cho giả lập.</summary>
    /// <returns>Backend input mới.</returns>
    IInputBackend CreateInputBackend();
}
