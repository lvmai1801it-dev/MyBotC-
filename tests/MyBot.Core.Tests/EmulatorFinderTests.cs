using FluentAssertions;
using MyBot.Core.Capture;

namespace MyBot.Core.Tests;

public class EmulatorFinderTests
{
    // MEmu bản cũ: top-level dùng lớp subWin, chính nó là cửa sổ render.
    [Fact]
    public void MatchEmulator_Should_Recognize_Legacy_MEmu_Window()
    {
        EmulatorFinder.MatchEmulator("subWin", "MEmu").Should().Be("MEmu");
    }

    // MEmu 9: cửa sổ Qt top-level, cửa sổ render là con "RenderWindowWindow".
    [Fact]
    public void MatchEmulator_Should_Recognize_MEmu_Qt_Window()
    {
        EmulatorFinder.MatchEmulator("Qt5152QWindowIcon", "MEmu").Should().Be("MEmu");
    }

    // Cửa sổ dịch vụ của MEmu không được nhận nhầm là giả lập.
    [Fact]
    public void MatchEmulator_Should_Ignore_MEmu_Service_Window()
    {
        EmulatorFinder.MatchEmulator("MEmuPowerNotifyClass", "MEmuPowerNotifyClass")
            .Should().BeNull();
    }

    // Cửa sổ Qt không có tiêu đề MEmu (ví dụ cửa sổ phụ) không khớp.
    [Fact]
    public void MatchEmulator_Should_Ignore_Other_Qt_Window()
    {
        EmulatorFinder.MatchEmulator("Qt5152QWindowIcon", "Tools").Should().BeNull();
    }

    [Fact]
    public void MatchEmulator_Should_Recognize_BlueStacks5()
    {
        EmulatorFinder.MatchEmulator("BlueStacksApp", "BlueStacks App Player")
            .Should().Be("BlueStacks5");
    }

    [Theory]
    [InlineData("Qt5152QWindowIcon", true)]
    [InlineData("qt6QWindowIcon", true)]
    [InlineData("subWin", false)]
    [InlineData("BlueStacksApp", false)]
    public void IsQtClass_Should_Detect_Qt_Window_Classes(string className, bool expected)
    {
        EmulatorFinder.IsQtClass(className).Should().Be(expected);
    }
}
