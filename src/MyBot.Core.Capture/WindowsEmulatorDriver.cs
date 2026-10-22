using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using MyBot.Core.Abstractions;
using MyBot.Core.Input;

namespace MyBot.Core.Capture;

/// <summary>
/// Driver giả lập Android trên Windows: dò cửa sổ đang chạy, dò bản đã cài,
/// khởi động MEmu/BlueStacks 5 và tạo backend GDI/PostMessage.
/// </summary>
public sealed class WindowsEmulatorDriver : IEmulatorDriver
{
    // PHMOD dùng đúng tên instance mặc định trong bảng cấu hình AutoIt.
    private const string MEmuInstance = "MEmu";
    private const string BlueStacksInstance = "Pie64";

    /// <inheritdoc />
    public string DriverName => "Windows";

    /// <inheritdoc />
    public IReadOnlyList<EmulatorInstance> DetectRunningInstances()
    {
        var instances = new List<EmulatorInstance>();

        foreach (EmulatorFinder.EmulatorWindow window in EmulatorFinder.FindAll())
        {
            instances.Add(new EmulatorInstance(
                Id: window.Handle.ToInt64().ToString("X"),
                Name: $"{window.EmulatorType} - {window.Title}",
                AdbPort: GetDefaultAdbPort(window.EmulatorType),
                WindowHandle: window.Handle,
                EmulatorType: window.EmulatorType));
        }

        return instances;
    }

    /// <inheritdoc />
    public IReadOnlyList<EmulatorInstallation> DetectInstalled(string? adbPath = null)
    {
        var installations = new List<EmulatorInstallation>();

        string? memu = FindMEmuExecutable(adbPath);
        if (memu is not null)
        {
            installations.Add(new EmulatorInstallation("MEmu", MEmuInstance, memu, 21503));
        }

        string? blueStacks = FindBlueStacks5Executable(adbPath);
        if (blueStacks is not null)
        {
            installations.Add(new EmulatorInstallation(
                "BlueStacks5",
                BlueStacksInstance,
                blueStacks,
                5555));
        }

        return installations;
    }

    /// <inheritdoc />
    public bool Launch(string instanceId, string? adbPath = null)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            return false;
        }

        EmulatorInstallation? installation = DetectInstalled(adbPath).FirstOrDefault(candidate =>
            string.Equals(candidate.InstanceName, instanceId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.EmulatorType, instanceId, StringComparison.OrdinalIgnoreCase));

        if (installation is null || !File.Exists(installation.ExecutablePath))
        {
            return false;
        }

        try
        {
            // BlueStacks 5 cần --instance để mở đúng instance; MEmu tự mở VM mặc định.
            string arguments = installation.EmulatorType == "BlueStacks5"
                ? $"--instance \"{installation.InstanceName}\""
                : string.Empty;

            using Process? process = Process.Start(new ProcessStartInfo(installation.ExecutablePath)
            {
                Arguments = arguments,
                UseShellExecute = true,
            });

            return process is not null;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public ICaptureBackend CreateCaptureBackend() => new Win32GdiCaptureBackend();

    /// <inheritdoc />
    public IInputBackend CreateInputBackend() => new Win32PostMessageInputBackend();

    private static int GetDefaultAdbPort(string emulatorType) => emulatorType switch
    {
        "MEmu" => 21503,
        "BlueStacks5" => 5555,
        "Nox" => 62001,
        _ => 5555,
    };

    private static string? FindMEmuExecutable(string? adbPath)
    {
        // 1) Cùng thư mục với adb đang cấu hình: F:\Microvirt\MEmu\adb.exe → MEmu.exe.
        string? besideAdb = FindBesideAdb(adbPath, "MEmu.exe");
        if (besideAdb is not null)
        {
            return besideAdb;
        }

        // 2) Registry do MEmu ghi khi cài đặt.
        string? installDir = ReadInstallDir(@"Microvirt\MEmu");
        string? fromRegistry = CombineIfExists(installDir, "MEmu.exe");
        if (fromRegistry is not null)
        {
            return fromRegistry;
        }

        // 3) Đường dẫn cài mặc định.
        return FindExisting(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Microvirt",
                "MEmu",
                "MEmu.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Microvirt",
                "MEmu",
                "MEmu.exe"));
    }

    private static string? FindBlueStacks5Executable(string? adbPath)
    {
        string? besideAdb = FindBesideAdb(adbPath, "HD-Player.exe");
        if (besideAdb is not null)
        {
            return besideAdb;
        }

        foreach (string subKey in new[] { "BlueStacks_nxt", "BlueStacks" })
        {
            string? installDir = ReadInstallDir(subKey);
            string? fromRegistry = CombineIfExists(installDir, "HD-Player.exe")
                ?? CombineIfExists(installDir, "HD-Frontend.exe");
            if (fromRegistry is not null)
            {
                return fromRegistry;
            }
        }

        return FindExisting(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "BlueStacks_nxt",
                "HD-Player.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "BlueStacks",
                "HD-Player.exe"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "BlueStacks",
                "HD-Player.exe"));
    }

    /// <summary>Tìm file thực thi nằm cạnh adb đang cấu hình.</summary>
    private static string? FindBesideAdb(string? adbPath, string fileName)
    {
        if (string.IsNullOrWhiteSpace(adbPath))
        {
            return null;
        }

        string? directory = Path.GetDirectoryName(adbPath);
        return string.IsNullOrWhiteSpace(directory)
            ? null
            : CombineIfExists(directory, fileName);
    }

    /// <summary>Ghép thư mục với tên file và trả về nếu file tồn tại.</summary>
    private static string? CombineIfExists(string? directory, string fileName)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        string path = Path.Combine(directory, fileName);
        return File.Exists(path) ? path : null;
    }

    /// <summary>Trả về đường dẫn đầu tiên tồn tại trong danh sách.</summary>
    private static string? FindExisting(params string[] candidates)
    {
        return candidates.FirstOrDefault(File.Exists);
    }

    /// <summary>Đọc giá trị thư mục cài đặt từ registry (HKLM, cả khóa 32/64-bit).</summary>
    private static string? ReadInstallDir(string subKey)
    {
        foreach (string path in new[] { $@"SOFTWARE\{subKey}", $@"SOFTWARE\WOW6432Node\{subKey}" })
        {
            try
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(path);
                if (key is null)
                {
                    continue;
                }

                foreach (string valueName in new[] { "InstallDir", "InstallPath", "InstallLocation", "Path" })
                {
                    if (key.GetValue(valueName) is string value && !string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
            }
            catch
            {
                // Không đọc được registry thì bỏ qua, thử đường dẫn mặc định.
            }
        }

        return null;
    }
}
