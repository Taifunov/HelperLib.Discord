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
/// <see cref="RunAsync"/>, <see cref="AttachToExistingClient"/> and <see cref="Dispose"/> may be called from any
/// thread; an instance serializes them. <see cref="Client"/> and <see cref="GuildLookup"/> hold process-wide static
/// state, so create one helper per process: several helpers share one static client and the last attach wins.
/// </summary>
public sealed class IonicHelper : IDisposable
{
    private static readonly Lock ClientGate = new();
    private static DiscordSocketClient? _client;
    private static CancellationTokenSource? _tokenSource;

    // Guards _disposed and the attached subscription, so an attach cannot interleave with Dispose.
    private readonly Lock _gate = new();
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
    public static DiscordSocketClient? Client
    {
        get => Volatile.Read(ref _client);
        internal set => Volatile.Write(ref _client, value);
    }

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
    /// A buffering logger provider may not flush the Critical entry written just before the process exits.
    /// </param>
    public IonicHelper(string token, ulong? defaultGuildId = null, ILogger? logger = null)
        : this(
            token,
            TimeSpan.FromMinutes(1),
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
    /// A buffering logger provider may not flush the Critical entry written just before the process exits.
    /// </param>
    public IonicHelper(ulong? defaultGuildId = null, ILogger? logger = null)
        : this(string.Empty, defaultGuildId, logger)
    {
    }

    internal IonicHelper(
        string token,
        TimeSpan resetTimeout,
        Func<TimeSpan, CancellationToken, Task> delayAsync,
        Action failFast,
        ulong? defaultGuildId = null,
        ILogger? logger = null)
    {
        _token = token;
        _logger = logger ?? ConsoleLogger.Instance;
        if (logger is not null)
        {
            GuildLookup.SetLogger(logger);
        }

        _watchdog = new ReconnectWatchdog(resetTimeout, delayAsync, failFast, _logger);

        if (defaultGuildId is not null)
        {
            GuildLookup.SetDefaultGuildId(defaultGuildId);
            _logger.DefaultGuildIdSet(defaultGuildId);
        }
    }

    /// <summary>
    /// Creates a <see cref="DiscordSocketClient"/>, attaches the reconnect watchdog to it,
    /// logs in as a bot and starts the gateway connection. Completes once the start is
    /// scheduled, not once the client is connected. Calling it again replaces the static
    /// <see cref="Client"/> and does not stop the previous client.
    /// </summary>
    /// <param name="config">The client configuration.</param>
    /// <param name="logger">Receives the client's log messages.</param>
    /// <exception cref="ArgumentNullException"><paramref name="config"/> or <paramref name="logger"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The helper was created without a token.</exception>
    /// <exception cref="ObjectDisposedException">The helper is disposed.</exception>
    public async Task RunAsync(DiscordSocketConfig config, Func<LogMessage, Task> logger)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(logger);

        if (_token.Length == 0)
        {
            throw new ArgumentException("Incorrect token! Pass valid one into IonicHelper.ctor");
        }

        DiscordSocketClient client;
        ClientEvents events;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            client = new DiscordSocketClient(config);
            client.Log += logger;
            events = ClientEvents.For(client);
            AttachClient(client, events);
        }

        _logger.StartingIonicHelper();
        try
        {
            await client.LoginAsync(TokenType.Bot, _token).ConfigureAwait(false);
            await client.StartAsync().ConfigureAwait(false);
        }
        catch
        {
            lock (_gate)
            {
                DetachClientIfAttached(events);
            }

            await StopAndDisposeAsync(client).ConfigureAwait(false);
            throw;
        }

        // Dispose ran while logging in: it detached the watchdog, and nobody else owns this client.
        bool disposedMeanwhile;
        lock (_gate)
        {
            disposedMeanwhile = _disposed;
        }

        if (disposedMeanwhile)
        {
            await StopAndDisposeAsync(client).ConfigureAwait(false);
        }
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
        ArgumentNullException.ThrowIfNull(client);

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (defaultGuildId != null)
            {
                GuildLookup.SetDefaultGuildId(defaultGuildId);
            }

            AttachClient(client, ClientEvents.For(client));
        }
    }

    internal void AttachClient(DiscordSocketClient client, ClientEvents events)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            DetachClient();

            _watchdog.Arm();
            ReplaceClient(client);
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
    }

    private static void ReplaceClient(DiscordSocketClient client)
    {
        lock (ClientGate)
        {
            Volatile.Write(ref _client, client);
            Volatile.Write(ref _tokenSource, new CancellationTokenSource());
        }
    }

    /// <summary>
    /// Unsubscribes the reconnect watchdog from the attached client, cancels any pending
    /// reconnect wait and releases the watchdog's resources. It stops and disposes a client only
    /// when it runs while <see cref="RunAsync"/> is still logging in or starting that client.
    /// A client <see cref="RunAsync"/> has already started, and any client passed to
    /// <see cref="AttachToExistingClient"/>, stay owned by the caller: stop them yourself
    /// (for the started one, through <see cref="Client"/>). Calling it again has no effect.
    /// </summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            DetachClient();
            _watchdog.Dispose();
        }
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

    private void DetachClientIfAttached(ClientEvents events)
    {
        if (ReferenceEquals(_attachedEvents, events))
        {
            DetachClient();
        }
    }

    private async Task StopAndDisposeAsync(DiscordSocketClient client)
    {
        Interlocked.CompareExchange(ref _client, null, client);
        try
        {
            await client.StopAsync().ConfigureAwait(false);
            client.Dispose();
        }
        catch (Exception exception)
        {
            _logger.ClientCleanupFailed(exception);
        }
    }

    private async Task RunDisconnectedWatchdogAsync(DiscordSocketClient client)
    {
        try
        {
            await _watchdog.ClientOnDisconnected(client).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.ReconnectWatchdogFailed(exception);
        }
    }

    /// <summary>
    /// Gets the token source created by the latest attach, or <see langword="null"/> before
    /// <see cref="RunAsync"/> or <see cref="AttachToExistingClient"/> has run.
    /// <see cref="Dispose"/> and a later attach do not cancel it.
    /// </summary>
    public static CancellationTokenSource? GetCancellationTokenSource() => Volatile.Read(ref _tokenSource);
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
