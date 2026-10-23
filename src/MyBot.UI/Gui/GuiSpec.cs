using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyBot.UI.Gui;

/// <summary>
/// Một control GUI trong <c>gui-spec.json</c> — bản ghi bất biến của lời gọi
/// <c>GUICtrlCreate*</c> tương ứng trong AutoIt.
/// </summary>
public sealed class GuiControlSpec
{
    /// <summary>Loại WPF cần dựng: label, icon, group, check, radio, input, edit, combo, button, progress, slider, richedit, graphic, pic, tree.</summary>
    [JsonPropertyName("t")]
    public string Type { get; init; } = "label";

    /// <summary>Toạ độ trái, tính bằng pixel tại 96 DPI, so với gốc cửa sổ chứa tab.</summary>
    [JsonPropertyName("x")]
    public double X { get; init; }

    /// <summary>Toạ độ trên (xem <see cref="X"/>).</summary>
    [JsonPropertyName("y")]
    public double Y { get; init; }

    /// <summary>Chiều rộng; <c>null</c> nghĩa là AutoIt tự co giãn theo nội dung.</summary>
    [JsonPropertyName("w")]
    public double? Width { get; init; }

    /// <summary>Chiều cao; <c>null</c> nghĩa là AutoIt tự co giãn theo nội dung.</summary>
    [JsonPropertyName("h")]
    public double? Height { get; init; }

    /// <summary>Mục (section) trong file ngôn ngữ, khi nhãn là chuỗi dịch.</summary>
    [JsonPropertyName("sec")]
    public string? Section { get; init; }

    /// <summary>Khoá trong file ngôn ngữ.</summary>
    [JsonPropertyName("key")]
    public string? Key { get; init; }

    /// <summary>Văn bản gốc (tiếng Anh) hoặc giá trị khởi tạo của input.</summary>
    [JsonPropertyName("txt")]
    public string? Text { get; init; }

    /// <summary>Tên hằng <c>IconId</c> (đã bỏ dấu <c>$</c>), ví dụ <c>eIcnGold</c>.</summary>
    [JsonPropertyName("icon")]
    public string? Icon { get; init; }

    /// <summary>Tên biến AutoIt giữ handle control (dùng để định danh khi cần hai chiều).</summary>
    [JsonPropertyName("var")]
    public string? Variable { get; init; }

    /// <summary>Section i18n của tooltip.</summary>
    [JsonPropertyName("ts")]
    public string? TipSection { get; init; }

    /// <summary>Khoá i18n của tooltip.</summary>
    [JsonPropertyName("tk")]
    public string? TipKey { get; init; }

    /// <summary>Tooltip dạng chuỗi tĩnh.</summary>
    [JsonPropertyName("tip")]
    public string? Tip { get; init; }

    /// <summary>Cỡ chữ theo point (đơn vị của AutoIt).</summary>
    [JsonPropertyName("fsz")]
    public double? FontSize { get; init; }

    /// <summary>1 nếu chữ in đậm.</summary>
    [JsonPropertyName("fb")]
    public int? FontBold { get; init; }

    /// <summary>Tên font, khi AutoIt chỉ định khác mặc định.</summary>
    [JsonPropertyName("ff")]
    public string? FontFamily { get; init; }

    /// <summary>Căn lề: <c>l</c>, <c>c</c> hoặc <c>r</c>.</summary>
    [JsonPropertyName("al")]
    public string? Align { get; init; }

    /// <summary>1 nếu chỉ đọc (<c>$ES_READONLY</c>).</summary>
    [JsonPropertyName("ro")]
    public int? ReadOnly { get; init; }

    /// <summary>1 nếu nhiều dòng (<c>$ES_MULTILINE</c> / <c>$BS_MULTILINE</c>).</summary>
    [JsonPropertyName("ml")]
    public int? Multiline { get; init; }

    /// <summary>1 nếu chỉ nhận số (<c>$ES_NUMBER</c>).</summary>
    [JsonPropertyName("num")]
    public int? Numeric { get; init; }

    /// <summary>1 nếu là ô mật khẩu (<c>$ES_PASSWORD</c>).</summary>
    [JsonPropertyName("pw")]
    public int? Password { get; init; }

    /// <summary>1 nếu nút hiển thị icon (<c>$BS_ICON</c>).</summary>
    [JsonPropertyName("bicon")]
    public int? ButtonIcon { get; init; }

    /// <summary>Màu chữ dạng <c>RRGGBB</c>.</summary>
    [JsonPropertyName("fg")]
    public string? Foreground { get; init; }

    /// <summary>Màu nền dạng <c>RRGGBB</c>.</summary>
    [JsonPropertyName("bg")]
    public string? Background { get; init; }

    /// <summary>1 nếu control bị ẩn lúc khởi tạo (<c>$GUI_HIDE</c>).</summary>
    [JsonPropertyName("hidden")]
    public int? Hidden { get; init; }

    /// <summary>1 nếu control bị vô hiệu hoá (<c>$GUI_DISABLE</c>).</summary>
    [JsonPropertyName("disabled")]
    public int? Disabled { get; init; }

    /// <summary>1 nếu checkbox/radio được tick sẵn (<c>$GUI_CHECKED</c>).</summary>
    [JsonPropertyName("checked")]
    public int? Checked { get; init; }

    /// <summary>Khoá cấu hình dạng <c>Section.Key</c> khi bot đọc/ghi giá trị này.</summary>
    [JsonPropertyName("cfg")]
    public string? ConfigKey { get; init; }

    /// <summary>
    /// Các mảnh dữ liệu của combo, đúng thứ tự trong lời gọi <c>GUICtrlSetData</c>.
    /// AutoIt ghép các mảnh lại rồi mới tách theo <c>|</c>, nên phía dựng phải dịch
    /// từng mảnh trước rồi mới tách — một khoá i18n có thể chứa cả danh sách.
    /// </summary>
    [JsonPropertyName("items")]
    public IReadOnlyList<GuiComboItem> Items { get; init; } = Array.Empty<GuiComboItem>();

    /// <summary>
    /// Mục được chọn sẵn: tham số thứ ba của <c>GUICtrlSetData</c> với combo là
    /// *giá trị* cần chọn, nên phải so khớp theo văn bản sau khi dịch.
    /// </summary>
    [JsonPropertyName("sel")]
    public GuiComboItem? Selected { get; init; }
}

/// <summary>Một mảnh dữ liệu của combo: chuỗi tĩnh, hoặc một khoá i18n có bản dịch mặc định.</summary>
public sealed class GuiComboItem
{
    /// <summary>Section i18n; <c>null</c> với mảnh chuỗi tĩnh.</summary>
    [JsonPropertyName("sec")]
    public string? Section { get; init; }

    /// <summary>Khoá i18n; <c>null</c> với mảnh chuỗi tĩnh.</summary>
    [JsonPropertyName("key")]
    public string? Key { get; init; }

    /// <summary>Văn bản tĩnh, hoặc giá trị mặc định của khoá i18n.</summary>
    [JsonPropertyName("txt")]
    public string? Text { get; init; }
}

/// <summary>Một màn hình GUI (một trang tab, hoặc panel Bottom) cùng toàn bộ control của nó.</summary>
public sealed class GuiScreenSpec
{
    /// <summary>Định danh màn hình, ví dụ <c>Village.Misc.NormalVillage</c>.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Chiều rộng vùng vẽ (bằng kích thước cửa sổ con chứa các control).</summary>
    [JsonPropertyName("w")]
    public double Width { get; init; }

    /// <summary>Chiều cao vùng vẽ.</summary>
    [JsonPropertyName("h")]
    public double Height { get; init; }

    /// <summary>Độ dời dọc của tab control con bên trong màn hình (0 nếu tab đặt tại gốc).</summary>
    [JsonPropertyName("tabTop")]
    public double TabTop { get; init; }

    /// <summary>Các control, đã sắp theo thứ tự vẽ (group trước, rồi theo y/x).</summary>
    [JsonPropertyName("controls")]
    public IReadOnlyList<GuiControlSpec> Controls { get; init; } = Array.Empty<GuiControlSpec>();
}

/// <summary>Đọc và tra cứu toàn bộ màn hình GUI đã sinh từ bản AutoIt.</summary>
public sealed class GuiSpecCatalog
{
    private const string ResourceName = "MyBot.UI.Gui.gui-spec.json";

    private readonly Dictionary<string, GuiScreenSpec> _screens;

    private GuiSpecCatalog(Dictionary<string, GuiScreenSpec> screens) => _screens = screens;

    /// <summary>Số màn hình đã nạp.</summary>
    public int Count => _screens.Count;

    /// <summary>Định danh của tất cả màn hình.</summary>
    public IEnumerable<string> Ids => _screens.Keys;

    /// <summary>
    /// Nạp <c>gui-spec.json</c> từ tài nguyên nhúng của assembly.
    /// Ném <see cref="InvalidOperationException"/> nếu tài nguyên thiếu hoặc hỏng —
    /// đây là lỗi đóng gói, không phải lỗi người dùng.
    /// </summary>
    public static GuiSpecCatalog Load()
    {
        Assembly assembly = typeof(GuiSpecCatalog).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream(ResourceName);
        if (stream is null)
        {
            throw new InvalidOperationException(
                $"Không tìm thấy tài nguyên nhúng '{ResourceName}'. Kiểm tra mục EmbeddedResource trong MyBot.UI.csproj.");
        }

        GuiSpecRoot? root = JsonSerializer.Deserialize<GuiSpecRoot>(stream);
        if (root is null || root.Screens.Count == 0)
        {
            throw new InvalidOperationException($"Tài nguyên '{ResourceName}' rỗng hoặc không hợp lệ.");
        }

        return new GuiSpecCatalog(root.Screens.ToDictionary(s => s.Id, StringComparer.Ordinal));
    }

    /// <summary>Lấy màn hình theo định danh; trả về <c>null</c> nếu không có.</summary>
    public GuiScreenSpec? Find(string id) =>
        _screens.TryGetValue(id, out GuiScreenSpec? screen) ? screen : null;

    /// <summary>Lấy màn hình theo định danh, ném nếu không tồn tại.</summary>
    public GuiScreenSpec Get(string id) =>
        Find(id) ?? throw new KeyNotFoundException($"Không có màn hình GUI '{id}' trong gui-spec.json.");

    private sealed class GuiSpecRoot
    {
        [JsonPropertyName("screens")]
        public List<GuiScreenSpec> Screens { get; init; } = new();
    }
}
