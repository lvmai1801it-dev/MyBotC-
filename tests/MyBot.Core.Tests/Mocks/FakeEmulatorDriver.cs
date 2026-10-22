using System;
using System.Collections.Generic;
using MyBot.Core.Abstractions;

namespace MyBot.Core.Tests.Mocks;

/// <summary>
/// Driver giả lập cho test: trả về danh sách instance/bản cài đã định trước
/// và ghi lại các lần gọi <see cref="Launch"/>.
/// </summary>
public sealed class FakeEmulatorDriver : IEmulatorDriver
{
    /// <summary>Các instance đang chạy mà <see cref="DetectRunningInstances"/> trả về.</summary>
    public List<EmulatorInstance> Running { get; } = new();

    /// <summary>Các bản cài mà <see cref="DetectInstalled"/> trả về.</summary>
    public List<EmulatorInstallation> Installed { get; } = new();

    /// <summary>Các instance đã được yêu cầu khởi động.</summary>
    public List<string> Launched { get; } = new();

    /// <summary>Kết quả trả về của <see cref="Launch"/>.</summary>
    public bool LaunchResult { get; set; } = true;

    /// <inheritdoc />
    public string DriverName => "Fake";

    /// <inheritdoc />
    public IReadOnlyList<EmulatorInstance> DetectRunningInstances() => Running;

    /// <inheritdoc />
    public IReadOnlyList<EmulatorInstallation> DetectInstalled(string? adbPath = null) => Installed;

    /// <inheritdoc />
    public bool Launch(string instanceId, string? adbPath = null)
    {
        Launched.Add(instanceId);
        return LaunchResult;
    }

    /// <inheritdoc />
    public ICaptureBackend CreateCaptureBackend() => throw new NotSupportedException();

    /// <inheritdoc />
    public IInputBackend CreateInputBackend() => throw new NotSupportedException();
}
