using MyBot.Core.Abstractions;
using MyBot.Core.AntiBan;
using OpenCvSharp;

namespace MyBot.Game.CoC.Village;

/// <summary>
/// Scans the village frame for full collector bubbles and taps each one to harvest.
/// Minimal T5.1 implementation used by the UI test harness.
/// </summary>
public class CollectorHarvester
{
    private const double DefaultThreshold = 0.6;

    private readonly ITemplateMatcher _matcher;
    private readonly IInputBackend _input;
    private readonly HumanDelay _delay;

    /// <summary>Creates a harvester using the given matcher, input backend and delay engine.</summary>
    /// <param name="matcher">Template matcher used to locate full collectors.</param>
    /// <param name="input">Input backend used to tap collectors.</param>
    /// <param name="delay">Human-like delay engine between taps.</param>
    public CollectorHarvester(ITemplateMatcher matcher, IInputBackend input, HumanDelay delay)
    {
        _matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
        _input = input ?? throw new ArgumentNullException(nameof(input));
        _delay = delay ?? throw new ArgumentNullException(nameof(delay));
    }

    /// <summary>Finds every full collector in the frame and taps its center.</summary>
    /// <param name="frame">Current village screenshot.</param>
    /// <param name="template">Collector bubble template.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of collectors that were tapped.</returns>
    public async Task<int> HarvestAllAsync(Mat frame, Mat template, CancellationToken ct = default)
    {
        if (frame is null || frame.Empty() || template is null || template.Empty())
        {
            return 0;
        }

        int count = 0;

        foreach (MatchResult match in _matcher.FindAll(frame, template, DefaultThreshold))
        {
            ct.ThrowIfCancellationRequested();

            int centerX = match.X + match.Width / 2;
            int centerY = match.Y + match.Height / 2;

            _input.Tap(centerX, centerY);
            count++;

            await _delay.DelayAsync(200, 60, 100, 400, ct);
        }

        return count;
    }
}
