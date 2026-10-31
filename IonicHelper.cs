using Discord;
using Discord.WebSocket;
using HelperLib.Discord.Util;
using HelperLib.Discord.Watchdog;
using Microsoft.Extensions.Logging;

namespace HelperLib.Discord;

/// <summary>
/// Creates (<see cref="RunAsync"/>) or attaches to (<see cref="AttachToExistingClient"/>) the
/// process-wide <see cref="DiscordSocketClient"/> and watches it with a reconnect watchdog that
/// backstops Discord.Net's own reconnect logic. There is one client per process.
/// </summary>
public sealed class IonicHelper : IDisposable
{
    private static CancellationTokenSource? _tokenSource;
    private readonly string _token;
    private readonly ReconnectWatchdog _watchdog;
    private readonly ILogger _logger;
    private bool _disposed;
    private ClientEvents? _attachedEvents;
    private Func<Exception, Task>? _disconnectedHandler;

    /// <summary>
    /// The client most recently started by <see cref="RunAsync"/> or attached by
    /// <see cref="AttachToExistingClient"/>; <see langword="null"/> before either has run.
    /// <see cref="GuildLookup"/> reads it.
    /// </summary>
    public static DiscordSocketClient? Client { get; private set; }

    /// <summary>
    /// Creates a helper that logs in with a bot token. When the client stays disconnected for one
    /// minute, the reconnect watchdog restarts it if it is <see cref="ConnectionState.Disconnected"/>,
    /// or waits for a Connecting or Disconnecting state to settle. If the client is not
    /// <see cref="ConnectionState.Connected"/> one more minute later, the watchdog ends the process
    /// with <see cref="Environment.Exit(int)"/> (exit code 1), so an external process host can
    /// restart the bot. It does nothing if the client recovered.
    /// </summary>
    /// <param name="token">The bot token used by <see cref="RunAsync"/>.</param>
    /// <param name="defaultGuildId">The guild used by <see cref="GuildLookup"/> when a call passes no guild id.</param>
    /// <param name="logger">
    /// Receives the helper's, the watchdog's and <see cref="GuildLookup"/>'s log messages; they go to the console when <see langword="null"/>.
    /// <see cref="GuildLookup"/> is static, so it uses the logger of the last <see cref="IonicHelper"/> constructed with one.
    /// </param>
    public IonicHelper(string token, ulong? defaultGuildId = null, ILogger? logger = null)
        : this(
            token,
            TimeSpan.FromMinutes(1),
            Task.Delay,
            Task.Delay,
            () => Environment.Exit(1),
            defaultGuildId,
            logger)
    {
    }

    /// <summary>
    /// Creates an IonicHelper for <see cref="AttachToExistingClient"/> only - no token is
    /// needed since this path never logs in a client itself, it only attaches to one the
    /// host application already owns.
    /// </summary>
    /// <param name="defaultGuildId">The guild used by <see cref="GuildLookup"/> when a call passes no guild id.</param>
    /// <param name="logger">
    /// Receives the helper's, the watchdog's and <see cref="GuildLookup"/>'s log messages; they go to the console when <see langword="null"/>.
    /// <see cref="GuildLookup"/> is static, so it uses the logger of the last <see cref="IonicHelper"/> constructed with one.
    /// </param>
    public IonicHelper(ulong? defaultGuildId = null, ILogger? logger = null)
        : this(string.Empty, defaultGuildId, logger)
    {
    }

    internal IonicHelper(
        string token,
        TimeSpan resetTimeout,
        Func<TimeSpan, CancellationToken, Task> delayAsync,
        Func<TimeSpan, Task> delayWithoutCancellationAsync,
        Action failFast,
        ulong? defaultGuildId = null,
        ILogger? logger = null)
    {
        _token = token;
        _logger = logger ?? ConsoleLogger.Instance;
        GuildLookup.SetDefaultGuildId(defaultGuildId);
        if (logger is not null)
        {
            GuildLookup.SetLogger(logger);
        }

        _watchdog = new ReconnectWatchdog(resetTimeout, delayAsync, delayWithoutCancellationAsync, failFast, _logger);

        if (defaultGuildId != default)
        {
            _logger.DefaultGuildIdSet(defaultGuildId);
        }
    }

    /// <summary>
    /// Creates a <see cref="DiscordSocketClient"/>, attaches the reconnect watchdog to it,
    /// logs in as a bot and starts the gateway connection. Completes once the start is
    /// scheduled, not once the client is connected.
    /// </summary>
    /// <param name="config">The client configuration.</param>
    /// <param name="logger">Receives the client's log messages.</param>
    /// <exception cref="ArgumentNullException"><paramref name="config"/> or <paramref name="logger"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The helper was created without a token.</exception>
    /// <exception cref="ObjectDisposedException">The helper is disposed.</exception>
    public async Task RunAsync(DiscordSocketConfig config, Func<LogMessage, Task> logger)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(logger);

        if (_token.Length == 0)
        {
            throw new ArgumentException("Incorrect token! Pass valid one into IonicHelper.ctor");
        }

        var client = new DiscordSocketClient(config);
        client.Log += logger;
        AttachClient(client, ClientEvents.For(client));

        _logger.StartingIonicHelper();
        await client.LoginAsync(TokenType.Bot, _token);
        await client.StartAsync();
    }

    /// <summary>
    /// Attaches this IonicHelper's reconnect watchdog and static <see cref="Client"/>
    /// reference to an already-running <see cref="DiscordSocketClient"/> instead of
    /// creating and logging in a second gateway connection. Use this when the host
    /// application already owns a client's lifecycle (e.g. via a DI-managed hosting
    /// framework) and only needs GuildLookup/IonicHelper.Client's static access plus
    /// the watchdog's fail-fast reconnect monitoring, not a second connection.
    /// </summary>
    /// <param name="client">The running client to attach to.</param>
    /// <param name="defaultGuildId">Replaces the default guild when not <see langword="null"/>; otherwise the current default is kept.</param>
    /// <exception cref="ArgumentNullException"><paramref name="client"/> is <see langword="null"/>.</exception>
    /// <exception cref="ObjectDisposedException">The helper is disposed.</exception>
    public void AttachToExistingClient(DiscordSocketClient client, ulong? defaultGuildId = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(client);

        if (defaultGuildId != null)
        {
            GuildLookup.SetDefaultGuildId(defaultGuildId);
        }

        AttachClient(client, ClientEvents.For(client));
    }

    internal void AttachClient(DiscordSocketClient client, ClientEvents events)
    {
        DetachClient();

        _tokenSource = new CancellationTokenSource();
        _watchdog.Arm();
        Client = client;
        _attachedEvents = events;
        _disconnectedHandler = exception =>
        {
            // Discord.Net awaits event handlers, so the timeout must not run inside this one.
            _ = RunDisconnectedWatchdogAsync(client);
            return Task.CompletedTask;
        };
        events.AddConnected(_watchdog.ClientOnConnected);
        events.AddDisconnected(_disconnectedHandler);
    }

    /// <summary>
    /// Unsubscribes the reconnect watchdog from the attached client, cancels any pending
    /// reconnect wait and releases the watchdog's resources. The client itself, and the static
    /// <see cref="Client"/> reference, are left alone because the host application owns them.
    /// Calling it again has no effect.
    /// </summary>
    public void Dispose()
    {
        _disposed = true;
        DetachClient();
        _watchdog.Dispose();
    }

    private void DetachClient()
    {
        if (_attachedEvents is null || _disconnectedHandler is null)
        {
            return;
        }

        _attachedEvents.RemoveConnected(_watchdog.ClientOnConnected);
        _attachedEvents.RemoveDisconnected(_disconnectedHandler);
        _attachedEvents = null;
        _disconnectedHandler = null;
    }

    private async Task RunDisconnectedWatchdogAsync(DiscordSocketClient client)
    {
        try
        {
            await _watchdog.ClientOnDisconnected(client);
        }
        catch (Exception exception)
        {
            _logger.ReconnectWatchdogFailed(exception);
        }
    }

    /// <summary>
    /// Gets the token source created by the latest attach, or <see langword="null"/> before
    /// <see cref="RunAsync"/> or <see cref="AttachToExistingClient"/> has run.
    /// </summary>
    public static CancellationTokenSource? GetCancellationTokenSource() => _tokenSource;
}

/// <summary>
/// The Connected/Disconnected subscriptions of one client. Discord.Net gives no way to
/// observe or raise its events, so tests replace this seam with a fake.
/// </summary>
internal sealed record ClientEvents(
    Action<Func<Task>> AddConnected,
    Action<Func<Task>> RemoveConnected,
    Action<Func<Exception, Task>> AddDisconnected,
    Action<Func<Exception, Task>> RemoveDisconnected)
{
    internal static ClientEvents For(DiscordSocketClient client) =>
        new(
            handler => client.Connected += handler,
            handler => client.Connected -= handler,
            handler => client.Disconnected += handler,
            handler => client.Disconnected -= handler);
}
