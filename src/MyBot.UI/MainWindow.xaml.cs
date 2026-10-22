using System.Windows;
using System.Windows.Input;
using MyBot.UI.ViewModels;

namespace MyBot.UI;

/// <summary>
/// Cửa sổ giao diện chính của MyBot.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// Khởi tạo mặc định cho designer và XAML loader.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Khởi tạo MainWindow với ViewModel được inject qua Dependency Injection.
    /// </summary>
    /// <param name="viewModel">ViewModel chính điều phối trạng thái và logic giao diện.</param>
    public MainWindow(MainViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }
}