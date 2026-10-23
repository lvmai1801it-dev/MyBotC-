using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using MyBot.Core.Abstractions;

namespace MyBot.Core.Input;

/// <summary>
/// Triển khai <see cref="IAdbShell"/> bằng <see cref="IProcessRunner"/>, dùng cùng
/// đường dẫn adb và serial với các backend capture/input hiện có.
/// </summary>
public sealed class AdbShell : IAdbShell
{
    private const string DefaultMouseDevicePattern =
        @"Microvirt Virtual Input|User Input|BlueStacks Virtual Touch|Virtual Touch";

    private static readonly Regex DeviceHeader = new(
        @"^add device \d+:\s*(/dev/input/event\d+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex DeviceName = new(
        "^name:\\s*\"(?<name>[^\"]*)\"",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // "getevent -p" in các dòng nối tiếp dạng "  0035  : value 0, ... max 860".
    private static readonly Regex NumericAbsMax = new(
        @"^\s*(?:ABS \(\d+\):\s*)?00(?<axis>35|36)\s*:.*max\s*(?<max>\d+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // "getevent -pl" in tên trục thay cho mã hex.
    private static readonly Regex NamedAbsMax = new(
        @"ABS_MT_POSITION_(?<axis>[XY])\s*:.*max\s*(?<max>\d+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly string _adbPath;
    private readonly IProcessRunner _processRunner;

    /// <summary>Khởi tạo shell adb cho một thiết bị cụ thể.</summary>
    /// <param name="adbPath">Đường dẫn tới <c>adb.exe</c>.</param>
    /// <param name="serial">Serial thiết bị; null/rỗng nếu adb chỉ có một thiết bị.</param>
    /// <param name="processRunner">Bộ chạy tiến trình.</param>
    public AdbShell(string adbPath, string? serial, IProcessRunner processRunner)
    {
        _adbPath = adbPath ?? throw new ArgumentNullException(nameof(adbPath));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        Serial = serial;
    }

    /// <inheritdoc />
    public string? Serial { get; }

    /// <inheritdoc />
    public AdbShellResult Run(string command, int timeoutMs = 20_000)
    {
        ArgumentNullException.ThrowIfNull(command);

        ProcessResult result = _processRunner.Run(
            _adbPath,
            $"{BuildTargetArgs()}shell {command}",
            timeoutMs);

        return new AdbShellResult(result.IsSuccess, result.StdOut, result.StdErr);
    }

    /// <inheritdoc />
    public bool Push(string localPath, string remotePath, int timeoutMs = 20_000)
    {
        if (!System.IO.File.Exists(localPath))
        {
            return false;
        }

        ProcessResult result = _processRunner.Run(
            _adbPath,
            $"{BuildTargetArgs()}push \"{localPath}\" \"{remotePath}\"",
            timeoutMs);

        return result.IsSuccess;
    }

    /// <inheritdoc />
    public bool Chmod(string remotePath, string mode = "755")
    {
        return Run($"chmod {mode} \"{remotePath}\"", 5_000).Success;
    }

    /// <inheritdoc />
    public string? ResolveMouseDevice(string? namePattern = null)
    {
        AdbShellResult result = Run("getevent -p", 5_000);
        if (!result.Success)
        {
            return null;
        }

        return ParseMouseDevice(result.StdOut, namePattern);
    }

    /// <summary>
    /// Phân tích kết quả <c>getevent -p</c> để tìm thiết bị cảm ứng.
    /// Ưu tiên khớp tên; nếu không có thì chọn thiết bị có trục ABS 35/36 đúng
    /// kích thước game 860x732 như AutoIt. Chấp nhận cả dạng <c>-p</c> (mã hex)
    /// lẫn <c>-pl</c> (tên trục).
    /// </summary>
    /// <param name="output">Nội dung <c>getevent -p</c>.</param>
    /// <param name="namePattern">Regex tên thiết bị; null dùng mẫu MEmu/BlueStacks.</param>
    /// <param name="screenWidth">Chiều rộng màn hình game.</param>
    /// <param name="screenHeight">Chiều cao màn hình game.</param>
    /// <returns>Đường dẫn thiết bị hoặc null.</returns>
    public static string? ParseMouseDevice(
        string output,
        string? namePattern = null,
        int screenWidth = 860,
        int screenHeight = 732)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        var pattern = new Regex(
            string.IsNullOrWhiteSpace(namePattern) ? DefaultMouseDevicePattern : namePattern,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        var byName = new List<string>();
        var bySize = new List<string>();

        string? currentDevice = null;
        string? currentName = null;
        string? maxX = null;
        string? maxY = null;

        void Flush()
        {
            if (currentDevice is null)
            {
                return;
            }

            if (currentName is not null && pattern.IsMatch(currentName))
            {
                byName.Add(currentDevice);
            }

            if (int.TryParse(maxX, NumberStyles.Integer, CultureInfo.InvariantCulture, out int x)
                && int.TryParse(maxY, NumberStyles.Integer, CultureInfo.InvariantCulture, out int y)
                && System.Math.Abs(x - screenWidth) <= 2
                && System.Math.Abs(y - screenHeight) <= 2)
            {
                bySize.Add(currentDevice);
            }
        }

        foreach (string rawLine in output.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n'))
        {
            string line = rawLine.TrimEnd();

            Match header = DeviceHeader.Match(line);
            if (header.Success)
            {
                Flush();
                currentDevice = header.Groups[1].Value;
                currentName = null;
                maxX = null;
                maxY = null;
                continue;
            }

            if (currentDevice is null)
            {
                continue;
            }

            Match name = DeviceName.Match(line.Trim());
            if (name.Success)
            {
                currentName = name.Groups["name"].Value;
                continue;
            }

            Match abs = NumericAbsMax.Match(line);
            if (!abs.Success)
            {
                abs = NamedAbsMax.Match(line);
            }

            if (abs.Success)
            {
                if (abs.Groups["axis"].Value is "35" or "X")
                {
                    maxX = abs.Groups["max"].Value;
                }
                else
                {
                    maxY = abs.Groups["max"].Value;
                }
            }
        }

        Flush();

        return byName.Count > 0 ? byName[0]
            : bySize.Count > 0 ? bySize[0]
            : null;
    }

    private string BuildTargetArgs()
    {
        return string.IsNullOrWhiteSpace(Serial) ? string.Empty : $"-s {Serial} ";
    }
}
