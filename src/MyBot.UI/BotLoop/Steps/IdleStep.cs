using System;
using System.Threading;
using System.Threading.Tasks;
using MyBot.Core.Abstractions;

namespace MyBot.UI.BotRuntime.Steps;

/// <summary>
/// Chuỗi hành động lúc rảnh (chưa đầy quân) — chỉ port phần xáo trộn danh sách và
/// gọi action; phần chờ đầy quân thuộc M2.
///
/// parity: <c>$g_asIdleActions</c> @ MyBot.run.au3:100 và <c>_Idle</c> @ MyBot.run.au3:1226-1233.
/// </summary>
public sealed class IdleStep
{
    // parity: $g_asIdleActions[5] @ MyBot.run.au3:100.
    private static readonly string[] IdleActions =
    {
        "Collect",
        "CheckTombs",
        "RequestCC",
        "DonateCC",
        "CleanYard"
    };

    private readonly IBotLogger _logger;
    private readonly Random _random;

    /// <summary>Tạo bước idle.</summary>
    /// <param name="logger">Logger của bot.</param>
    /// <param name="seed">Seed xáo trộn; null = ngẫu nhiên.</param>
    public IdleStep(IBotLogger logger, int? seed = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>Xáo trộn danh sách idle rồi chạy lần lượt; Collect là bước thật, còn lại là stub.</summary>
    /// <param name="ctx">Ngữ cảnh vòng bot.</param>
    /// <param name="ct">Token huỷ.</param>
    /// <returns>False khi bot bị dừng giữa chuỗi.</returns>
    public async Task<bool> ExecuteAsync(BotLoopContext ctx, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        string[] order = Shuffle(IdleActions);
        _logger.Debug("[Idle] Thứ tự: " + string.Join(", ", order));

        foreach (string action in order)
        {
            ct.ThrowIfCancellationRequested();

            if (!ctx.CanContinue)
            {
                return false;
            }

            if (string.Equals(action, "Collect", StringComparison.Ordinal))
            {
                if (!await new CollectStep(_logger).ExecuteAsync(ctx, ct).ConfigureAwait(false))
                {
                    return false;
                }
            }
            else
            {
                await new StubStep(action, _logger).ExecuteAsync(ct).ConfigureAwait(false);
            }
        }

        return true;
    }

    private string[] Shuffle(string[] source)
    {
        var order = new string[source.Length];
        source.CopyTo(order, 0);

        for (int i = order.Length - 1; i > 0; i--)
        {
            int j = _random.Next(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        return order;
    }
}
