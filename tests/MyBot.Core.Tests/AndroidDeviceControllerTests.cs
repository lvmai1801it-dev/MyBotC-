using FluentAssertions;
using MyBot.Core.Capture;
using MyBot.Core.Tests.Mocks;

namespace MyBot.Core.Tests;

public class AndroidDeviceControllerTests
{
    private const string Serial = "127.0.0.1:21503";
    private const string Package = "com.supercell.clashofclans";

    // Restart CoC = force-stop rồi mở lại qua monkey (CloseCoC + OpenCoC).
    [Fact]
    public void RestartApp_Should_ForceStop_Then_Launch()
    {
        var mock = new MockProcessRunner();
        mock.QueueResult(0, string.Empty);
        mock.QueueResult(0, string.Empty);

        var controller = new AdbAndroidDeviceController("adb", mock, Serial);

        bool result = controller.RestartApp(Package);

        result.Should().BeTrue();
        mock.LastArguments.Should().HaveCount(2);
        mock.LastArguments[0].Arguments.Should().Be($"-s {Serial} shell am force-stop {Package}");
        mock.LastArguments[1].Arguments.Should().Contain("monkey")
            .And.Contain(Package);
    }

    // Launch thất bại → controller trả false để recovery log cảnh báo.
    [Fact]
    public void RestartApp_Should_Return_False_When_Launch_Fails()
    {
        var mock = new MockProcessRunner();
        mock.QueueResult(0, string.Empty);
        mock.QueueResult(1, "error");

        var controller = new AdbAndroidDeviceController("adb", mock, Serial);

        controller.RestartApp(Package).Should().BeFalse();
    }

    // Reboot dùng đúng serial của giả lập.
    [Fact]
    public void RebootDevice_Should_Use_Configured_Serial()
    {
        var mock = new MockProcessRunner();
        mock.QueueResult(0, string.Empty);

        var controller = new AdbAndroidDeviceController("adb", mock, Serial);

        controller.RebootDevice().Should().BeTrue();
        mock.LastArguments.Should().HaveCount(1);
        mock.LastArguments[0].Arguments.Should().Be($"-s {Serial} reboot");
    }

    // ADB ném lỗi (mất kết nối) → trả false thay vì làm sập vòng phục hồi.
    [Fact]
    public void RebootDevice_Should_Return_False_When_Adb_Throws()
    {
        var mock = new MockProcessRunner();

        var controller = new AdbAndroidDeviceController("adb", mock, Serial);

        controller.RebootDevice().Should().BeFalse();
    }
}
