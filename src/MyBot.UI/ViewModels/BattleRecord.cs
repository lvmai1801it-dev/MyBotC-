namespace MyBot.UI.ViewModels;

/// <summary>
/// Bản ghi kết quả một trận đánh, dùng để hiển thị trong bảng Stats.
/// </summary>
public record BattleRecord(string Time, string Type, int Gold, int Elixir, int Trophies);
