using System;

namespace MyBot.Game.CoC.Village;

/// <summary>
/// Kết quả một lượt thu hoạch mỏ.
/// </summary>
/// <param name="Tapped">Số điểm đã tap thành công.</param>
/// <param name="SkippedFull">Số mỏ bị bỏ vì kho đầy.</param>
/// <param name="SkippedDuplicate">Số mỏ bị bỏ vì trùng vị trí trong cùng loại.</param>
/// <param name="Duration">Tổng thời gian của lượt thu hoạch.</param>
public sealed record CollectorResult(int Tapped, int SkippedFull, int SkippedDuplicate, TimeSpan Duration);
