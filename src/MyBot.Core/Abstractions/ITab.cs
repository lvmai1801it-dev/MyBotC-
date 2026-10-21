using System.Windows.Controls;

namespace MyBot.Core.Abstractions;

public interface ITab
{
    string TabId { get; }
    string TitleKey { get; }
    int Priority { get; }
    bool IsLazy { get; }
    UserControl CreateView();
    void OnStateChanged(Enums.GameState state);
}
