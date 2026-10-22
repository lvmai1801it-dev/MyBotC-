using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace MyBot.UI.Views;

/// <summary>
/// UserControl cho Tab About (thông tin dự án, phiên bản, giấy phép và liên kết GitHub).
/// </summary>
public partial class AboutTabView : UserControl
{
    public AboutTabView()
    {
        InitializeComponent();
    }

    private void OpenGitHub_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/lvmai1801it-dev/MyBotC-",
            UseShellExecute = true
        });
    }
}
