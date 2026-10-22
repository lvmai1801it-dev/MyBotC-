using System;
using System.Collections.Generic;
using MyBot.Core.Abstractions;
using MyBot.Core.Input;

namespace MyBot.Core.Capture;

/// <summary>
/// Driver giả lập Android trên Windows: tìm cửa sổ render của MEmu/LDPlayer/BlueStacks/Nox,
/// chụp màn hình bằng GDI và gửi thao tác bằng PostMessage.
/// </summary>
public sealed class WindowsEmulatorDriver : IEmulatorDriver
{
    /// <inheritdoc />
    public string DriverName => "Windows";

    /// <inheritdoc />
    public IEnumerable<EmulatorInstance> DetectInstances()
    {
        foreach (EmulatorFinder.EmulatorWindow window in EmulatorFinder.FindAll())
        {
            // Ưu tiên cửa sổ render con (chỉ chứa phần game, không có toolbar của giả lập).
            IntPtr renderHandle = EmulatorFinder.FindRenderChild(window.Handle, window.EmulatorType);

            yield return new EmulatorInstance(
                Id: renderHandle.ToInt64().ToString("X"),
                Name: $"{window.EmulatorType} - {window.Title}",
                AdbPort: GetDefaultAdbPort(window.EmulatorType),
                WindowHandle: renderHandle);
        }
    }

    /// <inheritdoc />
    public bool Launch(string instanceId) => false;

    /// <inheritdoc />
    public ICaptureBackend CreateCaptureBackend() => new Win32GdiCaptureBackend();

    /// <inheritdoc />
    public IInputBackend CreateInputBackend() => new Win32PostMessageInputBackend();

    private static int GetDefaultAdbPort(string emulatorType) => emulatorType switch
    {
        "MEmu" => 21503,
        "Nox" => 62001,
        _ => 5555,
    };
}
