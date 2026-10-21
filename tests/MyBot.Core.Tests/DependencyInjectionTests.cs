using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MyBot.App;
using MyBot.Core.Abstractions;
using MyBot.UI.Tabs.Log;

namespace MyBot.Core.Tests;

public class DependencyInjectionTests
{
    private readonly IServiceProvider _provider;

    public DependencyInjectionTests()
    {
        _provider = Startup.ConfigureServices();
    }

    // T0.3.1: Resolve Logger thành công
    [Fact]
    public void Should_Resolve_Logger()
    {
        var logger = _provider.GetService<ILogger<DependencyInjectionTests>>();
        logger.Should().NotBeNull();
    }

    // T0.3.2: ITimeProvider là Singleton
    [Fact]
    public void TimeProvider_Should_Be_Singleton()
    {
        var instance1 = _provider.GetService<ITimeProvider>();
        var instance2 = _provider.GetService<ITimeProvider>();

        // Tham chiếu bộ nhớ phải trỏ về cùng 1 object
        instance1.Should().BeSameAs(instance2);
    }

    // T0.3.3: ITab là Transient
    [Fact]
    public void ITab_Should_Be_Transient()
    {
        var instance1 = _provider.GetService<ITab>();
        var instance2 = _provider.GetService<ITab>();

        // Phải là 2 object khác nhau trong bộ nhớ
        instance1.Should().NotBeSameAs(instance2);
        instance1.Should().BeOfType<LogTab>();
    }
}
