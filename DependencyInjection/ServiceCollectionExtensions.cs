using Discord.WebSocket;
using HelperLib.Discord.Util;
using HelperLib.Discord.Watchdog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HelperLib.Discord.DependencyInjection;

/// <summary>Registers the library's services in a host's service collection.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="GuildLookup"/>, <see cref="HelperLibDiscordOptions"/> and the reconnect watchdog,
    /// a hosted service that ends the process when the client does not recover from a disconnect or stops
    /// acknowledging heartbeats.
    /// The host registers the <c>DiscordSocketClient</c> itself, for example through Discord.Addons.Hosting.
    /// </summary>
    /// <param name="services">The service collection to add the services to.</param>
    /// <param name="configure">Changes the default <see cref="HelperLibDiscordOptions"/>.</param>
    /// <returns>The same <paramref name="services"/>, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public static IServiceCollection AddHelperLibDiscord(
        this IServiceCollection services,
        Action<HelperLibDiscordOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = services.AddOptions<HelperLibDiscordOptions>().ValidateOnStart();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<HelperLibDiscordOptions>, HelperLibDiscordOptionsValidator>());
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<GuildLookup>();
        services.TryAddSingleton(ProcessExit.Default);
        services.TryAddSingleton<ProcessTerminator>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, ReconnectWatchdog>(CreateReconnectWatchdog));

        return services;
    }

    private static ReconnectWatchdog CreateReconnectWatchdog(IServiceProvider provider)
    {
        var client = provider.GetRequiredService<DiscordSocketClient>();
        return new ReconnectWatchdog(
            client,
            ClientEvents.For(client),
            provider.GetRequiredService<IOptions<HelperLibDiscordOptions>>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<ProcessTerminator>(),
            provider.GetRequiredService<ILogger<ReconnectWatchdog>>());
    }
}
