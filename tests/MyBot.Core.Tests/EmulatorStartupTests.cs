using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using MyBot.Core.Abstractions;
using MyBot.Core.Capture;
using MyBot.Core.Tests.Mocks;

namespace MyBot.Core.Tests;

public class EmulatorStartupTests
{
    private const string AdbPath = @"F:\Microvirt\MEmu\adb.exe";
    private const string AdbSerial = "127.0.0.1:21503";

    // Cửa sổ đang mở sẵn → dùng lại, không launch, không restart CoC.
    [Fact]
    public async Task EnsureRunningAsync_Should_Reuse_Running_Instance()
    {
        var driver = new FakeEmulatorDriver();
        driver.Running.Add(MemuRunning());

        var runner = new MockProcessRunner();
        runner.QueueResult(0, "connected");
        runner.QueueResult(0, "device");

        var device = new FakeAndroidDeviceController();
        var startup = CreateStartup(driver, runner, device);

        EmulatorStartupResult result = await startup.EnsureRunningAsync(
            AdbPath, AdbSerial, "MEmu", "MEmu");

        result.Ready.Should().BeTrue();
        result.Launched.Should().BeFalse();
        result.EmulatorType.Should().Be("MEmu");
        result.InstanceName.Should().Be("MEmu");
        result.AdbPort.Should().Be(21503);
        driver.Launched.Should().BeEmpty();
        device.RestartCount.Should().Be(0);
        runner.LastArguments.Should().Contain(call => call.Arguments.Contains("get-state"));
    }

    // Lần đầu profile chưa có instance → dò giả lập đang chạy, không ép loại mặc định.
    [Fact]
    public async Task EnsureRunningAsync_First_Run_Should_Detect_Running_Emulator()
    {
        var driver = new FakeEmulatorDriver();
        driver.Running.Add(new EmulatorInstance(
            Id: "5555",
            Name: "BlueStacks5 - BlueStacks App Player",
            AdbPort: 5555,
            WindowHandle: new IntPtr(0x5555),
            EmulatorType: "BlueStacks5"));
        driver.Installed.Add(MemuInstalled());

        var runner = new MockProcessRunner();
        runner.QueueResult(0, "connected");
        runner.QueueResult(0, "device");

        var startup = CreateStartup(driver, runner, new FakeAndroidDeviceController());

        EmulatorStartupResult result = await startup.EnsureRunningAsync(
            AdbPath, AdbSerial, "MEmu", string.Empty);

        result.Ready.Should().BeTrue();
        result.Launched.Should().BeFalse();
        result.EmulatorType.Should().Be("BlueStacks5");
        result.AdbPort.Should().Be(5555);
        driver.Launched.Should().BeEmpty();
    }

    // Chưa chạy nhưng đã cài → launch, chờ boot xong rồi mở Clash of Clans.
    [Fact]
    public async Task EnsureRunningAsync_Should_Launch_And_Start_CoC_When_Not_Running()
    {
        var driver = new FakeEmulatorDriver();
        driver.Installed.Add(MemuInstalled());

        var runner = new MockProcessRunner();
        runner.QueueResult(0, "connected");
        runner.QueueResult(0, "device");
        runner.QueueResult(0, "1");

        var device = new FakeAndroidDeviceController();
        var startup = CreateStartup(driver, runner, device);

        EmulatorStartupResult result = await startup.EnsureRunningAsync(
            AdbPath, AdbSerial, "MEmu", "MEmu");

        result.Ready.Should().BeTrue();
        result.Launched.Should().BeTrue();
        driver.Launched.Should().ContainSingle().Which.Should().Be("MEmu");
        device.RestartedPackages.Should().ContainSingle()
            .Which.Should().Be("com.supercell.clashofclans");
    }

    // Profile trỏ BlueStacks5 nhưng chỉ MEmu đang chạy → không dùng nhầm MEmu.
    [Fact]
    public async Task EnsureRunningAsync_Should_Not_Reuse_Another_Emulator_Type()
    {
        var driver = new FakeEmulatorDriver();
        driver.Running.Add(MemuRunning());
        driver.Installed.Add(new EmulatorInstallation(
            "BlueStacks5",
            "Pie64",
            @"C:\Program Files\BlueStacks_nxt\HD-Player.exe",
            5555));

        var runner = new MockProcessRunner();
        runner.QueueResult(0, "connected");
        runner.QueueResult(0, "device");
        runner.QueueResult(0, "1");

        var startup = CreateStartup(driver, runner, new FakeAndroidDeviceController());

        EmulatorStartupResult result = await startup.EnsureRunningAsync(
            "C:\\adb.exe", "127.0.0.1:5555", "BlueStacks5", "Pie64");

        result.Ready.Should().BeTrue();
        result.Launched.Should().BeTrue();
        result.EmulatorType.Should().Be("BlueStacks5");
        driver.Launched.Should().ContainSingle().Which.Should().Be("Pie64");
    }

    // Không có giả lập nào chạy hoặc đã cài → báo lỗi rõ ràng.
    [Fact]
    public async Task EnsureRunningAsync_Should_Fail_When_Nothing_Found()
    {
        var driver = new FakeEmulatorDriver();
        var startup = CreateStartup(driver, new MockProcessRunner(), new FakeAndroidDeviceController());

        EmulatorStartupResult result = await startup.EnsureRunningAsync(
            AdbPath, AdbSerial, "MEmu", "MEmu");

        result.Ready.Should().BeFalse();
        result.Reason.Should().Contain("Không tìm thấy giả lập");
    }

    // Launch thất bại → dừng ngay, không chờ ADB.
    [Fact]
    public async Task EnsureRunningAsync_Should_Fail_When_Launch_Fails()
    {
        var driver = new FakeEmulatorDriver { LaunchResult = false };
        driver.Installed.Add(MemuInstalled());

        var runner = new MockProcessRunner();
        var startup = CreateStartup(driver, runner, new FakeAndroidDeviceController());

        EmulatorStartupResult result = await startup.EnsureRunningAsync(
            AdbPath, AdbSerial, "MEmu", "MEmu");

        result.Ready.Should().BeFalse();
        result.Reason.Should().Contain("Không khởi động được");
        runner.RunCount.Should().Be(0);
    }

    // ADB không lên trong thời gian chờ → fail, chưa mở CoC.
    [Fact]
    public async Task EnsureRunningAsync_Should_Fail_When_Adb_Never_Ready()
    {
        var driver = new FakeEmulatorDriver();
        driver.Installed.Add(MemuInstalled());

        var runner = new MockProcessRunner();
        runner.QueueResult(0, "connected");
        for (int i = 0; i < 4; i++)
        {
            runner.QueueResult(0, "offline");
        }

        var device = new FakeAndroidDeviceController();
        var startup = CreateStartup(
            driver,
            runner,
            device,
            new EmulatorStartupOptions { AdbReadyTimeoutMs = 3000, PollIntervalMs = 1000 });

        EmulatorStartupResult result = await startup.EnsureRunningAsync(
            AdbPath, AdbSerial, "MEmu", "MEmu");

        result.Ready.Should().BeFalse();
        result.Reason.Should().Contain("ADB không sẵn sàng");
        device.RestartCount.Should().Be(0);
    }

    // ADB lên nhưng Android chưa boot xong → fail.
    [Fact]
    public async Task EnsureRunningAsync_Should_Fail_When_Boot_Never_Completes()
    {
        var driver = new FakeEmulatorDriver();
        driver.Installed.Add(MemuInstalled());

        var runner = new MockProcessRunner();
        runner.QueueResult(0, "connected");
        runner.QueueResult(0, "device");
        runner.QueueResult(0, "0");
        runner.QueueResult(0, "0");

        var device = new FakeAndroidDeviceController();
        var startup = CreateStartup(
            driver,
            runner,
            device,
            new EmulatorStartupOptions { BootTimeoutMs = 2000, PollIntervalMs = 1000 });

        EmulatorStartupResult result = await startup.EnsureRunningAsync(
            AdbPath, AdbSerial, "MEmu", "MEmu");

        result.Ready.Should().BeFalse();
        result.Reason.Should().Contain("sys.boot_completed");
        device.RestartCount.Should().Be(0);
    }

    [Fact]
    public async Task EnsureRunningAsync_Should_Throw_When_Cancelled()
    {
        var startup = CreateStartup(
            new FakeEmulatorDriver(),
            new MockProcessRunner(),
            new FakeAndroidDeviceController());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = () => startup.EnsureRunningAsync(
            AdbPath, AdbSerial, "MEmu", "MEmu", cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private static EmulatorStartup CreateStartup(
        FakeEmulatorDriver driver,
        IProcessRunner runner,
        IAndroidDeviceController device,
        EmulatorStartupOptions? options = null)
    {
        return new EmulatorStartup(
            driver,
            runner,
            new Mock<IBotLogger>().Object,
            new FakeTimeProvider(),
            _ => device,
            options);
    }

    private static EmulatorInstance MemuRunning(string title = "MEmu")
    {
        return new EmulatorInstance(
            Id: "1234",
            Name: $"MEmu - {title}",
            AdbPort: 21503,
            WindowHandle: new IntPtr(0x1234),
            EmulatorType: "MEmu");
    }

    private static EmulatorInstallation MemuInstalled()
    {
        return new EmulatorInstallation(
            "MEmu",
            "MEmu",
            @"F:\Microvirt\MEmu\MEmu.exe",
            21503);
    }
}
