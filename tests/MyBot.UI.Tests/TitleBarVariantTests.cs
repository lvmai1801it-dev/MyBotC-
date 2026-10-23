using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using MyBot.UI.Gui;

namespace MyBot.UI.Tests;

/// <summary>
/// Biến thể thanh tiêu đề theo <c>general.botDesignFlags</c>: bit 0 bật thì dùng
/// thanh tự vẽ với <c>$_GUI_MAIN_TOP = 23</c>; tắt thì cửa sổ dùng caption của
/// Windows và toàn bộ nội dung dịch lên <c>$_GUI_MAIN_TOP = 5</c>.
/// </summary>
public sealed class TitleBarVariantTests
{
    [Fact]
    public void Bit0_bat_thi_giu_thanh_tieu_de_tu_ve()
    {
        Assert.Equal(new TitleBarVariant(true, 23), TitleBarVariant.FromDesignFlags(3));

        LayoutSnapshot layout = RunOnSta(() => Snapshot(TitleBarVariant.DefaultDesignFlags));

        Assert.Equal(Visibility.Visible, layout.TitleBarVisibility);
        Assert.Equal(WindowStyle.None, layout.WindowStyle);
        Assert.Equal(23d, layout.LogoTop);
        Assert.Equal(90d, layout.UrlTop);
        Assert.Equal(108d, layout.TabsTop);
        Assert.Equal(580d, layout.BottomTop);
        Assert.Equal(715d, layout.RootHeight);
        Assert.Equal(715d, layout.WindowHeight);
    }

    [Fact]
    public void Bit0_tat_thi_dung_caption_windows_va_dich_len_18px()
    {
        TitleBarVariant variant = TitleBarVariant.FromDesignFlags(0);
        Assert.False(variant.CustomTitleBar);
        Assert.Equal(5d, variant.MainTop);
        Assert.Equal(-18d, variant.ContentOffset);
        Assert.Equal(697d, variant.ClientHeight);

        LayoutSnapshot layout = RunOnSta(() => Snapshot(0));

        Assert.Equal(Visibility.Collapsed, layout.TitleBarVisibility);
        Assert.Equal(WindowStyle.SingleBorderWindow, layout.WindowStyle);
        Assert.Equal(5d, layout.LogoTop);
        Assert.Equal(72d, layout.UrlTop);
        Assert.Equal(90d, layout.TabsTop);
        Assert.Equal(562d, layout.BottomTop);
        Assert.Equal(697d, layout.RootHeight);

        // Height của WPF tính cả caption và viền trên/dưới của cửa sổ.
        double expected = 697 + SystemParameters.WindowCaptionHeight
                              + (SystemParameters.ResizeFrameVerticalBorderWidth * 2);
        Assert.Equal(expected, layout.WindowHeight);
    }

    private static LayoutSnapshot Snapshot(int botDesignFlags)
    {
        var window = new MainWindow { BotDesignFlags = botDesignFlags };

        return new LayoutSnapshot(
            ((FrameworkElement)window.FindName("TitleBarHost")!).Visibility,
            window.WindowStyle,
            Canvas.GetTop((UIElement)window.FindName("LogoImage")!),
            Canvas.GetTop((UIElement)window.FindName("LogoUrlImage")!),
            Canvas.GetTop((UIElement)window.FindName("TabsHost")!),
            Canvas.GetTop((UIElement)window.FindName("BottomHost")!),
            ((FrameworkElement)window.FindName("RootCanvas")!).Height,
            window.Height);
    }

    private sealed record LayoutSnapshot(
        Visibility TitleBarVisibility,
        WindowStyle WindowStyle,
        double LogoTop,
        double UrlTop,
        double TabsTop,
        double BottomTop,
        double RootHeight,
        double WindowHeight);

    /// <summary>WPF yêu cầu STA để tạo phần tử giao diện.</summary>
    private static T RunOnSta<T>(Func<T> action)
    {
        T result = default!;
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            throw new InvalidOperationException("Dựng GUI thất bại trên luồng STA.", failure);
        }

        return result;
    }
}
