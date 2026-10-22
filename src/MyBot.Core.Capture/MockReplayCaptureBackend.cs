using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenCvSharp;
using MyBot.Core.Abstractions;

namespace MyBot.Core.Capture;

public class MockReplayCaptureBackend : ICaptureBackend
{
    private List<string> _imagePaths = new();
    private int _currentIndex = 0;
    private bool _loop = false;
    private bool _isRunning;

    public bool IsRunning => _isRunning;

    public bool Initialize(EmulatorTarget target)
    {
        // Trong Mock, tham số MockRecordingPath chứa đường dẫn tới thư mục ảnh
        string? directoryPath = target.MockRecordingPath;

        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            return false;

        // Lấy tất cả file ảnh (.png, .jpg) và sắp xếp theo tên
        var files = Directory.GetFiles(directoryPath, "*.*")
            .Where(f => f.EndsWith(".png") || f.EndsWith(".jpg") || f.EndsWith(".jpeg"))
            .OrderBy(f => f)
            .ToList();

        if (files.Count == 0) return false;

        _imagePaths = files;
        _currentIndex = 0;
        _isRunning = true;
        return true;
    }

    public void SetLoop(bool loop) => _loop = loop;

    public Mat GrabFrame()
    {
        if (!_isRunning || _imagePaths.Count == 0) return new Mat();

        string path = _imagePaths[_currentIndex];
        _currentIndex++;

        // Nếu hết ảnh
        if (_currentIndex >= _imagePaths.Count)
        {
            if (_loop)
                _currentIndex = 0; // Quay lại từ đầu
            else
                _isRunning = false; // Dừng (trả về frame cuối hoặc rỗng ở lần sau)
        }

        var mat = Cv2.ImRead(path, ImreadModes.Color);
        return mat ?? new Mat();
    }

    public void Dispose()
    {
        _isRunning = false;
    }
}
