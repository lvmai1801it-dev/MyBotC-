using System;
using System.Threading;
using FluentAssertions;
using MyBot.Core.Utils;

namespace MyBot.Core.Tests;

public class ProfileLockTests
{
    // CORE-LOOP-FINAL: hai luồng (tượng trưng cho hai tiến trình) không thể cùng giữ khóa.
    [Fact]
    public void TryAcquire_Should_Fail_When_Another_Thread_Holds_Lock()
    {
        string profileName = NewProfileName();

        ProfileLock.TryAcquire(profileName, out ProfileLock? first).Should().BeTrue();
        try
        {
            bool secondAcquired = true;

            using var completed = new ManualResetEventSlim(false);
            var thread = new Thread(() =>
            {
                secondAcquired = ProfileLock.TryAcquire(profileName, out ProfileLock? second);
                second?.Dispose();
                completed.Set();
            });
            thread.Start();
            completed.Wait(TimeSpan.FromSeconds(5)).Should().BeTrue();
            thread.Join();

            secondAcquired.Should().BeFalse();
        }
        finally
        {
            first!.Dispose();
        }
    }

    // CORE-LOOP-FINAL: Dispose nhả khóa để lần chạy sau lấy lại được.
    [Fact]
    public void TryAcquire_Should_Succeed_Again_After_Dispose()
    {
        string profileName = NewProfileName();

        ProfileLock.TryAcquire(profileName, out ProfileLock? first).Should().BeTrue();
        first!.Dispose();

        ProfileLock.TryAcquire(profileName, out ProfileLock? second).Should().BeTrue();
        second!.Dispose();
    }

    // CORE-LOOP-FINAL: Dispose gọi nhiều lần không ném lỗi.
    [Fact]
    public void Dispose_Should_Be_Idempotent()
    {
        string profileName = NewProfileName();

        ProfileLock.TryAcquire(profileName, out ProfileLock? profileLock).Should().BeTrue();

        Action act = () =>
        {
            profileLock!.Dispose();
            profileLock.Dispose();
        };

        act.Should().NotThrow();
    }

    // Tên profile rỗng được quy về "Default" nhưng test dùng tên ngẫu nhiên để không đụng nhau.
    private static string NewProfileName() => $"test-{Guid.NewGuid():N}";
}
