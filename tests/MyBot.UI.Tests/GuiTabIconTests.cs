using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using MyBot.Localization;
using MyBot.UI.Gui;
using MyBot.UI.Icons;

namespace MyBot.UI.Tests;

/// <summary>
/// Icon tab phải khớp image list của AutoIt (<c>Bind_ImageList</c>): mọi icon bị ép
/// về 16×16, tab nào cũng có icon và chỉ số icon đúng theo từng tab control.
/// </summary>
public sealed class GuiTabIconTests
{
    [Fact]
    public void Icon_tab_dung_16x16()
    {
        Assert.Equal(16, GuiTabHost.TabIconSize);

        (double Width, double Height, int ImageCount) = RunOnSta(() =>
        {
            var host = new GuiTabHost(200, 100);
            host.AddTab("Mot", () => new Canvas(), 0, 0, IconId.eIcnMisc);
            host.AddTab("Hai", () => new Canvas());

            var header = (Border)host.Headers[0];
            var pictures = ((Panel)header.Child).Children.OfType<Image>().ToList();

            // Tab không gắn icon thì tiêu đề chỉ có chữ, không có ô ảnh rỗng.
            var plainHeader = (Border)host.Headers[1];

            return (
                pictures.Single().Width,
                pictures.Single().Height,
                pictures.Count + (plainHeader.Child is TextBlock ? 0 : 1));
        });

        Assert.Equal(16d, Width);
        Assert.Equal(16d, Height);
        Assert.Equal(1, ImageCount);
    }

    [Fact]
    public void Moi_tab_co_icon_va_dung_chi_so()
    {
        Dictionary<string, IconId?[]> actual = RunOnSta(CollectTabHosts);

        var expected = new Dictionary<string, IconId?[]>(StringComparer.Ordinal)
        {
            // parity: Bind_ImageList @ COCBot/MBR GUI Control.au3:2053
            ["Main"] = new IconId?[]
            {
                IconId.eIcnHourGlass, IconId.eIcnVillager, IconId.eIcnAttack,
                IconId.eIcnGUI, IconId.eIcnGoldElixir, IconId.eIcnInfo,
            },
            ["Main/1"] = new IconId?[]
            {
                IconId.eIcnMisc, IconId.eIcnCC, IconId.eIcnLaboratory, IconId.eIcnTelegram,
            },
            ["Main/1/0"] = new IconId?[]
            {
                IconId.eIcnTH, IconId.eIcnBuilderHall, IconId.eIcnStrongMan, IconId.eIcnClanCapital,
            },
            ["Main/1/1"] = new IconId?[]
            {
                IconId.eIcnCCRequest, IconId.eIcnCCDonate, IconId.eIcnHourGlass,
            },
            ["Main/1/2"] = new IconId?[]
            {
                IconId.eIcnBuilderHut, IconId.eIcnBarrack, IconId.eIcnHeroes,
                IconId.eIcnLaboratory, IconId.eIcnWall,
            },
            ["Main/2"] = new IconId?[]
            {
                // $eIcnStrategies = $eIcnBlank
                IconId.eIcnTrain, IconId.eIcnMagnifier, IconId.eIcnBlank,
            },
            ["Main/2/0"] = new IconId?[] { IconId.eIcnCamp, IconId.eIcnSettings },
            ["Main/2/1"] = new IconId?[]
            {
                IconId.eIcnDeadBase, IconId.eIcnActiveBase, IconId.eIcnSettings,
            },
            ["Main/2/1/0"] = new IconId?[]
            {
                IconId.eIcnMagnifier, IconId.eIcnMainAttack, IconId.eIcnCollector,
            },
            ["Main/2/1/1"] = new IconId?[]
            {
                IconId.eIcnMagnifier, IconId.eIcnMainAttack,
            },
            ["Main/2/1/2"] = new IconId?[]
            {
                IconId.eIcnMagnifier, IconId.eIcnHeroes, IconId.eIcnSilverStar,
            },
            ["Main/3"] = new IconId?[]
            {
                // $eIcnOptions = $eIcnMisc; tab thứ 5 của AutoIt không tồn tại.
                IconId.eIcnMisc, IconId.eIcnAndroid, IconId.eIcnProfile, IconId.eIcnProfile,
            },
            ["Main/4"] = new IconId?[]
            {
                IconId.eIcnGoldElixir, IconId.eIcnGeneral, IconId.eIcnCC, IconId.eIcnMultiStats,
            },
        };

        Assert.Equal(expected.Count, actual.Count);
        foreach ((string path, IconId?[] icons) in expected)
        {
            Assert.True(actual.TryGetValue(path, out IconId?[]? real), $"Thiếu tab control '{path}'.");
            Assert.Equal(icons, real);
        }
    }

    /// <summary>
    /// Duyệt cây tab của cửa sổ chính, ghi lại icon của từng tab control theo đường dẫn
    /// chỉ số tab. Nội dung tab được dựng ở lần chọn đầu tiên (giống lúc chạy thật).
    /// </summary>
    private static Dictionary<string, IconId?[]> CollectTabHosts()
    {
        GuiSpecCatalog catalog = GuiSpecCatalog.Load();
        var translator = new IniFileTranslator(Path.Combine(AppContext.BaseDirectory, "Languages"));
        using var icons = new MbrBotIconProvider(Path.Combine(AppContext.BaseDirectory, "lib", "MBRBOT.dll"));

        var builder = new MainGuiBuilder(catalog, translator, icons);
        var root = (GuiTabHost)builder.BuildMainTabs();

        var found = new Dictionary<string, IconId?[]>(StringComparer.Ordinal);
        Collect(root, "Main", found);

        // "số icon = số tab": tab nào cũng phải có icon như image list trong AutoIt.
        foreach ((string path, IconId?[] iconsOfHost) in found)
        {
            Assert.DoesNotContain(null, iconsOfHost);
            Assert.Equal(TabCount(root, path), iconsOfHost.Length);
        }

        return found;
    }

    private static void Collect(GuiTabHost host, string path, Dictionary<string, IconId?[]> found)
    {
        found[path] = host.TabIcons.ToArray();

        for (int i = 0; i < host.TabCount; i++)
        {
            host.Select(i);
            if (ContentOf(host) is GuiTabHost child)
            {
                Collect(child, $"{path}/{i}", found);
            }
        }
    }

    /// <summary>Đếm số tab của host tại <paramref name="path"/> (đi theo chỉ số tab).</summary>
    private static int TabCount(GuiTabHost root, string path)
    {
        GuiTabHost host = root;
        foreach (string part in path.Split('/').Skip(1))
        {
            int index = int.Parse(part);
            host.Select(index);
            host = (GuiTabHost)ContentOf(host)!;
        }

        return host.TabCount;
    }

    private static FrameworkElement? ContentOf(GuiTabHost host)
    {
        // GuiTabHost là Grid hai con: [0] canvas nội dung, [1] dải tiêu đề.
        Canvas? content = host.Children.OfType<Canvas>().FirstOrDefault();
        return content?.Children.OfType<FrameworkElement>().FirstOrDefault();
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
