using System;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;
using MyBot.UI.Views;

namespace MyBot.UI.Tests;

/// <summary>
/// Panel đáy phải có status bar như bản AutoIt: một ô chạy hết chiều ngang, neo ở
/// đáy cửa sổ 472x135, đè lên phần trống cuối panel và hiển thị trạng thái bot.
/// </summary>
public sealed class BottomStatusBarTests
{
    [Fact]
    public void Status_bar_neo_day_rong_23px()
    {
        (double Width, double Height, VerticalAlignment Align, int ZIndex, Visibility Visibility, double HostHeight) =
            RunOnSta(() =>
            {
                var view = new BottomPanelView();
                var bar = (Border)view.FindName("StatusBar")!;
                var host = (Canvas)view.FindName("Host")!;

                return (bar.Width, bar.Height, bar.VerticalAlignment, Panel.GetZIndex(bar), bar.Visibility, host.Height);
            });

        Assert.Equal(472d, Width);
        Assert.Equal(23d, Height);
        Assert.Equal(VerticalAlignment.Bottom, Align);
        Assert.Equal(Visibility.Visible, Visibility);

        // Status bar của Win32 đè lên 23 px cuối của cửa sổ đáy, không đẩy nội dung lên.
        Assert.True(ZIndex > 0);
        Assert.Equal(135d, HostHeight);
    }

    [Fact]
    public void Status_bar_hien_thi_StatusText_cua_ViewModel()
    {
        (string Initial, string Updated, string? BindingPath) = RunOnSta(() =>
        {
            var view = new BottomPanelView();
            var text = (TextBlock)view.FindName("StatusBarText")!;

            var stub = new StatusStub { StatusText = "Status : Idle" };
            view.DataContext = stub;
            FlushBindings();
            string initial = text.Text;

            stub.StatusText = "Status : Searching";
            FlushBindings();
            string updated = text.Text;

            string? path = BindingOperations.GetBinding(text, TextBlock.TextProperty)?.Path.Path;
            return (initial, updated, path);
        });

        Assert.Equal("Status : Idle", Initial);
        Assert.Equal("Status : Searching", Updated);
        Assert.Equal("StatusText", BindingPath);
    }

    /// <summary>Đẩy các phép binding đang chờ trên dispatcher (control chưa vào cây hiển thị).</summary>
    private static void FlushBindings()
    {
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
    }

    private sealed class StatusStub : INotifyPropertyChanged    {
        private string _statusText = string.Empty;

        public string StatusText
        {
            get => _statusText;
            set
            {
                _statusText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusText)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

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
