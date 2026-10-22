using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenCvSharp;
using MyBot.Core.Abstractions;

namespace MyBot.Core.Capture;

/// <summary>
/// Backend chụp hình giả lập dùng để test: phát lại lần lượt các ảnh trong một thư mục
/// (đường dẫn lấy từ <c>EmulatorTarget.MockRecordingPath</c>).
/// </summary>
public class MockReplayCaptureBackend : ICaptureBackend
{
    private List<string> _imagePaths = new();
    private int _currentIndex = 0;
    private bool _loop = false;
    private bool _isRunning;

    /// <summary>Cho biết backend còn ảnh để phát lại hay không.</summary>
    public bool IsRunning => _isRunning;

    /// <summary>Nạp danh sách ảnh từ thư mục ghi hình của target.</summary>
    /// <param name="target">Thông tin thiết bị đích (dùng MockRecordingPath).</param>
    /// <returns>True nếu tìm thấy ít nhất một file ảnh hợp lệ.</returns>
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

    /// <summary>Bật/tắt chế độ phát lại lặp vô hạn.</summary>
    /// <param name="loop">True để quay lại ảnh đầu tiên sau khi hết danh sách.</param>
    public void SetLoop(bool loop) => _loop = loop;

    /// <summary>Lấy khung hình kế tiếp trong danh sách phát lại.</summary>
    /// <returns>Ảnh kế tiếp, hoặc Mat rỗng nếu đã hết ảnh và không bật loop.</returns>
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

    /// <summary>Đánh dấu backend ngừng hoạt động.</summary>
    public void Dispose()
    {
        _isRunning = false;
    }
}
