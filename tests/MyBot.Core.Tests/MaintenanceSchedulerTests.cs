using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;
using MyBot.Core.Abstractions;
using MyBot.Core.FSM.Maintenance;
using MyBot.Core.Tests.Mocks;

namespace MyBot.Core.Tests;

public class MaintenanceSchedulerTests
{
    private static MaintenanceScheduler Create(FakeTimeProvider time, IBotLogger logger, int seed = 42)
        => new(time, logger, seed);

    [Fact]
    public void ActionChuaChay_IsDueTrue()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var logger = new Mock<IBotLogger>();
        var scheduler = Create(time, logger.Object);

        // Act & Assert
        scheduler.IsDue(MaintenanceAction.Collect, MaintenanceData.FastMin, MaintenanceData.FastMax).Should().BeTrue();
    }

    [Fact]
    public async Task MarkRun_RoiHetCooldown_IsDueLai()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var logger = new Mock<IBotLogger>();
        var scheduler = Create(time, logger.Object);

        // Act
        scheduler.MarkRun(MaintenanceAction.Laboratory, 5, 5);

        // Assert — vừa chạy xong: chưa tới hạn, và action bị bỏ qua (trả True, không execute).
        scheduler.IsDue(MaintenanceAction.Laboratory, 5, 5).Should().BeFalse();
        bool executed = false;
        scheduler.RunAction(MaintenanceAction.Laboratory, 5, 5, _ => { executed = true; return true; })
            .Should().BeTrue();
        executed.Should().BeFalse();

        await time.DelayAsync(5 * 60_000 - 1);
        scheduler.IsDue(MaintenanceAction.Laboratory, 5, 5).Should().BeFalse();

        await time.DelayAsync(1);
        scheduler.IsDue(MaintenanceAction.Laboratory, 5, 5).Should().BeTrue();
    }

    [Fact]
    public async Task Cooldown_NamTrongDai_8DaiDeuDung()
    {
        // Arrange
        var bands = new (int Min, int Max)[]
        {
            (MaintenanceData.FastMin, MaintenanceData.FastMax),
            (MaintenanceData.StatusMin, MaintenanceData.StatusMax),
            (MaintenanceData.UpgradeMin, MaintenanceData.UpgradeMax),
            (MaintenanceData.CleanupMin, MaintenanceData.CleanupMax),
            (MaintenanceData.BuilderBaseMin, MaintenanceData.BuilderBaseMax),
            (MaintenanceData.BuilderBaseAttackMin, MaintenanceData.BuilderBaseAttackMax),
            (MaintenanceData.RetryMin, MaintenanceData.RetryMax),
            (MaintenanceData.ClanGamesMin, MaintenanceData.ClanGamesMax)
        };
        var logger = new Mock<IBotLogger>();

        foreach ((int min, int max) in bands)
        {
            var time = new FakeTimeProvider();
            var scheduler = Create(time, logger.Object, seed: 123);

            // Act
            scheduler.MarkRun(MaintenanceAction.Collect, min, max);

            // Assert — chưa đủ min phút thì chưa tới hạn, đủ max phút thì chắc chắn tới hạn.
            scheduler.IsDue(MaintenanceAction.Collect, min, max).Should().BeFalse();
            await time.DelayAsync(min * 60_000 - 1);
            scheduler.IsDue(MaintenanceAction.Collect, min, max).Should().BeFalse();
            await time.DelayAsync(max * 60_000 - (min * 60_000 - 1));
            scheduler.IsDue(MaintenanceAction.Collect, min, max).Should().BeTrue();
        }
    }

    [Fact]
    public void DoiDaiMinMax_IsDueTrue()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var logger = new Mock<IBotLogger>();
        var scheduler = Create(time, logger.Object);
        scheduler.MarkRun(MaintenanceAction.Collect, 2, 4);
        scheduler.IsDue(MaintenanceAction.Collect, 2, 4).Should().BeFalse();

        // Act & Assert — parity: đổi dải so với lần reset trước → tới hạn.
        scheduler.IsDue(MaintenanceAction.Collect, 5, 10).Should().BeTrue();
    }

    [Fact]
    public async Task AnyDue_Va_ResetGroup_TrenCaNhom()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var logger = new Mock<IBotLogger>();
        var scheduler = Create(time, logger.Object);
        var group = MaintenanceData.Status;

        // Act & Assert
        scheduler.AnyDue(group, 5, 5).Should().BeTrue();

        scheduler.ResetGroup(group, 5, 5);
        scheduler.AnyDue(group, 5, 5).Should().BeFalse();

        await time.DelayAsync(5 * 60_000);
        scheduler.AnyDue(group, 5, 5).Should().BeTrue();
    }

    [Fact]
    public void ShuffleOrder_GiuDuPhanTu_SeedGiongThiThuTuGiong_CoLog()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var loggerA = new Mock<IBotLogger>();
        var loggerB = new Mock<IBotLogger>();
        var schedulerA = Create(time, loggerA.Object, seed: 7);
        var schedulerB = Create(time, loggerB.Object, seed: 7);
        var group = MaintenanceData.Cleanup;
        var original = group.ToArray();

        // Act
        IReadOnlyList<MaintenanceAction> orderA = schedulerA.ShuffleOrder(group);
        IReadOnlyList<MaintenanceAction> orderB = schedulerB.ShuffleOrder(group);

        // Assert
        orderA.Should().HaveCount(group.Length);
        orderA.Should().BeEquivalentTo(group);
        orderA.Should().Equal(orderB);
        group.Should().Equal(original); // không sửa mảng đầu vào

        loggerA.Verify(
            l => l.Debug(It.Is<string>(s => s.Contains("[Maintenance] Thứ tự:", StringComparison.Ordinal))),
            Times.Once);
    }

    [Fact]
    public void RunGroup_DungODauTienTraVeFalse()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var logger = new Mock<IBotLogger>();
        var scheduler = Create(time, logger.Object);
        int calls = 0;

        bool Execute(MaintenanceAction action)
        {
            calls++;
            return calls < 2;
        }

        // Act
        bool result = scheduler.RunGroup(MaintenanceData.Status, 5, 5, Execute);

        // Assert
        result.Should().BeFalse();
        calls.Should().Be(2);
    }

    [Fact]
    public void Force_ChayDuChuaDue_IdKhongHopLe_TraFalseVaLogError()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var logger = new Mock<IBotLogger>();
        var scheduler = Create(time, logger.Object);
        scheduler.MarkRun(MaintenanceAction.Collect, 5, 5);
        int executed = 0;

        // Act
        bool forced = scheduler.RunAction(
            MaintenanceAction.Collect, 5, 5, _ => { executed++; return true; }, force: true);
        bool invalid = scheduler.RunAction(
            (MaintenanceAction)99, 5, 5, _ => { executed++; return true; });

        // Assert
        forced.Should().BeTrue();
        executed.Should().Be(1);
        invalid.Should().BeFalse();
        logger.Verify(
            l => l.Error(It.Is<string>(s => s.Contains("không hợp lệ", StringComparison.Ordinal)), null),
            Times.Once);
    }

    [Fact]
    public void CanContinueFalse_KhongExecute_TraFalse()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var logger = new Mock<IBotLogger>();
        var scheduler = Create(time, logger.Object);
        bool executed = false;

        // Act
        bool result = scheduler.RunAction(
            MaintenanceAction.Collect, 5, 5,
            _ => { executed = true; return true; },
            canContinue: () => false);

        // Assert
        result.Should().BeFalse();
        executed.Should().BeFalse();
    }

    [Fact]
    public async Task CheckRebootTrue_TraFalse_KhongResetTimer()
    {
        // Arrange
        var time = new FakeTimeProvider();
        var logger = new Mock<IBotLogger>();
        var scheduler = Create(time, logger.Object);
        scheduler.MarkRun(MaintenanceAction.Collect, 5, 5);

        // Act — force để bỏ qua kiểm tra tới hạn, rebootCheck trả True.
        bool result = scheduler.RunAction(
            MaintenanceAction.Collect, 5, 5, _ => true,
            checkReboot: true, rebootCheck: () => true, force: true);

        // Assert — parity: return False trước khi __MaintenanceDue(reset), timer giữ nguyên.
        result.Should().BeFalse();
        scheduler.IsDue(MaintenanceAction.Collect, 5, 5).Should().BeFalse();

        await time.DelayAsync(5 * 60_000);
        scheduler.IsDue(MaintenanceAction.Collect, 5, 5).Should().BeTrue();
    }
}
