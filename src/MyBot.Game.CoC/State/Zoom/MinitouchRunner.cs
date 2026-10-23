using System;
using System.IO;
using MyBot.Core.Abstractions;

namespace MyBot.Game.CoC.State.Zoom;

/// <summary>
/// Đẩy binary <c>minitouch</c> lên thiết bị rồi chạy các script pinch có sẵn.
/// Port từ <c>AndroidAdbScript</c> / <c>AndroidAdbSendShellCommandScript</c>
/// trong <c>Android.au3</c>.
/// </summary>
public sealed class MinitouchRunner
{
    private readonly IAdbShell _shell;
    private readonly string _scriptsDirectory;
    private readonly string _remoteDirectory;
    private readonly int _defaultTimeoutMs;
    private readonly IBotLogger? _logger;
    private bool _ready;
    private string? _mouseDevice;

    /// <summary>Khởi tạo runner minitouch.</summary>
    /// <param name="shell">Shell adb tới thiết bị.</param>
    /// <param name="scriptsDirectory">Thư mục chứa <c>minitouch</c> và các file <c>*.minitouch</c>.</param>
    /// <param name="remoteDirectory">Thư mục tạm trên Android.</param>
    /// <param name="defaultTimeoutMs">Thời gian chờ mặc định khi chạy script.</param>
    /// <param name="logger">Logger tuỳ chọn.</param>
    public MinitouchRunner(
        IAdbShell shell,
        string scriptsDirectory,
        string remoteDirectory = "/data/local/tmp",
        int defaultTimeoutMs = 20_000,
        IBotLogger? logger = null)
    {
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _scriptsDirectory = scriptsDirectory ?? throw new ArgumentNullException(nameof(scriptsDirectory));
        _remoteDirectory = remoteDirectory.TrimEnd('/');
        _defaultTimeoutMs = defaultTimeoutMs;
        _logger = logger;
    }

    /// <summary>True nếu đã đẩy minitouch và dò được thiết bị cảm ứng.</summary>
    public bool IsReady => _ready;

    /// <summary>Thiết bị input đang dùng (<c>/dev/input/eventN</c>).</summary>
    public string? MouseDevice => _mouseDevice;

    /// <summary>
    /// Chuẩn bị minitouch trên thiết bị: dò thiết bị cảm ứng, push binary, chmod.
    /// </summary>
    /// <returns>True nếu minitouch sẵn sàng.</returns>
    public bool EnsureReady()
    {
        if (_ready)
        {
            return true;
        }

        string binary = Path.Combine(_scriptsDirectory, "minitouch");
        if (!File.Exists(binary))
        {
            _logger?.Warning($"Không tìm thấy minitouch tại {binary} — tắt zoom qua ADB.");
            return false;
        }

        _mouseDevice = _shell.ResolveMouseDevice();
        if (string.IsNullOrWhiteSpace(_mouseDevice))
        {
            _logger?.Warning("Không dò được thiết bị input Android — tắt zoom qua ADB.");
            return false;
        }

        string remoteBinary = $"{_remoteDirectory}/minitouch";
        if (!_shell.Push(binary, remoteBinary))
        {
            _logger?.Warning("Không đẩy được minitouch lên thiết bị.");
            return false;
        }

        _shell.Chmod(remoteBinary, "555");
        _ready = true;
        return true;
    }

    /// <summary>
    /// Chạy một script minitouch theo thứ tự tìm file của AutoIt:
    /// <c>tag.Emulator.minitouch</c>, <c>tag.Emulator.script</c>, <c>tag.minitouch</c>,
    /// <c>tag.script</c>.
    /// </summary>
    /// <param name="tag">Tên script, ví dụ <c>Normal0</c> hoặc <c>Small2</c>.</param>
    /// <param name="emulatorName">Tên giả lập, ví dụ <c>MEmu</c> hoặc <c>BlueStacks5</c>.</param>
    /// <param name="timeoutMs">Thời gian chờ; null dùng giá trị khởi tạo.</param>
    /// <returns>True nếu script chạy thành công.</returns>
    public bool RunScript(string tag, string emulatorName, int? timeoutMs = null)
    {
        if (!EnsureReady())
        {
            return false;
        }

        string? scriptPath = FindScript(tag, emulatorName);
        if (scriptPath is null)
        {
            _logger?.Warning($"Không tìm thấy script minitouch cho '{tag}' ({emulatorName}).");
            return false;
        }

        string remoteScript = $"{_remoteDirectory}/{Path.GetFileName(scriptPath)}.sh";
        if (!_shell.Push(scriptPath, remoteScript))
        {
            _logger?.Warning($"Không đẩy được script {scriptPath} lên thiết bị.");
            return false;
        }

        // AutoIt: "/data/local/tmp/minitouch" -v -d /dev/input/eventX -f "<script>.sh"
        AdbShellResult result = _shell.Run(
            $"\"{_remoteDirectory}/minitouch\" -v -d {_mouseDevice} -f \"{remoteScript}\"",
            timeoutMs ?? _defaultTimeoutMs);

        if (!result.Success)
        {
            _logger?.Debug($"minitouch lỗi: {result.StdErr}");
        }

        return result.Success;
    }

    /// <summary>Tìm file script theo đúng thứ tự ưu tiên của <c>AndroidAdbScript</c>.</summary>
    /// <param name="tag">Tên script.</param>
    /// <param name="emulatorName">Tên giả lập.</param>
    /// <returns>Đường dẫn file hoặc null.</returns>
    public string? FindScript(string tag, string emulatorName)
    {
        string[] candidates =
        {
            $"{tag}.{emulatorName}.minitouch",
            $"{tag}.{emulatorName}.script",
            $"{tag}.minitouch",
            $"{tag}.script",
        };

        foreach (string candidate in candidates)
        {
            string path = Path.Combine(_scriptsDirectory, candidate);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }
}
