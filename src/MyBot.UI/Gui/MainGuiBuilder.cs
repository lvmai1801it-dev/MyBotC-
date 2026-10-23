using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using MyBot.Localization;
using MyBot.UI.Icons;
using MyBot.UI.ViewModels;

namespace MyBot.UI.Gui;

/// <summary>
/// Dựng cây tab của cửa sổ chính, đúng cấu trúc lồng nhau của bản AutoIt.
///
/// Sơ đồ phân cấp (mỗi cửa sổ con được đặt tại offset ghi ở cột phải):
/// <code>
/// main 463x467 @ (5,108)          — 6 tab cấp 1
///   ├─ Log / About                452x437 @ (5,25)
///   ├─ Village 452x437             — tab tại (0,0), cửa sổ con @ (5,25)
///   │    ├─ Misc 442x407           — trang 442x407 @ (0,0)
///   │    ├─ Clan Castle 442x407    — trang 442x407 @ (0,0)
///   │    ├─ Upgrade 442x407        — trang 442x407 @ (0,0)
///   │    └─ Notify                 — trang 442x407 @ (0,0)
///   ├─ Attack 452x437              — cửa sổ con @ (5,25)
///   │    ├─ Train Army 442x407     — cửa sổ con 434x377 @ (5,25)
///   │    ├─ Search 442x407         — cửa sổ con 434x377 @ (5,25)
///   │    └─ Strategies             — trang 442x407 @ (0,0)
///   ├─ Bot 452x437                 — trang 452x437 @ (0,0)
///   └─ Stats 452x437               — trang 452x437 @ (0,0)
/// </code>
/// Toạ độ lấy trực tiếp từ các lời gọi <c>_GUICreate</c> trong
/// <c>COCBot/GUI/MBR GUI Design*.au3</c>; xem thêm <c>docs/context/06_DAC_TA_THONG_SO_GUI_PORT_CSHARP.md</c>.
/// </summary>
public sealed class MainGuiBuilder
{
    /// <summary>Kích thước tab control cấp 1 (<c>GUICtrlCreateTab(5, 108, 463, 467)</c>).</summary>
    public const double MainTabWidth = 463;

    /// <summary>Chiều cao tab control cấp 1.</summary>
    public const double MainTabHeight = 467;

    /// <summary>Toạ độ trái của tab control cấp 1 trong cửa sổ chính.</summary>
    public const double MainTabLeft = 5;

    /// <summary>Toạ độ trên của tab control cấp 1 trong cửa sổ chính.</summary>
    public const double MainTabTop = 108;

    /// <summary>Mục ngôn ngữ chứa toàn bộ tiêu đề tab.</summary>
    private const string MainSection = "MBR Main GUI";

    // Bộ kích thước cửa sổ con theo cấp (MBR GUI Design.au3: $g_iSizeWGrpTab1..3).
    private const double SizeTab1Width = 452;
    private const double SizeTab1Height = 437;
    private const double SizeTab2Width = 442;
    private const double SizeTab2Height = 407;
    private const double SizeTab3Width = 434;
    private const double SizeTab3Height = 377;

    // Cửa sổ con luôn được đặt ở $_GUI_CHILD_LEFT / so với cửa sổ cha.
    private const double ChildLeft = 5;
    private const double ChildTop = 25;

    private readonly GuiSpecCatalog _catalog;
    private readonly IMyBotTranslator _translator;
    private readonly MbrBotIconProvider _icons;

    /// <summary>Khởi tạo builder với dữ liệu màn hình, bộ dịch và nguồn icon.</summary>
    public MainGuiBuilder(GuiSpecCatalog catalog, IMyBotTranslator translator, MbrBotIconProvider icons)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _translator = translator ?? throw new ArgumentNullException(nameof(translator));
        _icons = icons ?? throw new ArgumentNullException(nameof(icons));
    }

    /// <summary>Dựng tab control cấp 1 (463x467) chứa cả 6 tab chính.</summary>
    public FrameworkElement BuildMainTabs()
    {
        // parity: Bind_ImageList @ COCBot/MBR GUI Control.au3:2064 — icon tab cấp 1
        // [$eIcnHourGlass, $eIcnVillager, $eIcnAttack, $eIcnGUI, $eIcnGoldElixir, $eIcnInfo].
        var main = new GuiTabHost(MainTabWidth, MainTabHeight, _icons);

        main.AddTab(Tr("Tab_01", "Log"), BuildLogTab, ChildLeft, ChildTop, IconId.eIcnHourGlass);
        main.AddTab(Tr("Tab_02", "Village"), BuildVillageTab, ChildLeft, ChildTop, IconId.eIcnVillager);
        main.AddTab(Tr("Tab_03", "Attack Plan"), BuildAttackTab, ChildLeft, ChildTop, IconId.eIcnAttack);
        main.AddTab(Tr("Tab_04", "Bot"), BuildBotTab, ChildLeft, ChildTop, IconId.eIcnGUI);
        main.AddTab(Tr("Tab_04_STab_05", "Stats"), BuildStatsTab, ChildLeft, ChildTop, IconId.eIcnGoldElixir);
        main.AddTab(Tr("Tab_05", "About Us"), () => Screen("About"), ChildLeft, ChildTop, IconId.eIcnInfo);

        main.SelectFirst();
        return main;
    }

    /// <summary>Dựng panel đáy (472x135) gồm các nút Start/Stop/Pause và thông số làng.</summary>
    public GuiScreenView BuildBottomPanel() => Screen("Bottom");

    // ── Cấp 1 ────────────────────────────────────────────────────────────────────

    private FrameworkElement BuildVillageTab()
    {
        // parity: Bind_ImageList @ COCBot/MBR GUI Control.au3:2068
        // [$eIcnMisc, $eIcnCC, $eIcnLaboratory, $eIcnTelegram].
        var host = new GuiTabHost(SizeTab1Width, SizeTab1Height, _icons);

        host.AddTab(Tr("Tab_02_STab_01", "Misc"), BuildVillageMiscTab, ChildLeft, ChildTop, IconId.eIcnMisc);
        host.AddTab(Tr("Tab_02_STab_02", "Clan Castle"), BuildClanCastleTab, ChildLeft, ChildTop, IconId.eIcnCC);
        host.AddTab(Tr("Tab_02_STab_03", "Upgrade"), BuildUpgradeTab, ChildLeft, ChildTop, IconId.eIcnLaboratory);
        host.AddTab(Tr("Tab_02_STab_05", "Notify"), () => Screen("Village.Notify"), 0, 0, IconId.eIcnTelegram);

        host.SelectFirst();
        return host;
    }

    private FrameworkElement BuildAttackTab()
    {
        // parity: Bind_ImageList @ COCBot/MBR GUI Control.au3:2092
        // [$eIcnTrain, $eIcnMagnifier, $eIcnStrategies] — $eIcnStrategies = $eIcnBlank.
        var host = new GuiTabHost(SizeTab1Width, SizeTab1Height, _icons);

        host.AddTab(Tr("Tab_03_STab_01", "Train Army"), BuildTrainArmyTab, ChildLeft, ChildTop, IconId.eIcnTrain);
        host.AddTab(Tr("Tab_03_STab_02", "Search && Attack"), BuildSearchTab, ChildLeft, ChildTop, IconId.eIcnMagnifier);
        host.AddTab(Tr("Tab_03_STab_03", "Strategies"), () => Screen("Attack.Strategies"), 0, 0, IconId.eIcnBlank);

        host.SelectFirst();
        return host;
    }

    private FrameworkElement BuildBotTab()
    {
        // parity: Bind_ImageList @ COCBot/MBR GUI Control.au3:2112
        // [$eIcnOptions, $eIcnAndroid, $eIcnProfile, $eIcnProfile, $eIcnGold] —
        // $eIcnOptions = $eIcnMisc; tab thứ 5 không tồn tại nên chỉ dùng 4 icon đầu.
        var host = new GuiTabHost(SizeTab1Width, SizeTab1Height, _icons);

        host.AddTab(Tr("Tab_04_STab_01", "Options"), () => Screen("Bot.Options"), 0, 0, IconId.eIcnMisc);
        host.AddTab(Tr("Tab_04_STab_02", "Android"), () => Screen("Bot.Android"), 0, 0, IconId.eIcnAndroid);
        host.AddTab(Tr("Tab_04_STab_03", "Debug"), () => Screen("Bot.Debug"), 0, 0, IconId.eIcnProfile);
        host.AddTab(Tr("Tab_04_STab_04", "Profiles"), () => Screen("Bot.Profiles"), 0, 0, IconId.eIcnProfile);

        host.SelectFirst();
        return host;
    }

    private FrameworkElement BuildStatsTab()
    {
        // parity: Bind_ImageList @ COCBot/MBR GUI Control.au3:2121
        // [$eIcnGoldElixir, $eIcnGeneral, $eIcnCC, $eIcnMultiStats].
        var host = new GuiTabHost(SizeTab1Width, SizeTab1Height, _icons);

        host.AddTab(Tr("Tab_04_STab_05_STab_01", "Gain"), () => Screen("Stats.Gain"), 0, 0, IconId.eIcnGoldElixir);
        host.AddTab(Tr("Tab_04_STab_05_STab_02", "General"), () => Screen("Stats.General"), 0, 0, IconId.eIcnGeneral);
        host.AddTab(Tr("Tab_04_STab_05_STab_04", "Donations"), () => Screen("Stats.Donations"), 0, 0, IconId.eIcnCC);
        host.AddTab(Tr("Tab_04_STab_05_STab_05", "Multi Stats"), () => Screen("Stats.MultiStats"), 0, 0, IconId.eIcnMultiStats);

        host.SelectFirst();
        return host;
    }

    // ── Cấp 2 ────────────────────────────────────────────────────────────────────

    private FrameworkElement BuildVillageMiscTab()
    {
        // parity: Bind_ImageList @ COCBot/MBR GUI Control.au3:2077
        // [$eIcnTH, $eIcnBuilderHall, $eIcnStrongMan, $eIcnClanCapital].
        var host = new GuiTabHost(SizeTab2Width, SizeTab2Height, _icons);

        host.AddTab(Tr("MISC_TAB_ITEM1", "Normal Village"), () => Screen("Village.Misc.NormalVillage"), 0, 0, IconId.eIcnTH);
        host.AddTab(Tr("MISC_TAB_ITEM2", "Builder Base"), () => Screen("Village.Misc.BuilderBase"), 0, 0, IconId.eIcnBuilderHall);
        host.AddTab(Tr("MISC_TAB_ITEM3", "Clan Games"), () => Screen("Village.Misc.ClanGames"), 0, 0, IconId.eIcnStrongMan);
        host.AddTab(Tr("MISC_TAB_ITEM4", "Clan Capital"), () => Screen("Village.Misc.ClanCapital"), 0, 0, IconId.eIcnClanCapital);

        host.SelectFirst();
        return host;
    }

    private FrameworkElement BuildClanCastleTab()
    {
        // parity: Bind_ImageList @ COCBot/MBR GUI Control.au3:2080
        // [$eIcnCCRequest, $eIcnCCDonate, $eIcnHourGlass].
        var host = new GuiTabHost(SizeTab2Width, SizeTab2Height, _icons);

        host.AddTab(Tr("Tab_02_STab_02_STab_01", "Request Troops"), () => Screen("Village.ClanCastle.Request"), 0, 0, IconId.eIcnCCRequest);
        host.AddTab(Tr("Tab_02_STab_02_STab_02", "Donate Troops"), () => Screen("Village.ClanCastle.Donate"), 0, 0, IconId.eIcnCCDonate);
        host.AddTab(Tr("Tab_02_STab_02_STab_03", "Schedule Donations"), () => Screen("Village.ClanCastle.Schedule"), 0, 0, IconId.eIcnHourGlass);

        host.SelectFirst();
        return host;
    }

    private FrameworkElement BuildUpgradeTab()
    {
        // parity: Bind_ImageList @ COCBot/MBR GUI Control.au3:2084
        // [$eIcnBuilderHut, $eIcnBarrack, $eIcnHeroes, $eIcnLaboratory, $eIcnWall].
        var host = new GuiTabHost(SizeTab2Width, SizeTab2Height, _icons);

        // Thứ tự tab trong nguồn: Auto Upgrade, Buildings, Heroes, Lab, Walls.
        host.AddTab(Tr("Tab_02_STab_03_STab_05", "Auto Upgrade"), () => Screen("Village.Upgrade.AutoUpgrade"), 0, 0, IconId.eIcnBuilderHut);
        host.AddTab(Tr("Tab_02_STab_03_STab_03", "Buildings"), () => Screen("Village.Upgrade.Buildings"), 0, 0, IconId.eIcnBarrack);
        host.AddTab(Tr("Tab_02_STab_03_STab_02", "Heroes"), () => Screen("Village.Upgrade.Heroes"), 0, 0, IconId.eIcnHeroes);
        host.AddTab(Tr("Tab_02_STab_03_STab_01", "Lab"), () => Screen("Village.Upgrade.Lab"), 0, 0, IconId.eIcnLaboratory);
        host.AddTab(Tr("Tab_02_STab_03_STab_04", "Walls"), () => Screen("Village.Upgrade.Walls"), 0, 0, IconId.eIcnWall);

        host.SelectFirst();
        return host;
    }

    private FrameworkElement BuildTrainArmyTab()
    {
        // parity: Bind_ImageList @ COCBot/MBR GUI Control.au3:2072
        // [$eIcnCamp, $eIcnSettings].
        var host = new GuiTabHost(SizeTab2Width, SizeTab2Height, _icons);

        host.AddTab(Tr("Tab_03_STab_01_STab_01", "Army"), () => Screen("Attack.TrainArmy.Army"), ChildLeft, ChildTop, IconId.eIcnCamp);
        host.AddTab(Tr("Tab_03_STab_01_STab_02", "Settings"), () => Screen("Attack.TrainArmy.Settings"), ChildLeft, ChildTop, IconId.eIcnSettings);

        host.SelectFirst();
        return host;
    }

    private FrameworkElement BuildSearchTab()
    {
        // parity: Bind_ImageList @ COCBot/MBR GUI Control.au3:2096
        // [$eIcnDeadBase, $eIcnActiveBase, $eIcnSettings].
        var host = new GuiTabHost(SizeTab2Width, SizeTab2Height, _icons);

        // AutoIt thêm 4 dấu cách vào tiêu đề để chừa chỗ cho ô tick.
        host.AddTab(Tr("Tab_03_STab_02_STab_01", "DeadBase") + "    ", () => BuildBaseTab("DeadBase"), ChildLeft, ChildTop, IconId.eIcnDeadBase);
        host.AddTab(Tr("Tab_03_STab_02_STab_02", "ActiveBase") + "    ", () => BuildBaseTab("ActiveBase"), ChildLeft, ChildTop, IconId.eIcnActiveBase);
        host.AddTab(Tr("Tab_03_STab_02_STab_05", "Settings"), BuildAttackOptionsTab, ChildLeft, ChildTop, IconId.eIcnSettings);

        host.SelectFirst();
        return host;
    }

    private GuiTabHost BuildBaseTab(string name)
    {
        // parity: Bind_ImageList @ COCBot/MBR GUI Control.au3:2100 (DeadBase)
        // và :2104 (ActiveBase) — [$eIcnMagnifier, $eIcnMainAttack(, $eIcnCollector)].
        var host = new GuiTabHost(SizeTab3Width, SizeTab3Height, _icons);

        host.AddTab(Tr("Tab_03_STab_02_STab_0X_STab_01", "-1"), () => Screen($"Attack.Search.{name}.Search"), 0, 0, IconId.eIcnMagnifier);
        host.AddTab(Tr("Tab_03_STab_02_STab_0X_STab_02", "-1"), () => Screen($"Attack.Search.{name}.Attack"), 0, 0, IconId.eIcnMainAttack);

        if (string.Equals(name, "DeadBase", StringComparison.Ordinal))
        {
            host.AddTab(Tr("Tab_03_STab_02_STab_01_STab_01", "Collectors"), () => Screen("Attack.Search.DeadBase.Collectors"), 0, 0, IconId.eIcnCollector);
        }

        host.SelectFirst();
        return host;
    }

    private FrameworkElement BuildAttackOptionsTab()
    {
        // parity: Bind_ImageList @ COCBot/MBR GUI Control.au3:2108
        // [$eIcnMagnifier, $eIcnHeroes, $eIcnSilverStar].
        var host = new GuiTabHost(SizeTab3Width, SizeTab3Height, _icons);

        host.AddTab(Tr("Tab_03_STab_02_STab_0X_STab_01", "-1"), () => Screen("Attack.Search.Settings.Search"), 0, 0, IconId.eIcnMagnifier);
        host.AddTab(Tr("Tab_03_STab_02_STab_05_STab_02", "Hero Abilities"), () => Screen("Attack.Search.Settings.HeroAbilities"), 0, 0, IconId.eIcnHeroes);
        host.AddTab(Tr("Tab_03_STab_02_STab_0X_STab_03", "-1"), () => Screen("Attack.Search.Settings.EndBattle"), 0, 0, IconId.eIcnSilverStar);

        host.SelectFirst();
        return host;
    }

    // ── Tab Log (bố cục động, không nằm trong gui-spec.json) ─────────────────────

    private const double LogWidth = 452;
    private const double LogHeight = 437;
    private const double LogDividerHeight = 4;
    private const double LogToolbarTop = 410;

    // Chiều cao mặc định: bot log cao gấp rưỡi attack log (kiểu "Large Bot Log").
    private const double BotLogHeight = 303;

    /// <summary>
    /// Tab Log: hai RichEdit (bot log / attack log) ngăn bởi thanh chia, cùng hàng
    /// công cụ ở đáy — toàn bộ được đặt vị trí lúc chạy bằng <c>LayoutMiniLogPanel</c>
    /// và <c>cmbLog</c>, nên phải dựng tay thay vì đọc từ gui-spec.json.
    /// </summary>
    private FrameworkElement BuildLogTab()
    {
        var canvas = new Canvas
        {
            Width = LogWidth,
            Height = LogHeight,
            Background = GuiPalette.TabBodyBackground,
            ClipToBounds = true,
        };

        const double atkLogTop = BotLogHeight + LogDividerHeight;
        double atkLogHeight = LogToolbarTop - atkLogTop;

        ListBox botLog = CreateLogBox(BotLogHeight);
        botLog.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(MainViewModel.LogMessages)));

        TextBox atkLog = CreateAttackLog(atkLogHeight);

        var divider = new Border
        {
            Width = LogWidth,
            Height = LogDividerHeight,
            Background = GuiPalette.Text,
        };

        Place(canvas, botLog, 0, 0);
        Place(canvas, divider, 0, BotLogHeight);
        Place(canvas, atkLog, 0, atkLogTop);

        var styleLabel = new TextBlock
        {
            Text = _translator.T("MBR GUI Design Log", "LblLog_Style", "Log Style") + ":",
            Foreground = GuiPalette.Text,
            FontSize = GuiControlFactory.DefaultFontPoints * 96.0 / 72.0,
        };
        Place(canvas, styleLabel, 0, LogToolbarTop + 5);

        Place(canvas, CreateLogStyleCombo(), 50, LogToolbarTop);

        Image clearIcon = CreateIconImage(IconId.eIcnReload, 22);
        clearIcon.ToolTip = _translator.T(
            "MBR GUI Design Log", "BtnAtkLogClear_Info_01", "Use this to clear the Attack Log.");
        clearIcon.MouseLeftButtonDown += (_, e) =>
        {
            atkLog.Clear();
            e.Handled = true;
        };
        Place(canvas, clearIcon, LogWidth - 84, LogToolbarTop + 1);

        Image copyIcon = CreateIconImage(IconId.eIcnCopy, 22);
        copyIcon.ToolTip = _translator.T(
            "MBR GUI Design Log", "BtnAtkLogCopyClipboard_Info_01",
            "Use this to copy the Attack Log to the Clipboard (CTRL+C)");
        copyIcon.MouseLeftButtonDown += (_, e) =>
        {
            if (atkLog.Text.Length > 0)
            {
                Clipboard.SetText(atkLog.Text);
            }

            e.Handled = true;
        };
        Place(canvas, copyIcon, LogWidth - 55, LogToolbarTop + 1);

        Image openIcon = CreateIconImage(IconId.eIcnEdit, 22);
        openIcon.ToolTip = _translator.T(
            "MBR GUI Design Log", "BtnOpenProfileFolder_Info_01", "Use this to open the Profile folder.");
        openIcon.MouseLeftButtonDown += (_, e) =>
        {
            OpenProfileFolder();
            e.Handled = true;
        };
        Place(canvas, openIcon, LogWidth - 26, LogToolbarTop + 1);

        return canvas;
    }

    private static ListBox CreateLogBox(double height)
    {
        var box = new ListBox
        {
            Width = LogWidth,
            Height = height,
            Background = GuiPalette.FieldBackground,
            BorderBrush = GuiPalette.FieldBorder,
            BorderThickness = new Thickness(1),
            Foreground = GuiPalette.Text,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 9 * 96.0 / 72.0,
            Padding = new Thickness(1),
        };

        ScrollViewer.SetHorizontalScrollBarVisibility(box, ScrollBarVisibility.Auto);
        ScrollViewer.SetVerticalScrollBarVisibility(box, ScrollBarVisibility.Auto);
        return box;
    }

    private static TextBox CreateAttackLog(double height)
    {
        var box = new TextBox
        {
            Width = LogWidth,
            Height = height,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            Background = GuiPalette.FieldBackground,
            BorderBrush = GuiPalette.FieldBorder,
            BorderThickness = new Thickness(1),
            Foreground = GuiPalette.Text,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 9 * 96.0 / 72.0,
            VerticalContentAlignment = VerticalAlignment.Top,
        };

        ScrollViewer.SetHorizontalScrollBarVisibility(box, ScrollBarVisibility.Auto);
        ScrollViewer.SetVerticalScrollBarVisibility(box, ScrollBarVisibility.Auto);
        return box;
    }

    private ComboBox CreateLogStyleCombo()
    {
        var combo = new ComboBox
        {
            Width = 180,
            Height = 22,
            IsEditable = false,
            Background = GuiPalette.FieldBackground,
            BorderBrush = GuiPalette.FieldBorder,
            Foreground = GuiPalette.Text,
            FontSize = GuiControlFactory.DefaultFontPoints * 96.0 / 72.0,
        };

        combo.ToolTip = _translator.T(
            "MBR GUI Design Log", "LblLog_Style_Info_01", "Use these options to set the Log type.");

        string[] styles =
        {
            _translator.T("MBR GUI Design Log", "CmbLogDividerOption_Item_01", "-1"),
            _translator.T("MBR GUI Design Log", "CmbLogDividerOption_Item_02", "-1"),
            _translator.T("MBR GUI Design Log", "CmbLogDividerOption_Item_03", "-1"),
            _translator.T("MBR GUI Design Log", "CmbLogDividerOption_Item_04", "-1"),
            _translator.T("MBR GUI Design Log", "CmbLogDividerOption_Item_05", "-1"),
            _translator.T("MBR GUI Design Log", "CmbLogDividerOption_Item_06", "-1"),
        };

        foreach (string style in styles)
        {
            combo.Items.Add(style);
        }

        combo.SelectedIndex = 0;
        return combo;
    }

    private static void OpenProfileFolder()
    {
        try
        {
            string folder = System.IO.Path.Combine(AppContext.BaseDirectory, "Profiles");
            if (System.IO.Directory.Exists(folder))
            {
                System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(folder) { UseShellExecute = true });
            }
        }
        catch (Exception)
        {
            // Mở thư mục chỉ là tiện ích — lỗi quyền truy cập không được làm sập GUI.
        }
    }

    // ── Hạ tầng dùng chung ───────────────────────────────────────────────────────

    private GuiScreenView Screen(string id)
    {
        GuiScreenSpec spec = _catalog.Get(id);
        var factory = new GuiControlFactory(_translator, _icons, "rad_" + id);
        return new GuiScreenView(spec, factory);
    }

    private string Tr(string key, string? fallback) => _translator.T(MainSection, key, fallback);

    private Image CreateIconImage(IconId id, double size)
    {
        var image = new Image
        {
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            Cursor = Cursors.Hand,
        };

        ImageSource? source = _icons.GetIcon(id, (int)Math.Round(size));
        if (source is not null)
        {
            image.Source = source;
        }

        return image;
    }

    private static void Place(Canvas canvas, UIElement element, double left, double top)
    {
        Canvas.SetLeft(element, left);
        Canvas.SetTop(element, top);
        canvas.Children.Add(element);
    }
}
