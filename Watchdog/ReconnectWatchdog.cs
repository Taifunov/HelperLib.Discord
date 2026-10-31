using Discord;
using HelperLib.Discord.Util;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HelperLib.Discord.Watchdog;

/// <summary>
/// Backstops Discord.Net's own reconnect logic while the host runs. It restarts a client that stays
/// disconnected, and ends the process when the client does not recover or stops acknowledging heartbeats.
/// </summary>
internal sealed class ReconnectWatchdog : IHostedService, IDisposable
{
    private readonly IDiscordClient _client;
    private readonly ClientEvents _clientEvents;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _reconnectTimeout;
    private readonly TimeSpan _heartbeatTimeout;
    private readonly ProcessTerminator _terminator;
    private readonly HeartbeatMonitor _heartbeat;
    private readonly ILogger _logger;
    private readonly Lock _gate = new();
    private CancellationTokenSource _reconnectCts = new();
    private CancellationToken? _runningCheckToken;
    private bool _started;
    private volatile bool _stopped;

    internal ReconnectWatchdog(
        IDiscordClient client,
        ClientEvents clientEvents,
        IOptions<HelperLibDiscordOptions> options,
        TimeProvider timeProvider,
        ProcessTerminator terminator,
        ILogger<ReconnectWatchdog> logger)
    {
        _client = client;
        _clientEvents = clientEvents;
        _timeProvider = timeProvider;
        _reconnectTimeout = options.Value.ReconnectTimeout;
        _terminator = terminator;
        _logger = logger;
        _heartbeatTimeout = options.Value.HeartbeatTimeout;
        _heartbeat = new HeartbeatMonitor(
            timeProvider,
            _heartbeatTimeout,
            () => _client.ConnectionState == ConnectionState.Connected,
            OnHeartbeatTimeout);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_stopped, this);
            if (_started)
            {
                return Task.CompletedTask;
            }

            _started = true;
            _clientEvents.AddConnected(ClientOnConnected);
            _clientEvents.AddDisconnected(OnDisconnected);
            _clientEvents.AddLatencyUpdated(OnLatencyUpdated);
        }

        // The host may start this service after the client connected, so no Connected event will come.
        if (_client.ConnectionState == ConnectionState.Connected)
        {
            _heartbeat.Arm();
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        CancellationTokenSource lastCts;
        lock (_gate)
        {
            if (_stopped)
            {
                return;
            }

            _stopped = true;
            lastCts = _reconnectCts;
        }

        _clientEvents.RemoveConnected(ClientOnConnected);
        _clientEvents.RemoveDisconnected(OnDisconnected);
        _clientEvents.RemoveLatencyUpdated(OnLatencyUpdated);
        _heartbeat.Dispose();
        CancelAndDispose(lastCts);
    }

    internal Task ClientOnConnected()
    {
        CancellationTokenSource previousCts;
        lock (_gate)
        {
            if (_stopped)
            {
                return Task.CompletedTask;
            }

            previousCts = _reconnectCts;
            _reconnectCts = new CancellationTokenSource();
        }

        _logger.ClientReconnected();
        CancelAndDispose(previousCts);
        _heartbeat.Arm();
        _logger.CancelTokensReset();
        return Task.CompletedTask;
    }

    internal async Task ClientOnDisconnected()
    {
        if (_stopped)
        {
            return;
        }

        // The reconnect check below owns the disconnected state.
        _heartbeat.Pause();

        // Discord.Net raises Disconnected on every failed reconnect attempt. A second concurrent check
        // could call StartAsync on a client that is already connecting and wrongly end the process.
        if (!TryBeginCheck(out var token))
        {
            _logger.DuplicateDisconnectIgnored();
            return;
        }

        try
        {
            _logger.ClientDisconnected();

            try
            {
                await Task.Delay(_reconnectTimeout, _timeProvider, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            _logger.TimeoutExpired();
            await ReconnectStateChecker.CheckStateAsync(
                () => _client.ConnectionState,
                _client.StartAsync,
                _timeProvider,
                _reconnectTimeout,
                _logger,
                _terminator.Terminate,
                token).ConfigureAwait(false);
            if (!token.IsCancellationRequested)
            {
                _logger.StateCameBackOkay();
            }
        }
        finally
        {
            EndCheck(token);
        }
    }

    // Discord.Net awaits event handlers, so the reconnect timeout must not run inside this one.
    private Task OnDisconnected(Exception exception)
    {
        _ = RunDisconnectedCheckAsync();
        return Task.CompletedTask;
    }

    private async Task RunDisconnectedCheckAsync()
    {
        try
        {
            await ClientOnDisconnected().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.ReconnectWatchdogFailed(exception);
        }
    }

    private Task OnLatencyUpdated(int previousLatency, int latency)
    {
        _heartbeat.Beat();
        return Task.CompletedTask;
    }

    private void OnHeartbeatTimeout()
    {
        if (_stopped)
        {
            return;
        }

        _logger.NoHeartbeatAcknowledgement(_heartbeatTimeout);
        _terminator.Terminate("Heartbeat lost.");
    }

    private bool TryBeginCheck(out CancellationToken checkToken)
    {
        lock (_gate)
        {
            checkToken = _stopped ? new CancellationToken(canceled: true) : _reconnectCts.Token;
            // A check on an older token is already cancelled or about to be (the token swap and the
            // cancel are not atomic), so only a check on the current token makes this event a duplicate.
            if (_runningCheckToken == checkToken)
            {
                return false;
            }

            _runningCheckToken = checkToken;
            return true;
        }
    }

    private void EndCheck(CancellationToken checkToken)
    {
        lock (_gate)
        {
            if (_runningCheckToken == checkToken)
            {
                _runningCheckToken = null;
            }
        }
    }

    private static void CancelAndDispose(CancellationTokenSource cts)
    {
        cts.Cancel();
        cts.Dispose();
    }
}
