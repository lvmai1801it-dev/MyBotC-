using System;
using System.Threading;
using System.Threading.Tasks;
using MyBot.Core.Abstractions;

namespace MyBot.UI.BotRuntime.Steps;

/// <summary>
/// Bước chưa port: ghi log và coi như thành công để vòng <c>runBot</c> chạy tiếp.
/// Dùng cho các module M3/M4 (attack, train, donate, upgrade, search).
/// </summary>
public sealed class StubStep
{
    private readonly string _name;
    private readonly IBotLogger _logger;

    /// <summary>Tạo bước stub.</summary>
    /// <param name="name">Tên hàm AutoIt tương ứng.</param>
    /// <param name="logger">Logger của bot.</param>
    public StubStep(string name, IBotLogger logger)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Ghi log <c>[STUB]</c> rồi trả true.</summary>
    /// <param name="ct">Token huỷ.</param>
    /// <returns>Luôn true.</returns>
    public Task<bool> ExecuteAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _logger.Info($"[STUB] {_name} — M3/M4 chưa port");
        return Task.FromResult(true);
    }
}
