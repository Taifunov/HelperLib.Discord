namespace HelperLib.Discord;

/// <summary>Settings for the services that <c>AddHelperLibDiscord</c> registers.</summary>
public sealed class HelperLibDiscordOptions
{
    /// <summary>
    /// The guild that lookups use when a call passes no guild id.
    /// Without it, such a call finds nothing and logs a warning.
    /// </summary>
    public ulong? DefaultGuildId { get; set; }

    /// <summary>
    /// How long the client may stay disconnected, and how long a restarted client may take to
    /// reconnect, before the reconnect watchdog ends the process. Must be positive and at most 49.7 days.
    /// </summary>
    public TimeSpan ReconnectTimeout { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// How long the process gets to shut down gracefully after the reconnect watchdog gives up
    /// and before it is ended forcibly. Must be positive and at most 49.7 days.
    /// </summary>
    public TimeSpan ShutdownGracePeriod { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long the gateway may stay silent while the client reports itself connected
    /// before the reconnect watchdog treats the connection as lost. Must be positive and at most 49.7 days.
    /// </summary>
    public TimeSpan HeartbeatTimeout { get; set; } = TimeSpan.FromMinutes(3);
}
