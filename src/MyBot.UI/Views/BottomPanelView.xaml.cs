using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using MyBot.Core.FSM;
using MyBot.UI.Gui;
using MyBot.UI.ViewModels;

namespace MyBot.UI.Views;

/// <summary>
/// Panel đáy (472x135) — bản port của <c>CreateBottomPanel()</c> trong
/// <c>MBR GUI Design Bottom.au3</c>.
///
/// Hình dạng lấy nguyên từ gui-spec.json; phần "sống" (nút Start/Stop/Pause, cờ
/// trạng thái hero, các ô số liệu) được gắn tay vào ViewModel ở đây vì AutoIt điều
/// khiển chúng bằng GUICtrlSetState trong suốt vòng đời bot.
/// </summary>
public partial class BottomPanelView : UserControl
{
    private readonly MainViewModel? _viewModel;
    private readonly GuiScreenView? _screen;

    private readonly List<(Func<HeroState> State, Dictionary<HeroState, FrameworkElement?> Icons)> _heroes = new();

    private FrameworkElement? _start;
    private FrameworkElement? _stop;
    private FrameworkElement? _pause;
    private FrameworkElement? _resume;
    private FrameworkElement? _searchMode;

    /// <summary>Khởi tạo rỗng cho XAML designer.</summary>
    public BottomPanelView()
    {
        InitializeComponent();
    }

    /// <summary>Dựng panel đáy từ màn hình "Bottom" và gắn vào ViewModel.</summary>
    /// <param name="screen">Màn hình đã dựng (472x135).</param>
    /// <param name="viewModel">ViewModel cung cấp trạng thái nút và số liệu.</param>
    public BottomPanelView(GuiScreenView screen, MainViewModel viewModel) : this()
    {
        _screen = screen ?? throw new ArgumentNullException(nameof(screen));
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

        Host.Children.Add(screen);

        WireButtons();
        WireLabels();
        WireHeroes();

        UpdateButtonVisibility();
        UpdateHeroes();

        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.IsStartEnabled):
            case nameof(MainViewModel.IsStopEnabled):
            case nameof(MainViewModel.IsPauseEnabled):
            case nameof(MainViewModel.IsResumeEnabled):
            case nameof(MainViewModel.CurrentState):
                UpdateButtonVisibility();
                break;

            case nameof(MainViewModel.KingState):
            case nameof(MainViewModel.QueenState):
            case nameof(MainViewModel.PrinceState):
            case nameof(MainViewModel.WardenState):
            case nameof(MainViewModel.ChampionState):
            case nameof(MainViewModel.DukeState):
                UpdateHeroes();
                break;

            default:
                break;
        }
    }

    private void WireButtons()
    {
        if (_viewModel is null)
        {
            return;
        }

        _start = Find("$g_hBtnStart");
        _stop = Find("$g_hBtnStop");
        _pause = Find("$g_hBtnPause");
        _resume = Find("$g_hBtnResume");
        _searchMode = Find("$g_hBtnSearchMode");

        BindCommand(_start, _viewModel.StartBotCommand, nameof(MainViewModel.IsStartEnabled));
        BindCommand(_stop, _viewModel.StopBotCommand, nameof(MainViewModel.IsStopEnabled));
        BindCommand(_pause, _viewModel.PauseBotCommand, nameof(MainViewModel.IsPauseEnabled));
        BindCommand(_resume, _viewModel.ResumeCommand, nameof(MainViewModel.IsResumeEnabled));

        // "Search Mode" chưa được port (chế độ chỉ dò làng, không đánh) — để nút
        // ở trạng thái vô hiệu hoá thay vì bấm mà không có tác dụng.
        if (_searchMode is Control searchMode)
        {
            searchMode.IsEnabled = false;
        }

        Button? photo = Find("$g_hBtnMakeScreenshot") as Button;
        if (photo is not null)
        {
            photo.Command = _viewModel.TestCaptureCommand;
            photo.IsEnabled = true;
        }
    }

    private static void BindCommand(FrameworkElement? element, System.Windows.Input.ICommand command, string enabledProperty)
    {
        if (element is not Button button)
        {
            return;
        }

        button.Command = command;
        button.SetBinding(IsEnabledProperty, new Binding(enabledProperty));
    }

    private void UpdateButtonVisibility()
    {
        if (_viewModel is null)
        {
            return;
        }

        bool idle = _viewModel.CurrentState == BotState.Idle;

        Show(_start, idle);
        Show(_searchMode, idle);
        Show(_stop, !idle);
        Show(_pause, _viewModel.IsPauseEnabled);
        Show(_resume, _viewModel.IsResumeEnabled);
    }

    /// <summary>
    /// AutoIt dùng <c>GUICtrlSetState($GUI_HIDE)</c> (giữ nguyên vị trí, không dồn
    /// layout) nên ở đây cũng dùng <see cref="Visibility.Hidden"/> chứ không Collapsed.
    /// </summary>
    private static void Show(FrameworkElement? element, bool visible)
    {
        if (element is not null)
        {
            element.Visibility = visible ? Visibility.Visible : Visibility.Hidden;
        }
    }

    private void WireLabels()
    {
        if (_viewModel is null)
        {
            return;
        }

        BindText("$g_hLblResultGoldNow", nameof(MainViewModel.Gold), "{0:N0}");
        BindText("$g_hLblResultElixirNow", nameof(MainViewModel.Elixir), "{0:N0}");
        BindText("$g_hLblResultDENow", nameof(MainViewModel.DarkElixir), "{0:N0}");
        BindText("$g_hLblResultTrophyNow", nameof(MainViewModel.Trophies), "{0:N0}");
        BindText("$g_hLblResultGemNow", nameof(MainViewModel.Gems), "{0:N0}");
        BindText("$g_hLblResultBuilderNow", nameof(MainViewModel.BuilderText), null);
        BindText("$g_hLbLLabTime", nameof(MainViewModel.LabTime), null);
        BindText("$g_hLbLPetTime", nameof(MainViewModel.PetTime), null);
    }

    private void BindText(string variable, string property, string? format)
    {
        TextBlock? text = FindText(variable);
        if (text is null)
        {
            return;
        }

        var binding = new Binding(property);
        if (format is not null)
        {
            binding.StringFormat = format;
        }

        text.SetBinding(TextBlock.TextProperty, binding);
    }

    private void WireHeroes()
    {
        if (_viewModel is null)
        {
            return;
        }

        MainViewModel model = _viewModel;
        AddHero("King", () => model.KingState);
        AddHero("Queen", () => model.QueenState);
        AddHero("Prince", () => model.PrinceState);
        AddHero("Warden", () => model.WardenState);
        AddHero("Champion", () => model.ChampionState);
        AddHero("Duke", () => model.DukeState);
    }

    /// <summary>
    /// Mỗi hero có 4 icon chồng khít lên nhau (xám/xanh dương/xanh lá/đỏ); AutoIt
    /// hiện đúng một icon theo trạng thái nên ở đây cũng vậy.
    /// </summary>
    private void AddHero(string prefix, Func<HeroState> state)
    {
        var icons = new Dictionary<HeroState, FrameworkElement?>
        {
            [HeroState.Gray] = Find($"$g_hPic{prefix}Gray"),
            [HeroState.Blue] = Find($"$g_hPic{prefix}Blue"),
            [HeroState.Green] = Find($"$g_hPic{prefix}Green"),
            [HeroState.Red] = Find($"$g_hPic{prefix}Red"),
        };

        _heroes.Add((state, icons));
    }

    private void UpdateHeroes()
    {
        foreach ((Func<HeroState> readState, Dictionary<HeroState, FrameworkElement?> icons) in _heroes)
        {
            HeroState state = readState();
            foreach ((HeroState candidate, FrameworkElement? element) in icons)
            {
                Show(element, candidate == state);
            }
        }
    }

    private FrameworkElement? Find(string variable) => _screen?.FindByVariable(variable);

    /// <summary>
    /// Lấy <see cref="TextBlock"/> bên trong control: nhãn AutoIt có chiều cao cố
    /// định nên <see cref="GuiControlFactory"/> bọc nó trong một <see cref="Grid"/>.
    /// </summary>
    private TextBlock? FindText(string variable)
    {
        return Find(variable) switch
        {
            TextBlock text => text,
            Panel { Children.Count: 1 } panel => panel.Children[0] as TextBlock,
            _ => null,
        };
    }
}
