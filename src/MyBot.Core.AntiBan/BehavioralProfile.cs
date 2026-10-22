using System;
using MyBot.Core.Abstractions;

namespace MyBot.Core.AntiBan;

/// <summary>
/// Đại diện cho hồ sơ hành vi (anti-ban profile) của một tài khoản,
/// đóng gói các thành phần sinh ngẫu nhiên độc lập dựa trên behavioral seed.
/// </summary>
public class BehavioralProfile
{
    public BehavioralProfile(int seed, ITimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);

        Seed = seed;
        Delays = new HumanDelay(time, seed + 1);
        BoxClick = new BoundingBoxClick(seed + 2);
        Bezier = new BezierPath(seed + 3);
        ClickHistory = new CoordinateHistory(seed + 4);
    }

    public int Seed { get; }
    public HumanDelay Delays { get; }
    public BoundingBoxClick BoxClick { get; }
    public BezierPath Bezier { get; }
    public CoordinateHistory ClickHistory { get; }
}
