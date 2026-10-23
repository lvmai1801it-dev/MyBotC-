using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using MyBot.Localization;
using MyBot.UI.Gui;
using MyBot.UI.Icons;
using MyBot.UI.ViewModels;
using MyBot.UI.Views;

namespace MyBot.UI;

/// <summary>
/// Cửa sổ giao diện chính của MyBot (472x715).
///
/// Bố cục lấy từ <c>MBR GUI Design.au3</c>: thanh tiêu đề tự vẽ, ảnh logo, tab
/// control chính và panel đáy. Phần nội dung tab do <see cref="MainGuiBuilder"/>
/// dựng từ <c>gui-spec.json</c> (sinh ra từ chính bản AutoIt).
/// </summary>
public partial class MainWindow : Window
{
    private const string BotTitle = "MyBot.run v15.3";

    private int _botDesignFlags = TitleBarVariant.DefaultDesignFlags;

    /// <summary>Khởi tạo cho XAML designer và XAML loader.</summary>
    public MainWindow()
    {
        InitializeComponent();

        LoadTitleImages();
        TitleText.Text = BotTitle;
        Title = BotTitle;

        ApplyTitleBarVariant(TitleBarVariant.FromDesignFlags(_botDesignFlags));
    }

    /// <summary>
    /// Giá trị <c>general.botDesignFlags</c> trong config — quyết định thanh tiêu đề
    /// tự vẽ (bit 0 = 1) hay thanh tiêu đề mặc định của Windows (bit 0 = 0).
    ///
    /// parity: CreateMainGUI @ COCBot/MBR GUI Design Mini.au3:126. Mặc định 3 như
    /// profile mới của bản AutoIt; <c>AppConfig</c> chưa có trường này nên nơi đọc
    /// config sẽ gán sau (xem ghi chú trong PROGRESS.md).
    /// </summary>
    public int BotDesignFlags
    {
        get => _botDesignFlags;
        set
        {
            _botDesignFlags = value;
            ApplyTitleBarVariant(TitleBarVariant.FromDesignFlags(value));
        }
    }

    /// <summary>Khởi tạo MainWindow với các dịch vụ được inject qua Dependency Injection.</summary>
    /// <param name="viewModel">ViewModel chính điều phối trạng thái và logic giao diện.</param>
    /// <param name="catalog">Bộ mô tả màn hình GUI sinh từ bản AutoIt.</param>
    /// <param name="translator">Bộ dịch dùng cho nhãn và tiêu đề tab.</param>
    /// <param name="icons">Nguồn icon trong <c>lib/MBRBOT.dll</c>.</param>
    public MainWindow(
        MainViewModel viewModel,
        GuiSpecCatalog catalog,
        IMyBotTranslator translator,
        MbrBotIconProvider icons) : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(translator);
        ArgumentNullException.ThrowIfNull(icons);

        DataContext = viewModel;

        var builder = new MainGuiBuilder(catalog, translator, icons);

        TabsHost.Children.Add(builder.BuildMainTabs());
        BottomHost.Content = new BottomPanelView(builder.BuildBottomPanel(), viewModel);
    }

    /// <summary>
    /// Áp bố cục theo biến thể thanh tiêu đề: ẩn thanh tự vẽ và dịch toàn bộ nội dung
    /// lên <c>$_GUI_MAIN_TOP</c> mới khi cửa sổ dùng caption của Windows.
    /// </summary>
    private void ApplyTitleBarVariant(TitleBarVariant variant)
    {
        TitleBarHost.Visibility = variant.CustomTitleBar ? Visibility.Visible : Visibility.Collapsed;

        double offset = variant.ContentOffset;
        Canvas.SetTop(LogoImage, 23 + offset);
        Canvas.SetTop(LogoUrlImage, 90 + offset);
        Canvas.SetTop(TabsHost, 108 + offset);
        Canvas.SetTop(BottomHost, 580 + offset);
        RootCanvas.Height = variant.ClientHeight;

        if (variant.CustomTitleBar)
        {
            WindowStyle = WindowStyle.None;
            Height = variant.ClientHeight;
            return;
        }

        // Caption của Windows nằm ngoài vùng client nên Height của WPF phải cộng thêm.
        WindowStyle = WindowStyle.SingleBorderWindow;
        Height = variant.ClientHeight
                 + SystemParameters.WindowCaptionHeight
                 + (SystemParameters.ResizeFrameVerticalBorderWidth * 2);
    }

    /// <summary>Nạp ảnh logo và dải URL; thiếu file thì bỏ trống (không chặn khởi động).</summary>
    private void LoadTitleImages()
    {
        LogoImage.Source = TryLoadImage("Logo.png");
        LogoUrlImage.Source = TryLoadImage("LogoURL.png");
    }

    private static BitmapImage? TryLoadImage(string fileName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "images", fileName);
        if (!File.Exists(path))
        {
            return null;
        }

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
                // Chuột đã nhả trước khi DragMove kịp chạy — bỏ qua.
            }
        }
    }

    private void BtnMinimize_Click(object sender, MouseButtonEventArgs e)
    {
        WindowState = WindowState.Minimized;
        e.Handled = true;
    }

    private void BtnClose_Click(object sender, MouseButtonEventArgs e)
    {
        Application.Current.Shutdown();
        e.Handled = true;
    }
}
