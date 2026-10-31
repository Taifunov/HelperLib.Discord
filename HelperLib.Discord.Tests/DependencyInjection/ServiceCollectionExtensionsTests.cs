using Discord.WebSocket;
using HelperLib.Discord.DependencyInjection;
using HelperLib.Discord.Watchdog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace HelperLib.Discord.Tests.DependencyInjection;

public class ServiceCollectionExtensionsTests
{
    public static TheoryData<string, Action<HelperLibDiscordOptions>> InvalidTimeouts => new()
    {
        { nameof(HelperLibDiscordOptions.ReconnectTimeout), options => options.ReconnectTimeout = TimeSpan.Zero },
        { nameof(HelperLibDiscordOptions.ReconnectTimeout), options => options.ReconnectTimeout = TimeSpan.FromSeconds(-1) },
        { nameof(HelperLibDiscordOptions.ShutdownGracePeriod), options => options.ShutdownGracePeriod = TimeSpan.Zero },
        { nameof(HelperLibDiscordOptions.ShutdownGracePeriod), options => options.ShutdownGracePeriod = TimeSpan.FromSeconds(-1) },
        { nameof(HelperLibDiscordOptions.HeartbeatTimeout), options => options.HeartbeatTimeout = TimeSpan.Zero },
        { nameof(HelperLibDiscordOptions.HeartbeatTimeout), options => options.HeartbeatTimeout = TimeSpan.FromSeconds(-1) },
        { nameof(HelperLibDiscordOptions.ReconnectTimeout), options => options.ReconnectTimeout = TimeSpan.FromDays(50) },
        { nameof(HelperLibDiscordOptions.ShutdownGracePeriod), options => options.ShutdownGracePeriod = TimeSpan.FromDays(50) },
        { nameof(HelperLibDiscordOptions.HeartbeatTimeout), options => options.HeartbeatTimeout = TimeSpan.FromDays(50) },
    };

    [Fact]
    public void AddHelperLibDiscord_throws_when_services_is_null()
    {
        IServiceCollection services = null!;

        Assert.Throws<ArgumentNullException>(() => services.AddHelperLibDiscord());
    }

    [Fact]
    public void AddHelperLibDiscord_registers_GuildLookup_as_a_singleton_built_from_the_registered_client()
    {
        using var client = new DiscordSocketClient(new DiscordSocketConfig());
        var services = new ServiceCollection()
            .AddSingleton(client)
            .AddSingleton<ILogger<GuildLookup>>(NullLogger<GuildLookup>.Instance);

        services.AddHelperLibDiscord();

        using var provider = services.BuildServiceProvider();
        Assert.Same(provider.GetRequiredService<GuildLookup>(), provider.GetRequiredService<GuildLookup>());
    }

    [Fact]
    public void AddHelperLibDiscord_registers_one_reconnect_watchdog_hosted_service_even_when_called_twice()
    {
        using var client = new DiscordSocketClient(new DiscordSocketConfig());
        var services = new ServiceCollection()
            .AddSingleton(client)
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddSingleton(Mock.Of<IHostApplicationLifetime>());

        services.AddHelperLibDiscord();
        services.AddHelperLibDiscord();

        using var provider = services.BuildServiceProvider();
        var hostedService = Assert.Single(provider.GetServices<IHostedService>());
        Assert.IsType<ReconnectWatchdog>(hostedService);
    }

    [Fact]
    public void AddHelperLibDiscord_applies_the_configure_callback()
    {
        using var provider = BuildProvider(options =>
        {
            options.DefaultGuildId = 123;
            options.ReconnectTimeout = TimeSpan.FromSeconds(10);
        });

        var options = GetOptions(provider);
        Assert.Equal(123UL, options.DefaultGuildId);
        Assert.Equal(TimeSpan.FromSeconds(10), options.ReconnectTimeout);
    }

    [Fact]
    public void AddHelperLibDiscord_keeps_the_default_options_without_a_configure_callback()
    {
        using var provider = BuildProvider();

        var options = GetOptions(provider);
        Assert.Null(options.DefaultGuildId);
        Assert.Equal(TimeSpan.FromMinutes(1), options.ReconnectTimeout);
        Assert.Equal(TimeSpan.FromSeconds(30), options.ShutdownGracePeriod);
        Assert.Equal(TimeSpan.FromMinutes(3), options.HeartbeatTimeout);
    }

    [Theory]
    [MemberData(nameof(InvalidTimeouts))]
    public void AddHelperLibDiscord_rejects_a_timeout_that_is_not_positive_or_too_long(
        string optionName,
        Action<HelperLibDiscordOptions> configure)
    {
        using var provider = BuildProvider(configure);

        var exception = Assert.Throws<OptionsValidationException>(() => GetOptions(provider));

        Assert.Contains($"{optionName} must be positive and at most 49.7 days.", exception.Failures);
    }

    [Fact]
    public void AddHelperLibDiscord_accepts_the_longest_timeout_that_Task_Delay_supports()
    {
        var longestTimeout = TimeSpan.FromMilliseconds(uint.MaxValue - 1);
        using var provider = BuildProvider(options =>
        {
            options.ReconnectTimeout = longestTimeout;
            options.ShutdownGracePeriod = longestTimeout;
            options.HeartbeatTimeout = longestTimeout;
        });

        var options = GetOptions(provider);

        Assert.Equal(longestTimeout, options.ReconnectTimeout);
    }

    [Fact]
    public void AddHelperLibDiscord_called_twice_reports_each_invalid_timeout_once()
    {
        var services = new ServiceCollection();
        services.AddHelperLibDiscord(options => options.ReconnectTimeout = TimeSpan.Zero);
        services.AddHelperLibDiscord();

        using var provider = services.BuildServiceProvider();
        var exception = Assert.Throws<OptionsValidationException>(() => GetOptions(provider));

        Assert.Equal(["ReconnectTimeout must be positive and at most 49.7 days."], exception.Failures);
    }

    [Fact]
    public void AddHelperLibDiscord_validates_the_options_when_the_host_starts()
    {
        using var provider = BuildProvider(options => options.ShutdownGracePeriod = TimeSpan.Zero);
        var startupValidator = provider.GetRequiredService<IStartupValidator>();

        var exception = Assert.Throws<OptionsValidationException>(startupValidator.Validate);
        Assert.Contains("ShutdownGracePeriod must be positive and at most 49.7 days.", exception.Failures);
    }

    private static ServiceProvider BuildProvider(Action<HelperLibDiscordOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddHelperLibDiscord(configure);
        return services.BuildServiceProvider();
    }

    private static HelperLibDiscordOptions GetOptions(IServiceProvider provider) =>
        provider.GetRequiredService<IOptions<HelperLibDiscordOptions>>().Value;
}
