using System;
using System.Text.RegularExpressions;
using MyBot.Core.Abstractions;

namespace MyBot.Core.AntiBan;

public class CircadianSchedule
{
    public const int WakeUpIntervalHours = 1;

    private static readonly Regex RangeRegex = new(@"^(\d{1,2}):(\d{2})-(\d{1,2}):(\d{2})$", RegexOptions.Compiled);

    private readonly ITimeProvider _time;
    private readonly TimeZoneInfo _tz;
    private readonly bool _enableWakeUp;
    private readonly TimeSpan _activeStart;
    private readonly TimeSpan _activeEnd;

    private DateTime _lastWakeUpLocal = DateTime.MinValue;

    public CircadianSchedule(ITimeProvider time)
        : this(time, "06:00-23:30", TimeZoneInfo.Local, false) { }

    public CircadianSchedule(ITimeProvider time, string activeRange)
        : this(time, activeRange, TimeZoneInfo.Local, false) { }

    public CircadianSchedule(ITimeProvider time, string activeRange, TimeZoneInfo tz, bool enableWakeUpCollect = false)
    {
        _time = time ?? throw new ArgumentNullException(nameof(time));
        _tz = tz ?? throw new ArgumentNullException(nameof(tz));
        _enableWakeUp = enableWakeUpCollect;

        (TimeSpan start, TimeSpan end) parsed = ParseActiveRange(activeRange);
        _activeStart = parsed.start;
        _activeEnd = parsed.end;
    }

    public TimeSpan ActiveStart => _activeStart;
    public TimeSpan ActiveEnd => _activeEnd;

    public bool ShouldSleep()
    {
        DateTime nowLocal = TimeZoneInfo.ConvertTimeFromUtc(_time.UtcNow, _tz);
        TimeSpan t = nowLocal.TimeOfDay;
        return !(t >= _activeStart && t <= _activeEnd);
    }

    public bool ShouldWakeUp()
    {
        if (!_enableWakeUp || !ShouldSleep())
        {
            return false;
        }

        DateTime nowLocal = TimeZoneInfo.ConvertTimeFromUtc(_time.UtcNow, _tz);
        if (nowLocal - _lastWakeUpLocal < TimeSpan.FromHours(WakeUpIntervalHours))
        {
            return false;
        }

        _lastWakeUpLocal = nowLocal;
        return true;
    }

    private static (TimeSpan start, TimeSpan end) ParseActiveRange(string activeRange)
    {
        if (string.IsNullOrWhiteSpace(activeRange))
        {
            throw new ArgumentException("activeRange không được để trống", nameof(activeRange));
        }

        Match match = RangeRegex.Match(activeRange);
        if (!match.Success)
        {
            throw new ArgumentException($"Định dạng activeRange '{activeRange}' không hợp lệ (kỳ vọng HH:MM-HH:MM)", nameof(activeRange));
        }

        int startH = int.Parse(match.Groups[1].Value);
        int startM = int.Parse(match.Groups[2].Value);
        int endH = int.Parse(match.Groups[3].Value);
        int endM = int.Parse(match.Groups[4].Value);

        if (startH < 0 || startH > 23 || startM < 0 || startM > 59 ||
            endH < 0 || endH > 23 || endM < 0 || endM > 59)
        {
            throw new ArgumentException($"Giờ hoặc phút trong activeRange '{activeRange}' nằm ngoài phạm vi hợp lệ", nameof(activeRange));
        }

        var start = new TimeSpan(startH, startM, 0);
        var end = new TimeSpan(endH, endM, 0);

        if (start >= end)
        {
            throw new ArgumentException("activeRange phải có start < end (chưa hỗ trợ wrap qua đêm)", nameof(activeRange));
        }

        (TimeSpan start, TimeSpan end) result = (start, end);
        return result;
    }
}
