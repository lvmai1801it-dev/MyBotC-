using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MyBot.Localization;
using MyBot.UI.Gui;
using MyBot.UI.Icons;

namespace MyBot.UI.Tests;

/// <summary>
/// Kiểm tra bộ mô tả GUI sinh từ bản AutoIt: tài nguyên nhúng phải nạp được và
/// mọi màn hình phải dựng xong mà không ném ngoại lệ.
/// </summary>
public sealed class GuiSpecSmokeTests
{
    [Fact]
    public void Catalog_chua_du_40_man_hinh()
    {
        GuiSpecCatalog catalog = GuiSpecCatalog.Load();

        Assert.Equal(40, catalog.Count);
        Assert.Contains("Bottom", catalog.Ids);
        Assert.Contains("Village.Misc.NormalVillage", catalog.Ids);
        Assert.Contains("Attack.TrainArmy.Army", catalog.Ids);
    }

    [Fact]
    public void Moi_man_hinh_dung_du_so_control()
    {
        List<(string Id, int Expected, int Actual)> results = RunOnSta(DrawAllScreens);

        Assert.NotEmpty(results);
        foreach ((string id, int expected, int actual) in results)
        {
            Assert.True(expected == actual, $"Màn hình '{id}': dựng {actual}/{expected} control.");
        }
    }

    [Fact]
    public void Combo_tach_danh_sach_theo_dau_gach_dung()
    {
        // Khoá "CmbScriptRedlineImpl" trong English.ini chứa cả danh sách
        // ("a|b|c|d"), nên combo phải ra 4 mục chứ không phải một chuỗi dài.
        int itemCount = RunOnSta(() =>
        {
            GuiSpecCatalog catalog = GuiSpecCatalog.Load();
            GuiScreenSpec screen = catalog.Get("Attack.Search.ActiveBase.Attack.Scripted");

            var translator = new IniFileTranslator(Path.Combine(AppContext.BaseDirectory, "Languages"));
            using var icons = new MbrBotIconProvider(Path.Combine(AppContext.BaseDirectory, "lib", "MBRBOT.dll"));
            var factory = new GuiControlFactory(translator, icons, "rad_combo");

            GuiControlSpec spec = screen.Controls.First(c => c.Variable == "$g_hCmbScriptRedlineImplAB");
            var combo = (ComboBox)factory.Create(spec);

            return combo.Items.Count;
        });

        Assert.Equal(4, itemCount);
    }

    [Fact]
    public void Bam_tieu_de_tab_thi_chon_tab_do()
    {
        int selected = RunOnSta(() =>
        {
            var host = new GuiTabHost(200, 100);
            host.AddTab("Mot", () => new Canvas());
            host.AddTab("Hai", () => new Canvas());
            host.SelectFirst();

            // GuiTabHost là Grid hai con: [0] canvas nội dung, [1] dải tiêu đề.
            var headers = (StackPanel)((Grid)host).Children[1];
            var second = (Border)headers.Children[1];

            second.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
            {
                RoutedEvent = UIElement.MouseLeftButtonDownEvent,
            });

            return host.SelectedIndex;
        });

        Assert.Equal(1, selected);
    }

    private static List<(string Id, int Expected, int Actual)> DrawAllScreens()
    {
        GuiSpecCatalog catalog = GuiSpecCatalog.Load();

        // Không có file ngôn ngữ/icon DLL trong thư mục test: bộ dịch rơi về từ điển
        // English nhúng sẵn, provider icon trả null. Đúng thứ cần cho smoke test.
        var translator = new IniFileTranslator(Path.Combine(AppContext.BaseDirectory, "Languages"));
        using var icons = new MbrBotIconProvider(Path.Combine(AppContext.BaseDirectory, "lib", "MBRBOT.dll"));

        var results = new List<(string Id, int Expected, int Actual)>();
        foreach (string id in catalog.Ids)
        {
            GuiScreenSpec spec = catalog.Get(id);
            var factory = new GuiControlFactory(translator, icons, "rad_" + id);
            var view = new GuiScreenView(spec, factory);

            results.Add((id, spec.Controls.Count, view.ControlCount));
        }

        return results;
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
