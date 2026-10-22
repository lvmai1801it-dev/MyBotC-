using System.Collections.Generic;
using MyBot.Core.Abstractions;

namespace MyBot.Core.Tests.Mocks;

/// <summary>
/// Controller Android giả: ghi lại số lần restart app/reboot để kiểm tra recovery.
/// </summary>
public sealed class FakeAndroidDeviceController : IAndroidDeviceController
{
    /// <summary>Số lần <see cref="RestartApp"/> đã được gọi.</summary>
    public int RestartCount { get; private set; }

    /// <summary>Số lần <see cref="RebootDevice"/> đã được gọi.</summary>
    public int RebootCount { get; private set; }

    /// <summary>Các package đã được yêu cầu restart.</summary>
    public List<string> RestartedPackages { get; } = new();

    /// <summary>Kết quả trả về của <see cref="RestartApp"/>.</summary>
    public bool RestartResult { get; set; } = true;

    /// <summary>Kết quả trả về của <see cref="RebootDevice"/>.</summary>
    public bool RebootResult { get; set; } = true;

    /// <summary>Ghi nhận một lần restart app.</summary>
    /// <param name="packageName">Package được yêu cầu restart.</param>
    /// <returns><see cref="RestartResult"/>.</returns>
    public bool RestartApp(string packageName)
    {
        RestartCount++;
        RestartedPackages.Add(packageName);
        return RestartResult;
    }

    /// <summary>Ghi nhận một lần reboot giả lập.</summary>
    /// <returns><see cref="RebootResult"/>.</returns>
    public bool RebootDevice()
    {
        RebootCount++;
        return RebootResult;
    }
}
