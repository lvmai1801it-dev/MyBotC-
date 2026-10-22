using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using MyBot.UI.ViewModels;

namespace MyBot.UI.Converters;

/// <summary>
/// Chuyển đổi trạng thái HeroState sang màu sắc hiển thị tương ứng.
/// </summary>
public class HeroStateToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush GrayBrush = new(Color.FromRgb(0x77, 0x77, 0x77));
    private static readonly SolidColorBrush GreenBrush = new(Color.FromRgb(0x5C, 0xAD, 0x85));
    private static readonly SolidColorBrush BlueBrush = new(Color.FromRgb(0x00, 0xD2, 0xFF));
    private static readonly SolidColorBrush RedBrush = new(Color.FromRgb(0xDB, 0x4D, 0x4D));

    static HeroStateToBrushConverter()
    {
        GrayBrush.Freeze();
        GreenBrush.Freeze();
        BlueBrush.Freeze();
        RedBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is HeroState state)
        {
            return state switch
            {
                HeroState.Green => GreenBrush,
                HeroState.Blue => BlueBrush,
                HeroState.Red => RedBrush,
                _ => GrayBrush
            };
        }
        return GrayBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
