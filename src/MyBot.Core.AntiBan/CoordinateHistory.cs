using System;
using System.Collections.Generic;
using System.Linq;

namespace MyBot.Core.AntiBan;

/// <summary>
/// Ghi nhớ các điểm click gần đây và sinh toạ độ mới đủ xa các điểm cũ
/// nhằm tránh việc bot lặp lại cùng một vị trí click.
/// </summary>
public class CoordinateHistory
{
    /// <summary>Số điểm gần nhất được lưu trong lịch sử.</summary>
    public const int HistorySize = 5;

    /// <summary>Khoảng cách tối thiểu (pixel) giữa điểm mới và các điểm cũ.</summary>
    public const double MinDistancePx = 4.0;

    /// <summary>Số lần thử tối đa để tìm được điểm đủ xa.</summary>
    public const int MaxRetries = 10;

    private readonly BoundingBoxClick _box;
    private readonly Queue<(int X, int Y)> _history = new();

    /// <summary>Khởi tạo với bộ sinh toạ độ mặc định.</summary>
    public CoordinateHistory() : this(new BoundingBoxClick()) { }

    /// <summary>Khởi tạo với seed cố định để tái lập chuỗi toạ độ.</summary>
    /// <param name="seed">Seed khởi tạo bộ sinh số ngẫu nhiên.</param>
    public CoordinateHistory(int seed) : this(new BoundingBoxClick(seed)) { }

    /// <summary>Khởi tạo với bộ sinh toạ độ do caller cung cấp.</summary>
    /// <param name="box">Bộ sinh toạ độ click trong hộp.</param>
    public CoordinateHistory(BoundingBoxClick box)
    {
        _box = box ?? throw new ArgumentNullException(nameof(box));
    }

    /// <summary>Số điểm hiện có trong lịch sử.</summary>
    public int Count => _history.Count;

    /// <summary>Danh sách các điểm click gần đây (cũ nhất trước).</summary>
    public IReadOnlyList<(int X, int Y)> RecentPoints => _history.ToList();

    /// <summary>
    /// Sinh toạ độ click mới trong hộp, đảm bảo cách xa các điểm đã ghi nhận trước đó.
    /// </summary>
    /// <param name="x">Toạ độ X góc trên-trái của hộp.</param>
    /// <param name="y">Toạ độ Y góc trên-trái của hộp.</param>
    /// <param name="width">Chiều rộng hộp.</param>
    /// <param name="height">Chiều cao hộp.</param>
    /// <returns>Toạ độ (X, Y) mới và đã được ghi vào lịch sử.</returns>
    public (int X, int Y) GetNextClick(int x, int y, int width, int height)
    {
        (int X, int Y) fallback = default;

        for (int i = 0; i < MaxRetries; i++)
        {
            (int X, int Y) p = _box.NextClick(x, y, width, height);
            fallback = p;
            if (IsFarEnough(p))
            {
                PushHistory(p);
                return p;
            }
        }

        PushHistory(fallback);
        return fallback;
    }

    /// <summary>Xoá toàn bộ lịch sử điểm click đã ghi nhận.</summary>
    public void Clear()
    {
        _history.Clear();
    }

    private bool IsFarEnough((int X, int Y) p)
    {
        foreach ((int X, int Y) h in _history)
        {
            double dx = p.X - h.X;
            double dy = p.Y - h.Y;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist < MinDistancePx)
            {
                return false;
            }
        }

        return true;
    }

    private void PushHistory((int X, int Y) p)
    {
        _history.Enqueue(p);
        while (_history.Count > HistorySize)
        {
            _history.Dequeue();
        }
    }
}
