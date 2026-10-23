using System;
using System.Threading;
using System.Threading.Tasks;
using MyBot.Core.Abstractions;
using MyBot.Game.CoC.Village;
using OpenCvSharp;

namespace MyBot.UI.BotRuntime.Steps;

/// <summary>
/// Thu hoạch mỏ trên làng chính bằng <see cref="CollectorHarvester"/>.
///
/// parity: <c>Collect</c> @ COCBot/functions/Village/Collect.au3:17 (qua maintenance action
/// <c>Collect</c> trong <c>__RunMaintenanceAction</c> @ MyBot.run.au3:783).
/// </summary>
public sealed class CollectStep
{
    private readonly IBotLogger _logger;

    /// <summary>Tạo bước thu hoạch.</summary>
    /// <param name="logger">Logger của bot.</param>
    public CollectStep(IBotLogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Chụp khung hình mới và tap toàn bộ mỏ tìm được.</summary>
    /// <param name="ctx">Ngữ cảnh vòng bot.</param>
    /// <param name="ct">Token huỷ.</param>
    /// <returns>True khi chạy xong hoặc bỏ qua do thiếu template.</returns>
    public async Task<bool> ExecuteAsync(BotLoopContext ctx, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        if (ctx.Collector is null || ctx.Input is null)
        {
            // Chưa nạp imgxml/template và input: giữ vòng bot chạy tiếp như BOT-2 yêu cầu.
            _logger.Warning("[Collect] Thiếu template mỏ hoặc input — bỏ qua thu hoạch.");
            return true;
        }

        using Mat frame = await ctx.GrabFrameAsync(ct).ConfigureAwait(false);
        if (frame.Empty())
        {
            _logger.Warning("[Collect] Không chụp được khung hình — bỏ qua thu hoạch.");
            return true;
        }

        CollectorResult result = await ctx.Collector
            .HarvestAllAsync(frame, ctx.Matcher, ctx.Input, ct)
            .ConfigureAwait(false);

        _logger.Info(
            $"[Collect] Tap {result.Tapped} mỏ, bỏ qua {result.SkippedFull} kho đầy, " +
            $"{result.SkippedDuplicate} trùng, {result.Duration.TotalMilliseconds:F0} ms.");
        return true;
    }
}
