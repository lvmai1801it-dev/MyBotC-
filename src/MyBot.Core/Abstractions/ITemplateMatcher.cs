using OpenCvSharp;

namespace MyBot.Core.Abstractions;

public record MatchResult(int X, int Y, int Width, int Height, double Score);

public interface ITemplateMatcher
{
    MatchResult? Find(Mat haystack, Mat needle, double threshold = 0.8);
    IEnumerable<MatchResult> FindAll(Mat haystack, Mat needle, double threshold = 0.8);
}
