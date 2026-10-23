using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MyBot.Localization;
using MyBot.UI.Icons;

namespace MyBot.UI.Gui;

/// <summary>
/// Tab control tự dựng, thay cho <see cref="TabControl"/> của WPF.
///
/// Bản AutoIt đặt control con trực tiếp lên cửa sổ chứa tab (toạ độ tuyệt đối tính
/// từ góc trên-trái của GUI), còn <see cref="TabControl"/> của WPF lại bọc nội dung
/// trong một presenter nằm dưới dải tiêu đề — lệch vài chục pixel so với bản gốc.
/// Vì vậy ở đây dải tab được vẽ đè lên một <see cref="Canvas"/> giữ nguyên toạ độ.
///
/// Các tab con (level 2, 3, 4) cũng là những <see cref="GuiTabHost"/> lồng nhau,
/// đặt tại đúng offset mà AutoIt dùng cho cửa sổ con tương ứng (thường là 5,25).
/// </summary>
public sealed class GuiTabHost : Grid
{
    /// <summary>Chiều cao dải tiêu đề tab (khớp tab control mặc định của Win32).</summary>
    public const double HeaderHeight = 22;

    /// <summary>
    /// Cạnh icon tab, tính bằng pixel.
    ///
    /// parity: Bind_ImageList @ COCBot/MBR GUI Control.au3:2053 —
    /// AutoIt tạo image list bằng <c>_GUIImageList_Create(16, 16, 5, 1)</c> nên mọi icon
    /// gắn vào tab đều bị ép về 16×16.
    /// </summary>
    public const int TabIconSize = 16;

    private readonly Canvas _content = new();
    private readonly StackPanel _headers = new() { Orientation = Orientation.Horizontal };
    private readonly List<TabEntry> _tabs = new();
    private readonly List<Border> _headerBorders = new();
    private readonly List<IconId?> _tabIcons = new();
    private readonly MbrBotIconProvider? _icons;

    private int _selectedIndex = -1;

    /// <summary>Khởi tạo tab control với kích thước cố định.</summary>
    /// <param name="width">Chiều rộng tab control.</param>
    /// <param name="height">Chiều cao tab control.</param>
    /// <param name="icons">
    /// Nguồn icon tab; <c>null</c> thì tab chỉ có chữ (dùng trong test và ở những chỗ
    /// không cần icon). Icon vẫn được nạp đúng 16×16 như image list của AutoIt.
    /// </param>
    public GuiTabHost(double width, double height, MbrBotIconProvider? icons = null)
    {
        _icons = icons;
        Width = width;
        Height = height;
        ClipToBounds = true;
        SnapsToDevicePixels = true;
        Background = GuiPalette.TabBodyBackground;

        _content.Width = width;
        _content.Height = height;
        Children.Add(_content);

        _headers.VerticalAlignment = VerticalAlignment.Top;
        _headers.HorizontalAlignment = HorizontalAlignment.Left;
        _headers.Margin = new Thickness(4, 1, 4, 0);
        Children.Add(_headers);
    }

    /// <summary>Chỉ số tab đang chọn, -1 khi chưa chọn tab nào.</summary>
    public int SelectedIndex => _selectedIndex;

    /// <summary>Số tab đã thêm.</summary>
    public int TabCount => _tabs.Count;

    /// <summary>
    /// Icon của từng tab theo đúng thứ tự thêm vào; <c>null</c> ở tab không gắn icon.
    /// Bản AutoIt gắn icon theo chỉ số tab trong image list (xem <c>Bind_ImageList</c>).
    /// </summary>
    public IReadOnlyList<IconId?> TabIcons => _tabIcons;

    /// <summary>Dải tiêu đề tab (mỗi con là một <see cref="Border"/> ứng với một tab).</summary>
    public IReadOnlyList<FrameworkElement> Headers => _headerBorders;

    /// <summary>
    /// Thêm một tab.
    /// </summary>
    /// <param name="header">Tiêu đề (đã dịch).</param>
    /// <param name="contentFactory">
    /// Hàm dựng nội dung; chỉ gọi một lần, ở lần đầu tab được chọn — giữ cho việc
    /// dựng ~2900 control trải đều thay vì dồn hết vào lúc khởi động.
    /// </param>
    /// <param name="contentLeft">Toạ độ trái của nội dung so với tab control.</param>
    /// <param name="contentTop">Toạ độ trên của nội dung so với tab control.</param>
    /// <param name="icon">Icon 16×16 của tab; <c>null</c> là tab chỉ có chữ.</param>
    public void AddTab(
        string header,
        Func<FrameworkElement> contentFactory,
        double contentLeft = 0,
        double contentTop = 0,
        IconId? icon = null)
    {
        ArgumentNullException.ThrowIfNull(contentFactory);

        int index = _tabs.Count;
        _tabs.Add(new TabEntry(header, contentFactory, contentLeft, contentTop));
        _tabIcons.Add(icon);

        Border border = CreateHeader(header, index, icon);
        _headerBorders.Add(border);
        _headers.Children.Add(border);
    }

    /// <summary>Chọn tab theo chỉ số; nội dung được dựng ở lần chọn đầu tiên.</summary>
    public void Select(int index)
    {
        if (index < 0 || index >= _tabs.Count || index == _selectedIndex)
        {
            return;
        }

        _selectedIndex = index;
        TabEntry entry = _tabs[index];

        FrameworkElement? cached = entry.Content;
        if (cached is null)
        {
            cached = entry.Factory();
            entry.Content = cached;
        }

        _content.Children.Clear();
        Canvas.SetLeft(cached, entry.Left);
        Canvas.SetTop(cached, entry.Top);
        _content.Children.Add(cached);

        UpdateHeaderVisuals();
    }

    /// <summary>Chọn tab đầu tiên nếu có.</summary>
    public void SelectFirst()
    {
        if (_tabs.Count > 0)
        {
            Select(0);
        }
    }

    private Border CreateHeader(string text, int index, IconId? icon)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = GuiControlFactory.DefaultFontPoints * 96.0 / 72.0,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        FrameworkElement content = label;

        if (icon is { } iconId)
        {
            // parity: Bind_ImageList @ COCBot/MBR GUI Control.au3:2053 — icon đứng
            // trước tiêu đề, luôn đúng 16×16 dù nguồn là bao nhiêu.
            var picture = new Image
            {
                Source = _icons?.GetIcon(iconId, TabIconSize),
                Width = TabIconSize,
                Height = TabIconSize,
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
            };
            row.Children.Add(picture);
            row.Children.Add(label);
            content = row;
        }

        var border = new Border
        {
            Padding = new Thickness(9, 3, 9, 3),
            Height = HeaderHeight,
            Background = GuiPalette.TabInactiveBackground,
            BorderBrush = GuiPalette.Border,
            BorderThickness = new Thickness(1, 1, 1, 0),
            Margin = new Thickness(0, 0, 1, 0),
            Cursor = Cursors.Hand,
            Child = content,
        };

        border.MouseLeftButtonDown += (_, e) =>
        {
            Select(index);
            e.Handled = true;
        };

        return border;
    }

    private void UpdateHeaderVisuals()
    {
        for (int i = 0; i < _headerBorders.Count; i++)
        {
            Border border = _headerBorders[i];
            bool isSelected = i == _selectedIndex;

            border.Background = isSelected ? GuiPalette.TabBodyBackground : GuiPalette.TabInactiveBackground;
            border.BorderBrush = isSelected ? GuiPalette.BorderActive : GuiPalette.Border;

            TextBlock? label = HeaderLabel(border);
            if (label is not null)
            {
                label.Foreground = isSelected ? GuiPalette.Text : GuiPalette.TextMuted;
                label.FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal;
            }
        }
    }

    private static TextBlock? HeaderLabel(Border border) => border.Child switch
    {
        TextBlock text => text,
        Panel panel => panel.Children.OfType<TextBlock>().FirstOrDefault(),
        _ => null,
    };

    private sealed class TabEntry
    {
        public TabEntry(string header, Func<FrameworkElement> factory, double left, double top)
        {
            Header = header;
            Factory = factory;
            Left = left;
            Top = top;
        }

        public string Header { get; }

        public Func<FrameworkElement> Factory { get; }

        public double Left { get; }

        public double Top { get; }

        public FrameworkElement? Content { get; set; }
    }
}
