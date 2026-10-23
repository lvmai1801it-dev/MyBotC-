using System;

namespace MyBot.Game.CoC.State.Zoom;

/// <summary>
/// Chọn và chạy script pinch của AutoIt (<c>AndroidZoomOut</c>):
/// <c>Normal0..6</c> cho làng thường, <c>Small0..2</c> cho làng địch Builder Base.
/// </summary>
public sealed class AndroidZoomOut
{
    private readonly MinitouchRunner _runner;
    private readonly string _emulatorName;
    private readonly Random _random;
    private readonly int _timeoutMs;

    /// <summary>Khởi tạo bộ gửi zoom ADB.</summary>
    /// <param name="runner">Runner minitouch.</param>
    /// <param name="emulatorName">Tên giả lập dùng để chọn file script (<c>MEmu</c>, <c>BlueStacks5</c>).</param>
    /// <param name="timeoutMs">Thời gian chờ mỗi script.</param>
    /// <param name="random">Nguồn ngẫu nhiên (có thể seed trong test).</param>
    public AndroidZoomOut(
        MinitouchRunner runner,
        string emulatorName,
        int timeoutMs = 20_000,
        Random? random = null)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _emulatorName = string.IsNullOrWhiteSpace(emulatorName) ? "MEmu" : emulatorName;
        _timeoutMs = timeoutMs;
        _random = random ?? Random.Shared;
    }

    /// <summary>True nếu minitouch đã sẵn sàng.</summary>
    public bool IsAvailable => _runner.IsReady;

    /// <summary>Gửi một nấc zoom out bằng script minitouch.</summary>
    /// <param name="onBuilderBaseEnemy">True khi ở làng địch Builder Base.</param>
    /// <returns>True nếu script chạy thành công.</returns>
    public bool ZoomStep(bool onBuilderBaseEnemy)
    {
        string tag = SelectScript(onBuilderBaseEnemy, _random);
        return _runner.RunScript(tag, _emulatorName, _timeoutMs);
    }

    /// <summary>Chọn tên script theo đúng phân nhánh của <c>AndroidZoomOut</c>.</summary>
    /// <param name="onBuilderBaseEnemy">True khi ở làng địch Builder Base.</param>
    /// <param name="random">Nguồn ngẫu nhiên.</param>
    /// <returns><c>Small0..2</c> hoặc <c>Normal0..6</c>.</returns>
    public static string SelectScript(bool onBuilderBaseEnemy, Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        return onBuilderBaseEnemy
            ? $"Small{random.Next(0, 3)}"
            : $"Normal{random.Next(0, 7)}";
    }
}
