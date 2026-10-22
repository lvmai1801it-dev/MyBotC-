using MyBot.Core.Abstractions;
using OpenCvSharp;

namespace MyBot.Game.CoC.Detection;

/// <summary>Detects the "storage full" UI overlay via template matching.</summary>
public class StorageDetector
{
    private const double MatchThreshold = 0.75;

    private readonly ITemplateMatcher _matcher;

    /// <summary>Creates a detector backed by the given template matcher.</summary>
    public StorageDetector(ITemplateMatcher matcher)
    {
        _matcher = matcher ?? throw new ArgumentNullException(nameof(matcher));
    }

    /// <summary>Returns true when the storage-full template is present in the frame.</summary>
    public bool HasFullStorage(Mat frame, Mat storageFullTemplate)
    {
        if (frame is null || frame.Empty() || storageFullTemplate is null || storageFullTemplate.Empty())
        {
            return false;
        }

        MatchResult? match = _matcher.Find(frame, storageFullTemplate, MatchThreshold);
        return match is not null;
    }
}
