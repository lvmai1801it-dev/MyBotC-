using System;
using OpenCvSharp;

namespace MyBot.Core.ActionContract;

/// <summary>
/// Định nghĩa một khẳng định thị giác (Visual Assertion) cho DAV (Detect-Act-Verify) pattern.
/// </summary>
/// <param name="Region">Vùng kiểm tra trên khung hình</param>
/// <param name="VerifyFn">Hàm so sánh giữa ảnh trước khi click (preCrop) và ảnh sau khi click (postCrop)</param>
/// <param name="TimeoutMs">Thời gian tối đa để điều kiện thay đổi được thỏa mãn (mặc định 800ms)</param>
/// <param name="Description">Mô tả hành động để log</param>
public record VisualAssertion(
    Rect Region,
    Func<Mat, Mat, bool> VerifyFn,
    int TimeoutMs = 800,
    string Description = "unspecified"
);
