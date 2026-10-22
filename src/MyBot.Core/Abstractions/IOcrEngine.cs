using OpenCvSharp;

namespace MyBot.Core.Abstractions;

public interface IOcrEngine
{
    bool Initialize(string modelPath);

    /// <summary>Đọc chữ trong vùng ảnh bằng model mặc định.</summary>
    string Recognize(Mat region);

    /// <summary>
    /// Đọc chữ trong vùng ảnh bằng model chỉ định (VD: "coc-Builders" cho bộ đếm builder).
    /// Engine không hỗ trợ nhiều model sẽ bỏ qua tham số này.
    /// </summary>
    string Recognize(Mat region, string model) => Recognize(region);
}
