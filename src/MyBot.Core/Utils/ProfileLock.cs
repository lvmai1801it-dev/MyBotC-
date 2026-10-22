using System;
using System.IO;
using System.Threading;

namespace MyBot.Core.Utils;

/// <summary>
/// Khóa một profile theo tên bằng named Mutex toàn cục, ngăn hai tiến trình MyBot
/// cùng điều khiển một tài khoản game.
/// </summary>
public class ProfileLock : IDisposable
{
    private const string MutexPrefix = @"Global\MyBot.Profile.";

    private readonly Mutex _mutex;
    private readonly int _ownerThreadId;
    private bool _disposed;

    private ProfileLock(Mutex mutex)
    {
        _mutex = mutex;
        _ownerThreadId = Environment.CurrentManagedThreadId;
    }

    /// <summary>
    /// Thử lấy khóa cho profile. Trả false ngay lập tức nếu tiến trình khác đang giữ khóa.
    /// </summary>
    /// <param name="profileName">Tên profile (ví dụ "Default"); rỗng sẽ dùng "Default".</param>
    /// <param name="lockObj">Đối tượng khóa cần Dispose để nhả khóa; null nếu thất bại.</param>
    /// <returns>True nếu lấy được khóa.</returns>
    public static bool TryAcquire(string profileName, out ProfileLock? lockObj)
    {
        lockObj = null;

        Mutex mutex;
        try
        {
            mutex = new Mutex(false, MutexPrefix + SanitizeName(profileName));
        }
        catch (UnauthorizedAccessException)
        {
            // Mutex cùng tên đã tồn tại nhưng ACL không cho phép mở.
            return false;
        }
        catch (IOException)
        {
            return false;
        }

        bool acquired;
        try
        {
            acquired = mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            // Tiến trình giữ khóa trước đã chết; ta trở thành chủ sở hữu mới.
            acquired = true;
        }

        if (!acquired)
        {
            mutex.Dispose();
            return false;
        }

        lockObj = new ProfileLock(mutex);
        return true;
    }

    /// <summary>
    /// Nhả khóa và giải phóng Mutex. Nên gọi trên đúng luồng đã gọi <see cref="TryAcquire"/>.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_ownerThreadId == Environment.CurrentManagedThreadId)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Mutex không còn thuộc sở hữu của luồng này (đã bị giải phóng trước đó).
            }
        }

        // Khi gọi từ luồng khác, không thể ReleaseMutex (Mutex gắn với luồng sở hữu).
        // Đóng handle là đủ để kernel dọn đối tượng khi không còn ai mở.
        _mutex.Dispose();
    }

    private static string SanitizeName(string profileName)
    {
        if (string.IsNullOrWhiteSpace(profileName))
        {
            return "Default";
        }

        // Dấu '\' và '/' mang ý nghĩa đặc biệt trong tên kernel object.
        return profileName.Replace('\\', '_').Replace('/', '_');
    }
}
