using System;
using MyBot.Core.Abstractions;

namespace MyBot.Core.AntiBan;

/// <summary>
/// Đại diện cho hồ sơ hành vi (anti-ban profile) của một tài khoản,
/// đóng gói các thành phần sinh ngẫu nhiên độc lập dựa trên behavioral seed.
/// </summary>
public class BehavioralProfile
{
    /// <summary>
    /// Khởi tạo hồ sơ hành vi cho một tài khoản. Mỗi thành phần con nhận một nhánh seed
    /// khác nhau (seed+1..seed+4) để tránh trùng lặp chuỗi số ngẫu nhiên giữa các module.
    /// </summary>
    /// <param name="seed">Behavioral seed định danh "vân tay" hành vi của tài khoản.</param>
    /// <param name="time">Nguồn thời gian dùng cho các khoảng trễ.</param>
    public BehavioralProfile(int seed, ITimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);

        Seed = seed;
        Delays = new HumanDelay(time, seed + 1);
        BoxClick = new BoundingBoxClick(seed + 2);
        Bezier = new BezierPath(seed + 3);
        ClickHistory = new CoordinateHistory(seed + 4);
    }

    /// <summary>Behavioral seed gốc của hồ sơ.</summary>
    public int Seed { get; }

    /// <summary>Bộ sinh độ trễ người (Gaussian) của hồ sơ.</summary>
    public HumanDelay Delays { get; }

    /// <summary>Bộ sinh toạ độ click trong hộp của hồ sơ.</summary>
    public BoundingBoxClick BoxClick { get; }

    /// <summary>Bộ sinh quỹ đạo chuột Bezier của hồ sơ.</summary>
    public BezierPath Bezier { get; }

    /// <summary>Lịch sử toạ độ click chống lặp của hồ sơ.</summary>
    public CoordinateHistory ClickHistory { get; }
}
