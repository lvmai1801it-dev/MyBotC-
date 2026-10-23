namespace MyBot.UI.Gui;

/// <summary>
/// Biến thể thanh tiêu đề theo <c>general.botDesignFlags</c> (bit 0).
///
/// parity: CreateMainGUI @ COCBot/MBR GUI Design Mini.au3:126 —
/// bit 0 = 1: thanh tiêu đề tự vẽ, <c>$_GUI_MAIN_TOP = 23</c>;
/// bit 0 = 0: cửa sổ dùng <c>$WS_CAPTION</c> của Windows, <c>$_GUI_MAIN_TOP = 5</c>.
/// Cửa sổ cao <c>$_GUI_MAIN_HEIGHT</c> (692) + <c>$_GUI_MAIN_TOP</c>, nên hai biến
/// thể chênh nhau đúng 18 px.
/// </summary>
public readonly record struct TitleBarVariant(bool CustomTitleBar, double MainTop)
{
    /// <summary>Bit 0 của <c>botDesignFlags</c>: 1 = dùng thanh tiêu đề tự vẽ.</summary>
    public const int CustomTitleBarMask = 0x1;

    /// <summary>Giá trị mặc định cho profile mới (thanh tiêu đề tự vẽ + tự thu nhỏ khi dock).</summary>
    public const int DefaultDesignFlags = 3;

    /// <summary>Chiều cao phần nội dung cửa sổ (<c>$_GUI_MAIN_HEIGHT</c>).</summary>
    public const double MainHeight = 692;

    /// <summary><c>$_GUI_MAIN_TOP</c> khi bật thanh tiêu đề tự vẽ.</summary>
    public const double CustomMainTop = 23;

    /// <summary><c>$_GUI_MAIN_TOP</c> khi dùng thanh tiêu đề mặc định của Windows.</summary>
    public const double NativeMainTop = 5;

    /// <summary>Đọc biến thể từ giá trị <c>general.botDesignFlags</c> trong config.</summary>
    public static TitleBarVariant FromDesignFlags(int botDesignFlags)
    {
        bool custom = (botDesignFlags & CustomTitleBarMask) != 0;
        return new TitleBarVariant(custom, custom ? CustomMainTop : NativeMainTop);
    }

    /// <summary>Độ dịch dọc của nội dung so với biến thể thanh tiêu đề tự vẽ: 0 hoặc −18 px.</summary>
    public double ContentOffset => MainTop - CustomMainTop;

    /// <summary>Chiều cao client của cửa sổ (<c>$_GUI_MAIN_HEIGHT + $_GUI_MAIN_TOP</c>).</summary>
    public double ClientHeight => MainHeight + MainTop;
}
