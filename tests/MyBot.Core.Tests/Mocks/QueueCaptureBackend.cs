using System;
using System.Collections.Generic;
using MyBot.Core.Abstractions;
using OpenCvSharp;

namespace MyBot.Core.Tests.Mocks;

/// <summary>
/// Capture backend giả: phát lần lượt các frame đã xếp hàng; hết hàng thì trả Mat rỗng.
/// </summary>
public sealed class QueueCaptureBackend : ICaptureBackend
{
    private readonly Queue<Func<Mat>> _frames = new();

    /// <summary>Số lần <see cref="GrabFrame"/> đã được gọi.</summary>
    public int GrabCount { get; private set; }

    /// <summary>Mock luôn ở trạng thái sẵn sàng.</summary>
    public bool IsRunning => true;

    /// <summary>Không cần khởi tạo.</summary>
    /// <param name="target">Thông tin thiết bị đích (bị bỏ qua).</param>
    /// <returns>Luôn true.</returns>
    public bool Initialize(EmulatorTarget target) => true;

    /// <summary>Xếp một frame; backend trả bản clone để caller tự dispose.</summary>
    /// <param name="frame">Frame cần trả.</param>
    public void Enqueue(Mat frame)
    {
        _frames.Enqueue(frame.Clone);
    }

    /// <summary>Xếp một frame rỗng (mô phỏng chụp thất bại).</summary>
    public void EnqueueEmpty()
    {
        _frames.Enqueue(() => new Mat());
    }

    /// <summary>Lấy frame kế tiếp; hết hàng thì trả Mat rỗng.</summary>
    /// <returns>Frame kế tiếp hoặc Mat rỗng.</returns>
    public Mat GrabFrame()
    {
        GrabCount++;
        return _frames.Count > 0 ? _frames.Dequeue()() : new Mat();
    }

    /// <summary>Không có tài nguyên cần giải phóng.</summary>
    public void Dispose()
    {
    }
}
