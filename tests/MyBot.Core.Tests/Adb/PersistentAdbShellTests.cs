using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using MyBot.Core.Abstractions;
using MyBot.Core.Capture.Adb;
using MyBot.Core.Utils;
using Xunit;

namespace MyBot.Core.Tests.Adb;

/// <summary>
/// Test <see cref="PersistentAdbShell"/> trên fake adb viết bằng batch
/// (<c>Adb/FakeAdb/adb.bat</c>). Mỗi test dùng một thư mục tạm riêng nên chạy
/// song song được; <c>spawns.log</c> đếm số tiến trình đã spawn.
/// </summary>
public sealed class PersistentAdbShellTests : IAsyncLifetime
{
    private const string Serial = "test-serial";

    private readonly List<string> _tempDirs = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        foreach (string dir in _tempDirs)
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (IOException)
            {
                // Tiến trình test đã đóng file — không chặn kết quả test.
            }
            catch (UnauthorizedAccessException)
            {
                // Thư mục tạm còn bị giữ — hệ điều hành sẽ dọn.
            }
        }

        return Task.CompletedTask;
    }

    // Mode A: shell trả output rồi prompt → ExecuteAsync trả đúng phần thân.
    [Fact]
    public async Task ModeA_tra_ve_output_sau_dau_nhac()
    {
        var logger = new RecordingLogger();
        string adb = CreateFakeAdb("ok");
        await using var shell = new PersistentAdbShell(adb, Serial, new SystemTimeProvider(), logger);

        string output = await shell.ExecuteAsync("echo X");

        output.Should().Be("OUTPUT-OK");
        shell.IsRunning.Should().BeTrue();
        shell.ConsecutiveErrors.Should().Be(0);
        ReadSpawns(adb).Should().ContainSingle()
            .Which.Should().Be("-s " + Serial + " shell");
        logger.Debugs.Should().Contain(line => line.Contains("echo X", StringComparison.Ordinal));
    }

    // Mode B: phản hồi chậm hơn timeout → bộ đếm lỗi tăng, trả chuỗi rỗng.
    [Fact]
    public async Task ModeB_qua_timeout_tang_bo_dem_loi()
    {
        var logger = new RecordingLogger();
        string adb = CreateFakeAdb("slow");
        await using var shell = new PersistentAdbShell(adb, Serial, new SystemTimeProvider(), logger);

        string output = await shell.ExecuteAsync("slow-command", timeoutMs: 100);

        output.Should().BeEmpty();
        shell.ConsecutiveErrors.Should().Be(1);
        logger.Warnings.Should().ContainSingle(w => w.Contains("1/3", StringComparison.Ordinal));
        shell.IsRunning.Should().BeTrue();
    }

    // Mode C: 3 lỗi liên tiếp → kill-server, start-server, mở lại shell rồi ném exception.
    [Fact]
    public async Task ModeC_ba_loi_lien_tiep_khoi_dong_lai_daemon()
    {
        var logger = new RecordingLogger();
        string adb = CreateFakeAdb("fail");
        await using var shell = new PersistentAdbShell(adb, Serial, new SystemTimeProvider(), logger);

        (await shell.ExecuteAsync("cmd-1", timeoutMs: 100)).Should().BeEmpty();
        (await shell.ExecuteAsync("cmd-2", timeoutMs: 100)).Should().BeEmpty();

        Func<Task> act = () => shell.ExecuteAsync("cmd-3", timeoutMs: 100);

        AdbShellException failure = (await act.Should().ThrowAsync<AdbShellException>()).Which;
        failure.Command.Should().Be("cmd-3");
        failure.Output.Should().Contain("cmd-3");
        shell.ConsecutiveErrors.Should().Be(0);

        string[] spawns = ReadSpawns(adb);
        spawns.Should().Contain("-s " + Serial + " shell");
        spawns.Should().Contain(line => line.Contains("kill-server", StringComparison.Ordinal));
        spawns.Should().Contain(line => line.Contains("start-server", StringComparison.Ordinal));
        spawns.Count(line => line == "-s " + Serial + " shell").Should().Be(2);
        logger.Errors.Should().Contain(line => line.Contains("khởi động lại daemon adb", StringComparison.Ordinal));
    }

    // Mode D: dispose khi shell đang chạy → stdin đóng, tiến trình tự thoát, dùng lại thì ném.
    [Fact]
    public async Task ModeD_dispose_khi_dang_chay_kill_tien_trinh()
    {
        var logger = new RecordingLogger();
        string adb = CreateFakeAdb("ok");
        var shell = new PersistentAdbShell(adb, Serial, new SystemTimeProvider(), logger);

        await shell.EnsureStartedAsync();
        shell.IsRunning.Should().BeTrue();

        await shell.DisposeAsync();

        shell.IsRunning.Should().BeFalse();
        (await WaitForFileAsync(Path.Combine(Path.GetDirectoryName(adb)!, "exited.txt"), 2000))
            .Should().BeTrue("shell phải nhận EOF rồi thoát, không để lại tiến trình treo");

        Func<Task> act = () => shell.ExecuteAsync("after-dispose");
        await act.Should().ThrowAsync<ObjectDisposedException>();

        await shell.DisposeAsync();
    }

    // Mode E: hai lệnh song song → tuần tự hoá, không trộn output, chỉ một tiến trình shell.
    [Fact]
    public async Task ModeE_hai_lenh_song_song_duoc_tuan_tu_hoa()
    {
        var logger = new RecordingLogger();
        string adb = CreateFakeAdb("ok");
        await using var shell = new PersistentAdbShell(adb, Serial, new SystemTimeProvider(), logger);

        string[] results = await Task.WhenAll(
            shell.ExecuteAsync("lenh-mot"),
            shell.ExecuteAsync("lenh-hai"));

        results.Should().HaveCount(2);
        results.Should().AllBe("OUTPUT-OK");
        ReadSpawns(adb).Should().ContainSingle()
            .Which.Should().Be("-s " + Serial + " shell");
    }

    private string CreateFakeAdb(string mode)
    {
        string source = Path.Combine(
            RepoRoot(), "tests", "MyBot.Core.Tests", "Adb", "FakeAdb", "adb.bat");
        string dir = Path.Combine(Path.GetTempPath(), "mybot-adb-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.Copy(source, Path.Combine(dir, "adb.bat"));
        File.WriteAllText(Path.Combine(dir, "mode.txt"), mode);
        _tempDirs.Add(dir);
        return Path.Combine(dir, "adb.bat");
    }

    private static string RepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MyBot.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName
               ?? throw new InvalidOperationException("Không tìm thấy MyBot.sln từ thư mục build.");
    }

    private static string[] ReadSpawns(string adbPath)
    {
        string log = Path.Combine(Path.GetDirectoryName(adbPath)!, "spawns.log");
        using var stream = new FileStream(log, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        var lines = new List<string>();
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length > 0)
            {
                lines.Add(line);
            }
        }

        return lines.ToArray();
    }

    private static async Task<bool> WaitForFileAsync(string path, int timeoutMs)
    {
        int waited = 0;
        while (waited < timeoutMs)
        {
            if (File.Exists(path))
            {
                return true;
            }

            await Task.Delay(20);
            waited += 20;
        }

        return File.Exists(path);
    }

    private sealed class RecordingLogger : IBotLogger
    {
        private readonly List<string> _debugs = new();
        private readonly List<string> _warnings = new();
        private readonly List<string> _errors = new();

        public IReadOnlyList<string> Debugs => _debugs;

        public IReadOnlyList<string> Warnings => _warnings;

        public IReadOnlyList<string> Errors => _errors;

        public void Debug(string message) => _debugs.Add(message);

        public void Info(string message)
        {
        }

        public void Warning(string message) => _warnings.Add(message);

        public void Error(string message, Exception? ex = null) => _errors.Add(message);
    }
}
