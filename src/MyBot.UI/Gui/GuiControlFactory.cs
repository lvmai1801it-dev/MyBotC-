using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using MyBot.Localization;
using MyBot.UI.Icons;

namespace MyBot.UI.Gui;

/// <summary>
/// Dựng phần tử WPF tương ứng với từng control AutoIt trong <c>gui-spec.json</c>.
///
/// Mỗi hàm dựng ở đây tương ứng một hàm <c>GUICtrlCreate*</c> của AutoIt, giữ nguyên
/// toạ độ tuyệt đối, cỡ chữ theo point và trạng thái ban đầu (ẩn / vô hiệu hoá / tick).
/// </summary>
public sealed class GuiControlFactory
{
    /// <summary>Cỡ chữ mặc định của GUI AutoIt (<c>GUISetFont</c> mặc định 8.5pt).</summary>
    public const double DefaultFontPoints = 8.5;

    private readonly IMyBotTranslator _translator;
    private readonly MbrBotIconProvider _icons;
    private readonly string _radioGroup;

    /// <summary>
    /// Khởi tạo factory cho một màn hình.
    /// </summary>
    /// <param name="translator">Bộ dịch i18n dùng cho nhãn và tooltip.</param>
    /// <param name="icons">Nguồn icon trong <c>lib/MBRBOT.dll</c>.</param>
    /// <param name="radioGroup">
    /// Tên nhóm dùng chung cho mọi radio trong cùng màn hình — AutoIt gom nhóm radio
    /// theo thứ tự tạo trong cùng cửa sổ, WPF cần tên nhóm tường minh.
    /// </param>
    public GuiControlFactory(IMyBotTranslator translator, MbrBotIconProvider icons, string radioGroup)
    {
        _translator = translator ?? throw new ArgumentNullException(nameof(translator));
        _icons = icons ?? throw new ArgumentNullException(nameof(icons));
        _radioGroup = radioGroup;
    }

    /// <summary>Dựng phần tử WPF cho một control; luôn trả về phần tử không null.</summary>
    public FrameworkElement Create(GuiControlSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        FrameworkElement element = spec.Type switch
        {
            "label" => CreateLabel(spec),
            "icon" => CreateIcon(spec),
            "group" => CreateGroup(spec),
            "check" => CreateToggle(spec, isRadio: false),
            "radio" => CreateToggle(spec, isRadio: true),
            "input" => CreateTextBox(spec),
            "edit" => CreateTextBox(spec),
            "richedit" => CreateRichText(spec),
            "button" => CreateButton(spec),
            "combo" => CreateCombo(spec),
            "progress" => CreateProgress(spec),
            "slider" => CreateSlider(spec),
            "graphic" => CreateGraphic(spec),
            "pic" => CreatePicture(spec),
            "tree" => CreateTree(spec),
            _ => CreateLabel(spec),
        };

        ApplyState(element, spec);
        return element;
    }

    /// <summary>Chuỗi hiển thị của control: ưu tiên bản dịch, sau đó tới văn bản gốc.</summary>
    public string ResolveText(GuiControlSpec spec)
    {
        if (spec.Section is not null && spec.Key is not null)
        {
            return _translator.T(spec.Section, spec.Key, spec.Text);
        }

        return spec.Text ?? string.Empty;
    }

    /// <summary>Nội dung tooltip: bản dịch nếu có section/khoá, ngược lại chuỗi tĩnh.</summary>
    public string? ResolveTip(GuiControlSpec spec)
    {
        if (spec.TipSection is not null && spec.TipKey is not null)
        {
            return _translator.T(spec.TipSection, spec.TipKey, null);
        }

        return spec.Tip;
    }

    /// <summary>
    /// Danh sách mục của combo. Dịch từng mảnh rồi mới tách theo <c>|</c> — đúng
    /// như <c>GUICtrlSetData</c> của AutoIt, nơi một khoá i18n có thể chứa sẵn cả
    /// danh sách (ví dụ <c>CmbScriptDropline=…|…|…</c> trong file ngôn ngữ).
    /// </summary>
    private List<string> ResolveComboItems(GuiControlSpec spec)
    {
        var items = new List<string>();

        foreach (GuiComboItem fragment in spec.Items)
        {
            foreach (string piece in ResolveFragment(fragment).Split('|'))
            {
                string trimmed = piece.Trim();
                if (trimmed.Length > 0)
                {
                    items.Add(trimmed);
                }
            }
        }

        // Combo không khai báo danh sách: giữ nhãn đơn như trước.
        if (items.Count == 0)
        {
            string text = ResolveText(spec);
            if (!string.IsNullOrEmpty(text))
            {
                items.Add(text);
            }
        }

        return items;
    }

    /// <summary>
    /// Chỉ số mục được chọn sẵn. Với combo, tham số thứ ba của <c>GUICtrlSetData</c>
    /// là giá trị cần chọn chứ không phải chỉ số, nên phải dò theo văn bản đã dịch.
    /// </summary>
    private int ResolveComboSelection(GuiControlSpec spec, List<string> items)
    {
        if (spec.Selected is not null)
        {
            string wanted = ResolveFragment(spec.Selected);
            int index = wanted.Length > 0 ? items.IndexOf(wanted) : -1;
            if (index >= 0)
            {
                return index;
            }
        }

        return 0;
    }

    private string ResolveFragment(GuiComboItem fragment) =>
        fragment.Section is not null && fragment.Key is not null
            ? _translator.T(fragment.Section, fragment.Key, fragment.Text)
            : fragment.Text ?? string.Empty;

    private static double ToDip(double? points) => (points ?? DefaultFontPoints) * 96.0 / 72.0;

    private FrameworkElement CreateLabel(GuiControlSpec spec)
    {
        var text = new TextBlock
        {
            Text = ResolveText(spec),
            TextWrapping = TextWrapping.NoWrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = spec.Align switch
            {
                "r" => TextAlignment.Right,
                "c" => TextAlignment.Center,
                _ => TextAlignment.Left,
            },
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = spec.Foreground is not null ? GuiPalette.FromHex(spec.Foreground) : GuiPalette.Text,
        };

        ApplyTextStyle(text, spec);

        if (spec.Height is > 0)
        {
            // Nhãn AutoIt có chiều cao cố định và chữ canh giữa theo chiều dọc.
            var host = new Grid { Height = spec.Height.Value };
            if (spec.Width is > 0)
            {
                host.Width = spec.Width.Value;
            }

            host.Children.Add(text);
            return host;
        }

        if (spec.Width is > 0)
        {
            text.Width = spec.Width.Value;
        }

        return text;
    }

    private FrameworkElement CreateIcon(GuiControlSpec spec)
    {
        double width = spec.Width ?? 16;
        double height = spec.Height ?? width;

        var image = new Image
        {
            Width = width,
            Height = height,
            Stretch = Stretch.Uniform,
            SnapsToDevicePixels = true,
        };

        if (spec.Icon is not null && Enum.TryParse(spec.Icon, ignoreCase: false, out IconId id))
        {
            int pixel = (int)Math.Round(Math.Max(width, height));
            image.Source = _icons.GetIcon(id, pixel);
        }

        return image;
    }

    private FrameworkElement CreateGroup(GuiControlSpec spec)
    {
        double width = spec.Width ?? 200;
        double height = spec.Height ?? 100;

        var host = new Grid { Width = width, Height = height };

        // Khung group box: viền bo góc bắt đầu thấp hơn đỉnh một chút để chừa chỗ
        // cho dòng tiêu đề, đúng kiểu BS_GROUPBOX của Win32.
        host.Children.Add(new Border
        {
            BorderBrush = GuiPalette.Border,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(3),
            Margin = new Thickness(0, 7, 0, 0),
        });

        var caption = new TextBlock
        {
            Text = ResolveText(spec),
            Foreground = GuiPalette.Text,
            VerticalAlignment = VerticalAlignment.Center,
        };
        ApplyTextStyle(caption, spec);

        host.Children.Add(new Border
        {
            Background = GuiPalette.WindowBackground,
            Padding = new Thickness(4, 0, 4, 0),
            Margin = new Thickness(8, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Child = caption,
        });

        return host;
    }

    private FrameworkElement CreateToggle(GuiControlSpec spec, bool isRadio)
    {
        var content = new TextBlock
        {
            Text = ResolveText(spec),
            Foreground = spec.Foreground is not null ? GuiPalette.FromHex(spec.Foreground) : GuiPalette.Text,
            TextWrapping = TextWrapping.Wrap,
        };
        ApplyTextStyle(content, spec);

        ToggleButton toggle = isRadio
            ? new RadioButton { GroupName = _radioGroup }
            : new CheckBox();
        toggle.Content = content;
        toggle.IsChecked = spec.Checked == 1;
        toggle.Foreground = content.Foreground;

        Size(toggle, spec, autoWidth: true, autoHeight: true);
        return toggle;
    }

    private FrameworkElement CreateTextBox(GuiControlSpec spec)
    {
        bool multiline = spec.Multiline == 1;

        var box = new TextBox
        {
            Text = ResolveText(spec),
            IsReadOnly = spec.ReadOnly == 1,
            TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap,
            AcceptsReturn = multiline,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = GuiPalette.FieldBackground,
            BorderBrush = GuiPalette.FieldBorder,
            Foreground = GuiPalette.Text,
            Padding = new Thickness(2, 1, 2, 1),
            TextAlignment = spec.Align switch
            {
                "r" => TextAlignment.Right,
                "c" => TextAlignment.Center,
                _ => TextAlignment.Left,
            },
            VerticalScrollBarVisibility = multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
        };

        ApplyControlStyle(box, spec);

        if (multiline)
        {
            box.VerticalContentAlignment = VerticalAlignment.Top;
            Size(box, spec, autoWidth: false, autoHeight: false, defaultWidth: 200, defaultHeight: 70);
        }
        else
        {
            Size(box, spec, autoWidth: false, autoHeight: false, defaultWidth: 100, defaultHeight: 18);
        }

        return box;
    }

    private FrameworkElement CreateRichText(GuiControlSpec spec)
    {
        var box = new RichTextBox
        {
            IsReadOnly = spec.ReadOnly != 0,
            Background = GuiPalette.FieldBackground,
            BorderBrush = GuiPalette.FieldBorder,
            BorderThickness = new Thickness(1),
            Foreground = GuiPalette.Text,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        ApplyControlStyle(box, spec);
        Size(box, spec, autoWidth: false, autoHeight: false, defaultWidth: 200, defaultHeight: 60);
        return box;
    }

    private FrameworkElement CreateButton(GuiControlSpec spec)
    {
        var button = new Button
        {
            Content = ResolveText(spec),
            Background = GuiPalette.ButtonBackground,
            BorderBrush = GuiPalette.FieldBorder,
            Foreground = GuiPalette.Text,
            Padding = new Thickness(4, 1, 4, 1),
        };

        ApplyControlStyle(button, spec);
        Size(button, spec, autoWidth: false, autoHeight: false, defaultWidth: 75, defaultHeight: 23);
        return button;
    }

    private FrameworkElement CreateCombo(GuiControlSpec spec)
    {
        var combo = new ComboBox
        {
            IsEditable = false,
            Background = GuiPalette.FieldBackground,
            BorderBrush = GuiPalette.FieldBorder,
            Foreground = GuiPalette.Text,
        };

        ApplyControlStyle(combo, spec);

        List<string> items = ResolveComboItems(spec);
        foreach (string item in items)
        {
            combo.Items.Add(item);
        }

        if (items.Count > 0)
        {
            combo.SelectedIndex = ResolveComboSelection(spec, items);
        }

        // Tham số chiều cao của GUICtrlCreateCombo là chiều cao *khi mở danh sách*,
        // còn hộp đóng chỉ cao theo font — nên kẹp lại cho đúng hình dạng.
        Size(combo, spec, autoWidth: false, autoHeight: false, defaultWidth: 100, defaultHeight: 21);
        combo.Height = Math.Clamp(combo.Height, 16, 28);
        return combo;
    }

    private static FrameworkElement CreateProgress(GuiControlSpec spec)
    {
        var bar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Height = spec.Height ?? 10,
            BorderBrush = GuiPalette.FieldBorder,
            Foreground = GuiPalette.ProgressFill,
            Background = GuiPalette.PlaceholderBackground,
        };

        if (spec.Width is > 0)
        {
            bar.Width = spec.Width.Value;
        }

        return bar;
    }

    private static FrameworkElement CreateSlider(GuiControlSpec spec)
    {
        var slider = new Slider
        {
            Orientation = Orientation.Horizontal,
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Height = spec.Height ?? 20,
            IsSnapToTickEnabled = false,
        };

        if (spec.Width is > 0)
        {
            slider.Width = spec.Width.Value;
        }

        return slider;
    }

    private static FrameworkElement CreateGraphic(GuiControlSpec spec) =>
        new Rectangle
        {
            Fill = Brushes.Gray,
            Width = spec.Width ?? 1,
            Height = spec.Height ?? 1,
        };

    /// <summary>
    /// Ảnh tĩnh (<c>GUICtrlCreatePic</c>). Bản AutoIt trỏ tới một DLL/biến thể icon
    /// không có trong bộ tài nguyên của bản port, nên chỗ này vẽ hộp giữ chỗ.
    /// </summary>
    private static FrameworkElement CreatePicture(GuiControlSpec spec) =>
        new Border
        {
            Width = spec.Width ?? 64,
            Height = spec.Height ?? 64,
            Background = GuiPalette.PlaceholderBackground,
            BorderBrush = GuiPalette.Border,
            BorderThickness = new Thickness(1),
        };

    private static FrameworkElement CreateTree(GuiControlSpec spec)
    {
        var tree = new TreeView
        {
            Background = GuiPalette.FieldBackground,
            BorderBrush = GuiPalette.FieldBorder,
            BorderThickness = new Thickness(1),
        };

        Size(tree, spec, autoWidth: false, autoHeight: false, defaultWidth: 200, defaultHeight: 120);
        return tree;
    }

    private static void ApplyTextStyle(TextBlock text, GuiControlSpec spec)
    {
        text.FontSize = ToDip(spec.FontSize);
        if (spec.FontBold == 1)
        {
            text.FontWeight = FontWeights.Bold;
        }

        if (spec.FontFamily is not null)
        {
            text.FontFamily = new FontFamily(spec.FontFamily);
        }
    }

    private static void ApplyControlStyle(Control control, GuiControlSpec spec)
    {
        control.FontSize = ToDip(spec.FontSize);
        if (spec.FontBold == 1)
        {
            control.FontWeight = FontWeights.Bold;
        }

        if (spec.FontFamily is not null)
        {
            control.FontFamily = new FontFamily(spec.FontFamily);
        }
    }

    private static void Size(
        FrameworkElement element,
        GuiControlSpec spec,
        bool autoWidth,
        bool autoHeight,
        double defaultWidth = 0,
        double defaultHeight = 0)
    {
        if (spec.Width is > 0)
        {
            element.Width = spec.Width.Value;
        }
        else if (!autoWidth && defaultWidth > 0)
        {
            element.Width = defaultWidth;
        }

        if (spec.Height is > 0)
        {
            element.Height = spec.Height.Value;
        }
        else if (!autoHeight && defaultHeight > 0)
        {
            element.Height = defaultHeight;
        }
    }

    private static void ApplyState(FrameworkElement element, GuiControlSpec spec)
    {
        if (spec.Hidden == 1)
        {
            element.Visibility = Visibility.Collapsed;
        }

        if (spec.Disabled == 1)
        {
            element.IsEnabled = false;
        }

        if (spec.Background is not null && element is Control backgroundOwner)
        {
            backgroundOwner.Background = GuiPalette.FromHex(spec.Background);
        }

        if (spec.Foreground is not null)
        {
            Brush brush = GuiPalette.FromHex(spec.Foreground);
            switch (element)
            {
                case Control control:
                    control.Foreground = brush;
                    break;
                case TextBlock text:
                    text.Foreground = brush;
                    break;
                default:
                    break;
            }
        }
    }
}
