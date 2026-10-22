using System;
using System.Linq;
using System.Runtime.InteropServices;
using FluentAssertions;
using OpenCvSharp;
using MyBot.Core.CV;

namespace MyBot.Core.Tests;

public class TemplateMatcherTests
{
    private readonly TemplateMatcher _matcher = new();

    /// <summary>
    /// Tạo ảnh nhiễu xám trong khoảng [min, max). Nền phải có texture vì
    /// CCoeffNormed là hệ số tương quan: ảnh phẳng (phương sai 0) là trường hợp
    /// suy biến, OpenCV trả score 1.0 ở mọi vị trí.
    /// </summary>
    private static Mat CreateNoise(int width, int height, int min, int max, int seed)
    {
        var rng = new Random(seed);
        var data = new byte[width * height];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)rng.Next(min, max);
        }

        return new Mat(height, width, MatType.CV_8UC1, data);
    }

    /// <summary>Tạo nền tối có nhiễu.</summary>
    private static Mat CreateDarkNoise(int width, int height, int seed = 1)
        => CreateNoise(width, height, 0, 60, seed);

    /// <summary>Vẽ một khối texture sáng lên vùng chỉ định của canvas.</summary>
    private static void PaintBrightTexture(Mat canvas, int x, int y, int size, int seed = 7)
    {
        using var patch = CreateNoise(size, size, 160, 256, seed);
        using var roi = new Mat(canvas, new Rect(x, y, size, size));
        patch.CopyTo(roi);
    }

    /// <summary>Cắt vùng ảnh làm needle.</summary>
    private static Mat Crop(Mat source, int x, int y, int width, int height)
        => new Mat(source, new Rect(x, y, width, height)).Clone();

    /// <summary>Thêm nhiễu nhỏ để score &lt; 1.0 nhưng vẫn khớp đúng vị trí.</summary>
    private static void AddNoise(Mat mat, int seed = 99, int amplitude = 25)
    {
        int total = mat.Rows * mat.Cols;
        var buffer = new byte[total];
        Marshal.Copy(mat.Data, buffer, 0, total);

        var rng = new Random(seed);
        for (int i = 0; i < buffer.Length; i++)
        {
            buffer[i] = (byte)Math.Clamp(buffer[i] + rng.Next(-amplitude, amplitude + 1), 0, 255);
        }

        Marshal.Copy(buffer, 0, mat.Data, total);
    }

    [Fact]
    public void Find_ExactMatch_ReturnsValidResult()
    {
        using var haystack = CreateDarkNoise(100, 100);
        PaintBrightTexture(haystack, 40, 40, 20);
        using var needle = Crop(haystack, 40, 40, 20, 20);

        var result = _matcher.Find(haystack, needle, 0.99);

        result.Should().NotBeNull();
        result!.X.Should().Be(40);
        result.Y.Should().Be(40);
        result.Width.Should().Be(20);
        result.Height.Should().Be(20);
        result.Score.Should().BeApproximately(1.0, 0.01);
    }

    [Fact]
    public void Find_NoMatch_ReturnsNull()
    {
        using var haystack = CreateDarkNoise(100, 100, seed: 1);

        // Needle là texture độc lập, không xuất hiện trong haystack.
        using var needle = CreateNoise(20, 20, 160, 256, seed: 404);

        var result = _matcher.Find(haystack, needle, 0.9);

        result.Should().BeNull();
    }

    [Fact]
    public void Find_ThresholdBoundary()
    {
        using var haystack = CreateDarkNoise(100, 100);
        PaintBrightTexture(haystack, 40, 40, 20);

        // Needle gần khớp nhưng không hoàn hảo -> score cao nhưng < 1.0.
        using var needle = Crop(haystack, 40, 40, 20, 20);
        AddNoise(needle);

        var scored = _matcher.Find(haystack, needle, 0.0);

        scored.Should().NotBeNull();
        scored!.Score.Should().BeInRange(0.1, 0.99);

        double score = scored.Score;
        var loose = _matcher.Find(haystack, needle, score - 0.01);
        var strict = _matcher.Find(haystack, needle, score + 0.01);

        loose.Should().NotBeNull();
        strict.Should().BeNull();
    }

    [Fact]
    public void Find_NeedleLargerThanHaystack_Throws()
    {
        using var haystack = CreateDarkNoise(100, 100);
        using var needle = CreateDarkNoise(200, 200);

        Action act = () => _matcher.Find(haystack, needle, 0.8);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FindAll_MultipleMatches()
    {
        using var haystack = CreateDarkNoise(200, 200);

        // 3 khối texture giống hệt nhau ở 3 vị trí khác nhau.
        PaintBrightTexture(haystack, 10, 10, 10);
        PaintBrightTexture(haystack, 90, 90, 10);
        PaintBrightTexture(haystack, 160, 150, 10);

        using var needle = Crop(haystack, 10, 10, 10, 10);

        var results = _matcher.FindAll(haystack, needle, 0.9).ToList();

        results.Should().HaveCount(3);
        results.Should().Contain(r => r.X == 10 && r.Y == 10);
        results.Should().Contain(r => r.X == 90 && r.Y == 90);
        results.Should().Contain(r => r.X == 160 && r.Y == 150);
    }
}
