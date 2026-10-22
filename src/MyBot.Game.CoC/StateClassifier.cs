using MyBot.Core.Abstractions;
using MyBot.Core.Enums;
using OpenCvSharp;

namespace MyBot.Game.CoC;

/// <summary>
/// Classifies the current Clash of Clans screen by matching known UI templates
/// in priority order: Popup, Clouds, Battle, MainVillage.
/// </summary>
public sealed class StateClassifier : IStateClassifier
{
    private const double MatchThreshold = 0.8;

    private readonly ITemplateMatcher _matcher;

    // Placeholder templates until real game screenshots are available.
    private readonly Mat _popupTemplate = new(10, 10, MatType.CV_8UC3);
    private readonly Mat _cloudsTemplate = new(10, 10, MatType.CV_8UC3);
    private readonly Mat _battleTemplate = new(10, 10, MatType.CV_8UC3);
    private readonly Mat _villageTemplate = new(10, 10, MatType.CV_8UC3);

    private double _lastConfidence;

    /// <summary>Creates a classifier backed by the given template matcher.</summary>
    /// <param name="matcher">Template matcher used to search the frame.</param>
    public StateClassifier(ITemplateMatcher matcher)
    {
        _matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
    }

    /// <inheritdoc />
    public GameState Classify(Mat frame)
    {
        _lastConfidence = 0;

        if (frame is null || frame.Empty())
        {
            return GameState.Unknown;
        }

        MatchResult? match = _matcher.Find(frame, _popupTemplate, MatchThreshold);
        if (match is not null)
        {
            _lastConfidence = match.Score;
            return GameState.Popup;
        }

        match = _matcher.Find(frame, _cloudsTemplate, MatchThreshold);
        if (match is not null)
        {
            _lastConfidence = match.Score;
            return GameState.Clouds;
        }

        match = _matcher.Find(frame, _battleTemplate, MatchThreshold);
        if (match is not null)
        {
            _lastConfidence = match.Score;
            return GameState.Battle;
        }

        match = _matcher.Find(frame, _villageTemplate, MatchThreshold);
        if (match is not null)
        {
            _lastConfidence = match.Score;
            return GameState.MainVillage;
        }

        return GameState.Unknown;
    }

    /// <inheritdoc />
    public double GetLastConfidence() => _lastConfidence;
}
