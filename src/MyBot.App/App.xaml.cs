using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MyBot.UI;

namespace MyBot.App;

/// <summary>
/// Entry point của ứng dụng WPF MyBot.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = MyBot.App.Startup.ConfigureServices();
        var mainWindow = services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }
}
