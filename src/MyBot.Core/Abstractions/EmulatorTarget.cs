using System;

namespace MyBot.Core.Abstractions;

public record EmulatorTarget(
    IntPtr? WindowHandle = null,
    string? AdbSerial = null,
    string? MockRecordingPath = null
);
