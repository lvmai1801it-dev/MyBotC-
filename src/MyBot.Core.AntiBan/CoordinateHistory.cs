using System;
using System.Collections.Generic;
using System.Linq;

namespace MyBot.Core.AntiBan;

public class CoordinateHistory
{
    public const int HistorySize = 5;
    public const double MinDistancePx = 4.0;
    public const int MaxRetries = 10;

    private readonly BoundingBoxClick _box;
    private readonly Queue<(int X, int Y)> _history = new();

    public CoordinateHistory() : this(new BoundingBoxClick()) { }

    public CoordinateHistory(int seed) : this(new BoundingBoxClick(seed)) { }

    public CoordinateHistory(BoundingBoxClick box)
    {
        _box = box ?? throw new ArgumentNullException(nameof(box));
    }

    public int Count => _history.Count;

    public IReadOnlyList<(int X, int Y)> RecentPoints => _history.ToList();

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
