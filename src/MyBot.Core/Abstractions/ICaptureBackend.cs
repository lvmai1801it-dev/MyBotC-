using System;
using OpenCvSharp;

namespace MyBot.Core.Abstractions;

public interface ICaptureBackend : IDisposable
{
    bool Initialize(EmulatorTarget target);
    Mat GrabFrame();
    bool IsRunning { get; }
}
