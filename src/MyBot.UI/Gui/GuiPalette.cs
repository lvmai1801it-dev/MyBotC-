using System.Windows.Media;

namespace MyBot.UI.Gui;

/// <summary>
/// Bảng màu dùng cho phần GUI dựng lại từ AutoIt.
///
/// Bản AutoIt không đặt màu nền cho cửa sổ chính nên toàn bộ GUI dùng màu mặt
/// hộp thoại mặc định của Windows (<c>$GUI_BKCOLOR</c> = <c>0xF0F0F0</c>),
/// control nhập liệu nền trắng và viền xám — khác với dark theme của các view
/// WPF viết tay (xem <c>Themes/DarkTheme.xaml</c>).
/// </summary>
public static class GuiPalette
{
    /// <summary>Nền cửa sổ và thân tab (màu mặt hộp thoại mặc định).</summary>
    public static readonly Brush WindowBackground = Frozen(0xF0, 0xF0, 0xF0);

    /// <summary>Nền vùng không có control (dùng cho thân tab đang chọn).</summary>
    public static readonly Brush TabBodyBackground = Frozen(0xF0, 0xF0, 0xF0);

    /// <summary>Nền của tab không được chọn.</summary>
    public static readonly Brush TabInactiveBackground = Frozen(0xD8, 0xD8, 0xD8);

    /// <summary>Viền tab và group box.</summary>
    public static readonly Brush Border = Frozen(0x9A, 0x9A, 0x9A);

    /// <summary>Viền nhấn của tab đang chọn.</summary>
    public static readonly Brush BorderActive = Frozen(0x6E, 0x6E, 0x6E);

    /// <summary>Màu chữ mặc định.</summary>
    public static readonly Brush Text = Frozen(0x00, 0x00, 0x00);

    /// <summary>Màu chữ của tab không được chọn.</summary>
    public static readonly Brush TextMuted = Frozen(0x50, 0x50, 0x50);

    /// <summary>Nền control có thể chỉnh sửa (input, combo, danh sách).</summary>
    public static readonly Brush FieldBackground = Frozen(0xFF, 0xFF, 0xFF);

    /// <summary>Nền nút nhấn.</summary>
    public static readonly Brush ButtonBackground = Frozen(0xE1, 0xE1, 0xE1);

    /// <summary>Nền nút khi rê chuột.</summary>
    public static readonly Brush ButtonHoverBackground = Frozen(0xE9, 0xF5, 0xFD);

    /// <summary>Viền control nhập liệu.</summary>
    public static readonly Brush FieldBorder = Frozen(0x7A, 0x7A, 0x7A);

    /// <summary>Màu cho thanh tiến trình (progress bar).</summary>
    public static readonly Brush ProgressFill = Frozen(0x06, 0xB0, 0x25);

    /// <summary>Nền của ảnh chưa nạp được (chỗ đặt <c>GUICtrlCreatePic</c>).</summary>
    public static readonly Brush PlaceholderBackground = Frozen(0xE4, 0xE4, 0xE4);

    /// <summary>Nền thanh tiêu đề cửa sổ (màu trắng như bản AutoIt).</summary>
    public static readonly Brush TitleBarBackground = Frozen(0xFF, 0xFF, 0xFF);

    /// <summary>Màu chữ tiêu đề (<c>0x171717</c>).</summary>
    public static readonly Brush TitleText = Frozen(0x17, 0x17, 0x17);

    /// <summary>Nền/chard của các nút thu nhỏ, phóng to, mini, đóng.</summary>
    public static readonly Brush CaptionButtonBackground = Frozen(0xF0, 0xF0, 0xF0);

    /// <summary>Màu glyph của nút caption.</summary>
    public static readonly Brush CaptionGlyph = Frozen(0xB8, 0xB8, 0xB8);

    /// <summary>Nền nút đóng cửa sổ.</summary>
    public static readonly Brush CloseButtonBackground = Frozen(0xFF, 0x40, 0x40);

    /// <summary>Màu glyph của nút đóng cửa sổ.</summary>
    public static readonly Brush CloseButtonGlyph = Frozen(0xF8, 0xF8, 0xF8);

    /// <summary>Chuyển "RRGGBB" (định dạng trong gui-spec.json) sang brush.</summary>
    public static Brush FromHex(string rgb)
    {
        if (string.IsNullOrEmpty(rgb) || rgb.Length != 6)
        {
            return Text;
        }

        return Frozen(
            System.Convert.ToByte(rgb[..2], 16),
            System.Convert.ToByte(rgb.Substring(2, 2), 16),
            System.Convert.ToByte(rgb.Substring(4, 2), 16));
    }

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
