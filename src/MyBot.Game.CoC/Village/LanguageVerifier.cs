using System;
using System.Collections.Generic;
using System.IO;
using MyBot.Core.Abstractions;
using MyBot.Core.CV;
using OpenCvSharp;

namespace MyBot.Game.CoC.Village;

/// <summary>Xác nhận game đang dùng tiếng Anh bằng nút Attack (port <c>TestLanguage</c>).</summary>
public interface ILanguageVerifier
{
    /// <summary>True nếu có thể kiểm tra (có DLL và template nút Attack).</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Tìm nút Attack ở góc dưới trái. Trả false khi không thấy — caller nên dừng bot
    /// như <c>btnStop()</c> của AutoIt.
    /// </summary>
    /// <param name="capture">Hàm chụp khung hình mới.</param>
    /// <returns>True nếu xác nhận được tiếng Anh.</returns>
    bool Verify(Func<Mat> capture);
}

/// <summary>
/// Port <c>TestLanguage</c>: <c>findButton("AttackButton", Default, 1, True)</c>.
/// Nếu thiếu DLL/template thì chỉ cảnh báo và bỏ qua (không chặn bot).
/// </summary>
public sealed class LanguageVerifier : ILanguageVerifier
{
    /// <summary>Vùng nút Attack: <c>(0, 540 + $g_iBottomOffsetY)-(160, 672 + $g_iBottomOffsetY)</c>.</summary>
    public static readonly Rect AttackButtonArea = Rect.FromLTRB(0, 540 + 60, 160, 672 + 60);

    private readonly ITileSearcher _searcher;
    private readonly string _templateDirectory;
    private readonly IBotLogger? _logger;

    /// <summary>Khởi tạo bộ kiểm tra ngôn ngữ.</summary>
    /// <param name="searcher">Bộ tìm template ImgLoc.</param>
    /// <param name="templateDirectory">Thư mục chứa <c>AttackButton*</c>.</param>
    /// <param name="logger">Logger tuỳ chọn.</param>
    public LanguageVerifier(
        ITileSearcher searcher,
        string templateDirectory,
        IBotLogger? logger = null)
    {
        _searcher = searcher ?? throw new ArgumentNullException(nameof(searcher));
        _templateDirectory = templateDirectory ?? throw new ArgumentNullException(nameof(templateDirectory));
        _logger = logger;
    }

    /// <inheritdoc />
    public bool IsAvailable =>
        _searcher.IsAvailable && Directory.Exists(_templateDirectory);

    /// <inheritdoc />
    public bool Verify(Func<Mat> capture)
    {
        ArgumentNullException.ThrowIfNull(capture);

        if (!IsAvailable)
        {
            _logger?.Warning(
                $"Không có template nút Attack tại {_templateDirectory} — bỏ qua kiểm tra ngôn ngữ.");
            return true;
        }

        using Mat frame = capture() ?? new Mat();
        IReadOnlyList<NamedTilePoint> buttons = _searcher.SearchDirectory(
            frame,
            _templateDirectory,
            AttackButtonArea,
            maxPoints: 1);

        if (buttons.Count > 0)
        {
            _logger?.Info("Language Setting: English");
            return true;
        }

        _logger?.Error("Unable to confirm English language from Battle button");
        return false;
    }
}
