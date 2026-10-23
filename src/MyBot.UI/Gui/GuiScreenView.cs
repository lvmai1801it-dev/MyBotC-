using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace MyBot.UI.Gui;

/// <summary>
/// Vẽ một màn hình GUI (một trang tab hoặc panel Bottom) bằng cách đặt tuyệt đối
/// từng control lên một <see cref="Canvas"/> kích thước cố định.
///
/// AutoIt không có layout engine: mọi control đều được tạo với toạ độ tuyệt đối
/// theo pixel tại 96 DPI. Canvas là thành phần WPF duy nhất giữ nguyên ngữ nghĩa đó.
/// </summary>
public sealed class GuiScreenView : Canvas
{
    private readonly Dictionary<string, FrameworkElement> _byVariable = new(StringComparer.Ordinal);

    /// <summary>Dựng toàn bộ control của màn hình.</summary>
    /// <param name="screen">Mô tả màn hình đọc từ <c>gui-spec.json</c>.</param>
    /// <param name="factory">Bộ dựng control dùng chung cho màn hình này.</param>
    public GuiScreenView(GuiScreenSpec screen, GuiControlFactory factory)
    {
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(factory);

        ScreenId = screen.Id;
        Width = screen.Width;
        Height = screen.Height;
        Background = GuiPalette.TabBodyBackground;
        ClipToBounds = true;
        SnapsToDevicePixels = true;

        foreach (GuiControlSpec spec in screen.Controls)
        {
            FrameworkElement element = factory.Create(spec);
            SetLeft(element, spec.X);
            SetTop(element, spec.Y);
            Children.Add(element);

            if (spec.Variable is not null)
            {
                _byVariable[spec.Variable] = element;
            }
        }
    }

    /// <summary>Định danh màn hình, ví dụ <c>Village.Misc.NormalVillage</c>.</summary>
    public string ScreenId { get; }

    /// <summary>Số control đã dựng.</summary>
    public int ControlCount => Children.Count;

    /// <summary>
    /// Tìm phần tử theo tên biến AutoIt (ví dụ <c>$g_hChkCollect</c>) để gắn
    /// xử lý sự kiện hoặc đồng bộ với ViewModel.
    /// </summary>
    public FrameworkElement? FindByVariable(string variable) =>
        _byVariable.TryGetValue(variable, out FrameworkElement? element) ? element : null;
}
