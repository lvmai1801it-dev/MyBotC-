using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using MyBot.Core.Abstractions;

namespace MyBot.Core.Capture.Adb;

/// <summary>
/// Console <c>adb shell</c> thường trú: mở đúng một tiến trình, ghi lệnh vào stdin
/// và đọc stdout tới dấu nhắc <c>mybot.run:</c> thay vì gọi adb cho từng lệnh.
///
/// parity: AndroidAdbLaunchShellInstance @ COCBot/functions/Android/Android.au3:1934 —
/// mở shell một lần, thử lại 3 lượt trước khi bỏ cuộc.
/// parity: AndroidAdbSendShellCommand @ COCBot/functions/Android/Android.au3:2333 —
/// gửi lệnh, đọc tới prompt, timeout mặc định 3.000 ms, đếm lỗi liên tiếp để khởi
/// động lại daemon adb.
/// </summary>
public sealed class PersistentAdbShell : IAsyncDisposable
{
    /// <summary>Timeout mặc định của một lệnh, tính bằng mili-giây.</summary>
    public const int DefaultTimeoutMs = 3000;

    /// <summary>Dấu nhắc cuối mỗi phản hồi; AutoIt đặt qua <c>export PS1=mybot.run:</c>.</summary>
    public const string PromptMarker = "mybot.run:";

    /// <summary>Số lỗi liên tiếp tối đa trước khi khởi động lại daemon adb.</summary>
    public const int MaxConsecutiveErrors = 3;

    /// <summary>Chu kỳ đọc buffer khi chờ prompt, tính bằng mili-giây.</summary>
    private const int PollIntervalMs = 10;

    /// <summary>Thời gian chờ shell ổn định sau khi spawn, tính bằng mili-giây.</summary>
    private const int StartupSettleMs = 500;

    /// <summary>Thời gian chờ tiến trình thoát trước khi kill, tính bằng mili-gây.</summary>
    private const int ExitWaitMs = 1000;

    private readonly string _adbPath;
    private readonly string _serial;
    private readonly ITimeProvider _time;
    private readonly IBotLogger _logger;
    private readonly int _defaultTimeoutMs;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _bufferLock = new();
    private readonly StringBuilder _buffer = new();

    private Process? _process;
    private StreamWriter? _stdin;
    private Task? _stdoutPump;
    private Task? _stderrPump;
    private int _consecutiveErrors;
    private bool _disposed;

    /// <summary>Khởi tạo shell thường trú cho một thiết bị.</summary>
    /// <param name="adbPath">Đường dẫn file thực thi adb.</param>
    /// <param name="serial">Serial thiết bị (<c>127.0.0.1:21503</c>).</param>
    /// <param name="time">Nguồn thời gian; mọi delay đều đi qua đây.</param>
    /// <param name="logger">Logger của bot.</param>
    /// <param name="defaultTimeoutMs">Timeout mặc định thay thế cho <see cref="DefaultTimeoutMs"/>.</param>
    public PersistentAdbShell(
        string adbPath,
        string serial,
        ITimeProvider time,
        IBotLogger logger,
        int defaultTimeoutMs = DefaultTimeoutMs)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adbPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(serial);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentOutOfRangeException.ThrowIfLessThan(defaultTimeoutMs, 1);

        _adbPath = adbPath;
        _serial = serial;
        _time = time;
        _logger = logger;
        _defaultTimeoutMs = defaultTimeoutMs;
    }

    /// <summary>True khi tiến trình shell còn sống và chưa bị dispose.</summary>
    public bool IsRunning => !_disposed && _process is { HasExited: false };

    /// <summary>Số lỗi liên tiếp hiện tại (đặt về 0 sau mỗi lệnh thành công).</summary>
    public int ConsecutiveErrors => _consecutiveErrors;

    /// <summary>
    /// Gửi một lệnh và trả về output đã bỏ dòng echo + prompt cuối.
    ///
    /// parity: _AndroidAdbSendShellCommand @ COCBot/functions/Android/Android.au3:2343 —
    /// ghi lệnh kèm LF, đọc tới <c>@LF &amp; $g_sAndroidAdbPrompt</c>, timeout mặc định
    /// 3.000 ms. Lỗi 1–2 lần liên tiếp trả về chuỗi rỗng như AutoIt (<c>SetError(1)</c>);
    /// lỗi thứ <see cref="MaxConsecutiveErrors"/> thì khởi động lại daemon và ném
    /// <see cref="AdbShellException"/>.
    /// </summary>
    /// <param name="command">Lệnh shell, không kèm tiền tố <c>adb</c>.</param>
    /// <param name="timeoutMs">Timeout riêng; null dùng timeout mặc định; ≤ 0 chỉ đọc buffer hiện có.</param>
    /// <param name="ct">Token huỷ.</param>
    /// <returns>Output của lệnh, hoặc chuỗi rỗng khi lỗi dưới ngưỡng.</returns>
    public async Task<string> ExecuteAsync(string command, int? timeoutMs = null, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(command);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            await EnsureStartedLockedAsync(ct).ConfigureAwait(false);
            return await ExecuteLockedAsync(command, timeoutMs ?? _defaultTimeoutMs, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Mở shell nếu chưa có. Spawn <c>"{adbPath}" -s {serial} shell</c> rồi đặt prompt.
    ///
    /// parity: AndroidAdbLaunchShellInstance @ COCBot/functions/Android/Android.au3:1934 —
    /// <c>RunPipe('"adb" -s serial shell')</c> + <c>Sleep(500)</c>.
    /// </summary>
    /// <param name="ct">Token huỷ.</param>
    public async Task EnsureStartedAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            await EnsureStartedLockedAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Giết shell, chạy <c>adb kill-server</c> + <c>adb start-server</c> rồi mở lại.
    ///
    /// parity: <c>RebootAndroid()</c> / <c>ConnectAndroidAdb(False, False, 3000)</c>
    /// trong _AndroidAdbSendShellCommand @ COCBot/functions/Android/Android.au3:2422.
    /// </summary>
    /// <param name="ct">Token huỷ.</param>
    public async Task RestartDaemonAsync(CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            await RestartDaemonLockedAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Đóng stdin, chờ tiến trình thoát (tối đa 1 s) rồi kill nếu còn sống.</summary>
    /// <returns>ValueTask hoàn tất khi mọi handle đã đóng.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopProcessLockedAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>Đọc lệnh, chờ prompt, cắt framing và cập nhật bộ đếm lỗi.</summary>
    private async Task<string> ExecuteLockedAsync(string command, int timeoutMs, CancellationToken ct)
    {
        if (_process is null || _stdin is null || _process.HasExited)
        {
            string reason = _process is null ? "shell chưa được mở" : "tiến trình shell đã thoát";
            return await FailAsync(command, string.Empty, reason, ct).ConfigureAwait(false);
        }

        DateTime started = _time.UtcNow;
        ClearBuffer();

        try
        {
            await _stdin.WriteAsync(command + "\n").ConfigureAwait(false);
            await _stdin.FlushAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
            return await FailAsync(command, TakeBuffer(), ex.Message, ct).ConfigureAwait(false);
        }

        bool answered = timeoutMs <= 0 || await WaitForPromptAsync(started, timeoutMs, ct).ConfigureAwait(false);
        string raw = TakeBuffer();

        if (!answered)
        {
            return await FailAsync(command, raw, $"không thấy prompt sau {timeoutMs} ms", ct).ConfigureAwait(false);
        }

        _consecutiveErrors = 0;
        string output = StripFraming(raw);
        int elapsed = (int)(_time.UtcNow - started).TotalMilliseconds;
        _logger.Debug($"ADB shell: {command} ({elapsed} ms, {output.Length} ký tự)");
        return output;
    }

    /// <summary>Ghi nhận một lỗi; đủ ngưỡng thì khởi động lại daemon và ném exception.</summary>
    private async Task<string> FailAsync(string command, string raw, string reason, CancellationToken ct)
    {
        _consecutiveErrors++;
        _logger.Warning(
            $"ADB shell lỗi {_consecutiveErrors}/{MaxConsecutiveErrors}: {command} ({reason}) — output: {raw}");

        if (_consecutiveErrors < MaxConsecutiveErrors)
        {
            return string.Empty;
        }

        _consecutiveErrors = 0;
        _logger.Error($"{MaxConsecutiveErrors} lỗi ADB shell liên tiếp — khởi động lại daemon adb.");
        try
        {
            await RestartDaemonLockedAsync(ct).ConfigureAwait(false);
        }
        catch (AdbShellException ex)
        {
            throw new AdbShellException(
                $"ADB shell lỗi {MaxConsecutiveErrors} lần liên tiếp và không thể khởi động lại: {ex.Message}",
                command,
                raw,
                ex);
        }

        throw new AdbShellException(
            $"ADB shell lỗi {MaxConsecutiveErrors} lần liên tiếp: {command} ({reason})",
            command,
            raw);
    }

    /// <summary>Chờ tới khi buffer kết thúc bằng LF + prompt hoặc hết timeout.</summary>
    private async Task<bool> WaitForPromptAsync(DateTime started, int timeoutMs, CancellationToken ct)
    {
        while (true)
        {
            if (EndsWithPrompt(PeekBuffer()))
            {
                return true;
            }

            if (_process is null || _process.HasExited)
            {
                return false;
            }

            if ((_time.UtcNow - started).TotalMilliseconds >= timeoutMs)
            {
                return false;
            }

            await _time.DelayAsync(PollIntervalMs, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Mở tiến trình shell và đặt prompt nếu chưa có.</summary>
    private async Task EnsureStartedLockedAsync(CancellationToken ct)
    {
        if (_process is { HasExited: false })
        {
            return;
        }

        await StopProcessLockedAsync().ConfigureAwait(false);

        Process process = new() { StartInfo = CreateStartInfo() };
        try
        {
            if (!process.Start())
            {
                process.Dispose();
                throw new AdbShellException("Không khởi động được ADB shell.", string.Empty, string.Empty);
            }
        }
        catch (Win32Exception ex)
        {
            process.Dispose();
            throw new AdbShellException(
                $"Không khởi động được '{_adbPath}': {ex.Message}", string.Empty, string.Empty, ex);
        }

        _process = process;
        _stdin = process.StandardInput;
        _stdin.AutoFlush = false;
        _stdoutPump = PumpAsync(process.StandardOutput);
        _stderrPump = PumpAsync(process.StandardError);

        // parity: Sleep(500) sau RunPipe @ COCBot/functions/Android/Android.au3:2159
        await _time.DelayAsync(StartupSettleMs, ct).ConfigureAwait(false);

        ClearBuffer();
        string initCommand = "export PS1=" + PromptMarker;
        await WriteLineLockedAsync(initCommand).ConfigureAwait(false);

        bool ready = await WaitForPromptAsync(_time.UtcNow, _defaultTimeoutMs, ct).ConfigureAwait(false);
        string raw = TakeBuffer();
        if (!ready)
        {
            _logger.Error(
                $"ADB shell không trả về prompt '{PromptMarker}' sau {_defaultTimeoutMs} ms: {raw}");
            await StopProcessLockedAsync().ConfigureAwait(false);
            throw new AdbShellException(
                $"ADB shell không trả về prompt '{PromptMarker}'.", initCommand, raw);
        }

        _logger.Debug($"ADB shell đã mở: \"{_adbPath}\" -s {_serial} shell");
    }

    /// <summary>Dừng shell và khởi động lại daemon adb (đang giữ gate).</summary>
    private async Task RestartDaemonLockedAsync(CancellationToken ct)
    {
        await StopProcessLockedAsync().ConfigureAwait(false);
        await RunOneShotAsync("kill-server", ct).ConfigureAwait(false);
        await RunOneShotAsync("start-server", ct).ConfigureAwait(false);
        await EnsureStartedLockedAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Chạy một lệnh adb một lần rồi thoát (kill-server / start-server).</summary>
    private async Task RunOneShotAsync(string arguments, CancellationToken ct)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _adbPath,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        try
        {
            if (!process.Start())
            {
                return;
            }
        }
        catch (Win32Exception ex)
        {
            _logger.Warning($"Không chạy được adb {arguments}: {ex.Message}");
            return;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_defaultTimeoutMs);
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // Tiến trình vừa thoát giữa lúc kiểm tra và lúc kill.
            }

            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>Đóng stdin, chờ thoát tối đa 1 s rồi kill; sau đó giải phóng stream.</summary>
    private async Task StopProcessLockedAsync()
    {
        Process? process = _process;
        StreamWriter? stdin = _stdin;
        Task? stdoutPump = _stdoutPump;
        Task? stderrPump = _stderrPump;

        _process = null;
        _stdin = null;
        _stdoutPump = null;
        _stderrPump = null;

        try
        {
            stdin?.Dispose();
        }
        catch (IOException)
        {
            // Pipe đã đóng — bỏ qua.
        }

        if (process is not null)
        {
            try
            {
                if (!process.WaitForExit(ExitWaitMs))
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(ExitWaitMs);
                }
            }
            catch (InvalidOperationException)
            {
                // Tiến trình chưa từng chạy hoặc đã bị dispose.
            }
            catch (Win32Exception)
            {
                // Không đủ quyền kill — để hệ điều hành dọn.
            }
        }

        if (stdoutPump is not null || stderrPump is not null)
        {
            try
            {
                await Task.WhenAll(stdoutPump ?? Task.CompletedTask, stderrPump ?? Task.CompletedTask)
                    .WaitAsync(TimeSpan.FromMilliseconds(ExitWaitMs))
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                // Pump kẹt ở pipe đã chết — bỏ qua, handle sẽ đóng cùng tiến trình.
            }
            catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
            {
                // Stream đã đóng trong lúc đọc.
            }
        }

        process?.Dispose();
    }

    /// <summary>Dựng thông tin spawn <c>"{adbPath}" -s {serial} shell</c>.</summary>
    /// <remarks>
    /// parity: LaunchConsole @ COCBot/functions/Other/LaunchConsole.au3:24 — AutoIt
    /// chạy được cả <c>.bat</c> nên trên Windows phải đi qua <c>cmd.exe /c</c>.
    /// </remarks>
    private ProcessStartInfo CreateStartInfo()
    {
        string arguments = "-s " + _serial + " shell";
        string fileName = _adbPath;
        if (OperatingSystem.IsWindows())
        {
            string extension = Path.GetExtension(_adbPath);
            if (extension.Equals(".bat", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase))
            {
                fileName = "cmd.exe";
                arguments = "/c \"\"" + _adbPath + "\" " + arguments + "\"";
            }
        }

        // Encoding.UTF8 ghi BOM vào stdin và shell coi đó là một lệnh rác đầu tiên.
        Encoding utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        return new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = utf8WithoutBom,
            StandardOutputEncoding = utf8WithoutBom,
            StandardErrorEncoding = utf8WithoutBom
        };
    }

    /// <summary>Ghi một dòng lệnh vào stdin của shell.</summary>
    private async Task WriteLineLockedAsync(string line)
    {
        if (_stdin is null)
        {
            throw new AdbShellException("ADB shell chưa được mở.", line, string.Empty);
        }

        await _stdin.WriteAsync(line + "\n").ConfigureAwait(false);
        await _stdin.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>Đọc liên tục một stream và nối vào buffer chung (stdout + stderr như STDERR_MERGED).</summary>
    private async Task PumpAsync(StreamReader reader)
    {
        char[] chunk = new char[4096];
        try
        {
            int read;
            while ((read = await reader.ReadAsync(chunk.AsMemory()).ConfigureAwait(false)) > 0)
            {
                lock (_bufferLock)
                {
                    _buffer.Append(chunk, 0, read);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // Stream đóng khi shell thoát — kết thúc pump.
        }
    }

    /// <summary>
    /// Kiểm tra buffer kết thúc bằng LF + prompt. Bỏ qua CR/LF cuối vì pty thật
    /// không xuống dòng sau prompt, còn pipe của Windows thì có.
    /// </summary>
    private static bool EndsWithPrompt(string text)
        => text.TrimEnd('\r', '\n').EndsWith("\n" + PromptMarker, StringComparison.Ordinal);

    /// <summary>Cắt dòng echo lệnh và prompt cuối, chuẩn hoá CRLF.</summary>
    /// <remarks>
    /// parity: _AndroidAdbSendShellCommand @ COCBot/functions/Android/Android.au3:2365
    /// (bỏ dòng echo + prompt) và CleanLaunchOutput @ Other/LaunchConsole.au3:244.
    /// </remarks>
    private static string StripFraming(string raw)
    {
        int firstNewLine = raw.IndexOf('\n');
        if (firstNewLine > 0)
        {
            raw = raw[firstNewLine..];
        }

        int promptIndex = raw.LastIndexOf("\n" + PromptMarker, StringComparison.Ordinal);
        if (promptIndex >= 0)
        {
            raw = raw[..promptIndex];
        }

        raw = raw.Replace("\r\n", "\n", StringComparison.Ordinal);
        while (raw.Contains("\n\n", StringComparison.Ordinal))
        {
            raw = raw.Replace("\n\n", "\n", StringComparison.Ordinal);
        }

        return raw.Trim('\n', '\r');
    }

    /// <summary>Lấy toàn bộ buffer mà không xoá.</summary>
    private string PeekBuffer()
    {
        lock (_bufferLock)
        {
            return _buffer.ToString();
        }
    }

    /// <summary>Lấy toàn bộ buffer và xoá (như <c>ReadPipe</c>).</summary>
    private string TakeBuffer()
    {
        lock (_bufferLock)
        {
            string text = _buffer.ToString();
            _buffer.Clear();
            return text;
        }
    }

    /// <summary>Xoá buffer trước khi gửi lệnh mới.</summary>
    private void ClearBuffer()
    {
        lock (_bufferLock)
        {
            _buffer.Clear();
        }
    }
}

/// <summary>Lỗi không thể phục hồi của <see cref="PersistentAdbShell"/>.</summary>
public sealed class AdbShellException : Exception
{
    /// <summary>Khởi tạo exception không kèm thông tin lệnh.</summary>
    public AdbShellException()
    {
    }

    /// <summary>Khởi tạo exception với thông điệp.</summary>
    /// <param name="message">Thông điệp lỗi.</param>
    public AdbShellException(string message)
        : base(message)
    {
    }

    /// <summary>Khởi tạo exception với thông điệp và lỗi gốc.</summary>
    /// <param name="message">Thông điệp lỗi.</param>
    /// <param name="innerException">Lỗi gốc.</param>
    public AdbShellException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>Khởi tạo exception kèm lệnh và output tại thời điểm lỗi.</summary>
    /// <param name="message">Thông điệp lỗi.</param>
    /// <param name="command">Lệnh đang chạy.</param>
    /// <param name="output">Output đọc được.</param>
    /// <param name="innerException">Lỗi gốc, nếu có.</param>
    public AdbShellException(string message, string command, string output, Exception? innerException = null)
        : base(message, innerException)
    {
        Command = command;
        Output = output;
    }

    /// <summary>Lệnh gây lỗi; rỗng khi lỗi xảy ra lúc mở shell.</summary>
    public string Command { get; } = string.Empty;

    /// <summary>Output đọc được tại thời điểm lỗi.</summary>
    public string Output { get; } = string.Empty;
}
