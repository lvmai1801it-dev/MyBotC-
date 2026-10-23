using System.Threading;
using System.Threading.Tasks;
using MyBot.UI.BotRuntime.Steps;
using Xunit;

namespace MyBot.UI.Tests.BotRuntime.Steps;

public sealed class StubStepTests
{
    [Fact]
    public async Task ExecuteAsync_LogsExactFormat_AndReturnsTrue()
    {
        var logger = new FakeLogger();

        bool ok = await new StubStep("AttackCycle", logger).ExecuteAsync();

        Assert.True(ok);
        Assert.Equal("[STUB] AttackCycle — M3/M4 chưa port", Assert.Single(logger.Infos));
    }

    [Fact]
    public async Task ExecuteAsync_CancelledToken_Throws()
    {
        var logger = new FakeLogger();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<System.OperationCanceledException>(
            () => new StubStep("AttackCycle", logger).ExecuteAsync(cts.Token));
        Assert.Empty(logger.Infos);
    }
}
