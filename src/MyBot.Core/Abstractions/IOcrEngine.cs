using OpenCvSharp;

namespace MyBot.Core.Abstractions;

public interface IOcrEngine
{
    bool Initialize(string modelPath);
    string Recognize(Mat region);
}
