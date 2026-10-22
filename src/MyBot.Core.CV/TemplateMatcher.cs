using System;
using System.Collections.Generic;
using OpenCvSharp;
using MyBot.Core.Abstractions;

namespace MyBot.Core.CV;

/// <summary>
/// Bộ so khớp ảnh mẫu (template matching) dùng OpenCV, tìm ảnh nhỏ (needle)
/// bên trong ảnh lớn (haystack) theo hệ số tương quan chuẩn hoá.
/// </summary>
public class TemplateMatcher : ITemplateMatcher
{
    /// <summary>Ngưỡng tin cậy mặc định.</summary>
    public const double DefaultThreshold = 0.8;

    /// <summary>
    /// Tìm kết quả khớp tốt nhất của <paramref name="needle"/> trong <paramref name="haystack"/>.
    /// </summary>
    /// <param name="haystack">Ảnh lớn cần tìm.</param>
    /// <param name="needle">Ảnh mẫu nhỏ cần tìm.</param>
    /// <param name="threshold">Ngưỡng tin cậy tối thiểu (0..1).</param>
    /// <returns>Kết quả khớp đầu tiên vượt ngưỡng, hoặc <c>null</c> nếu không có.</returns>
    /// <exception cref="ArgumentNullException">Khi một trong hai ảnh là null.</exception>
    /// <exception cref="ArgumentException">Khi ảnh mẫu lớn hơn ảnh cần tìm.</exception>
    public MatchResult? Find(Mat haystack, Mat needle, double threshold = DefaultThreshold)
    {
        ValidateInputs(haystack, needle);

        using var result = Match(haystack, needle, out double maxVal, out Point maxLoc);

        return maxVal >= threshold
            ? new MatchResult(maxLoc.X, maxLoc.Y, needle.Width, needle.Height, maxVal)
            : null;
    }

    /// <summary>
    /// Tìm tất cả các vị trí khớp của <paramref name="needle"/> trong <paramref name="haystack"/>.
    /// Sau mỗi lần tìm thấy, vùng tương ứng trên ma trận kết quả bị "tô đen" để tránh trùng lặp.
    /// </summary>
    /// <param name="haystack">Ảnh lớn cần tìm.</param>
    /// <param name="needle">Ảnh mẫu nhỏ cần tìm.</param>
    /// <param name="threshold">Ngưỡng tin cậy tối thiểu (0..1).</param>
    /// <returns>Danh sách các kết quả khớp không trùng lặp.</returns>
    /// <exception cref="ArgumentNullException">Khi một trong hai ảnh là null.</exception>
    /// <exception cref="ArgumentException">Khi ảnh mẫu lớn hơn ảnh cần tìm.</exception>
    public IEnumerable<MatchResult> FindAll(Mat haystack, Mat needle, double threshold = DefaultThreshold)
    {
        ValidateInputs(haystack, needle);

        var matches = new List<MatchResult>();
        using var result = Match(haystack, needle, out _, out _);

        while (true)
        {
            Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out Point maxLoc);
            if (maxVal < threshold || maxVal <= 0.0)
            {
                break;
            }

            matches.Add(new MatchResult(maxLoc.X, maxLoc.Y, needle.Width, needle.Height, maxVal));

            // Tô đen vùng vừa khớp (toạ độ trên ma trận result trùng với toạ độ góc trên-trái
            // của khớp trong haystack) để vòng lặp sau không trả về cùng một vị trí.
            var suppress = new Rect(maxLoc.X, maxLoc.Y, needle.Width, needle.Height);
            Cv2.Rectangle(result, suppress, Scalar.Black, -1);
        }

        return matches;
    }

    /// <summary>Chạy MatchTemplate và trả về ma trận điểm số cùng giá trị lớn nhất.</summary>
    private static Mat Match(Mat haystack, Mat needle, out double maxVal, out Point maxLoc)
    {
        var result = new Mat(
            haystack.Rows - needle.Rows + 1,
            haystack.Cols - needle.Cols + 1,
            MatType.CV_32FC1);

        Cv2.MatchTemplate(haystack, needle, result, TemplateMatchModes.CCoeffNormed);
        Cv2.MinMaxLoc(result, out _, out maxVal, out _, out maxLoc);

        return result;
    }

    /// <summary>Kiểm tra null và ràng buộc kích thước giữa hai ảnh.</summary>
    private static void ValidateInputs(Mat haystack, Mat needle)
    {
        ArgumentNullException.ThrowIfNull(haystack);
        ArgumentNullException.ThrowIfNull(needle);

        if (haystack.Empty())
        {
            throw new ArgumentException("Haystack must not be empty.", nameof(haystack));
        }

        if (needle.Empty())
        {
            throw new ArgumentException("Needle must not be empty.", nameof(needle));
        }

        if (needle.Width > haystack.Width || needle.Height > haystack.Height)
        {
            throw new ArgumentException(
                $"Needle ({needle.Width}x{needle.Height}) is larger than haystack ({haystack.Width}x{haystack.Height}).",
                nameof(needle));
        }
    }
}
