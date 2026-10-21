using OpenCvSharp;
using MyBot.Core.Enums;

namespace MyBot.Core.Abstractions;

public interface IStateClassifier
{
    GameState Classify(Mat frame);
    double GetLastConfidence();
}
