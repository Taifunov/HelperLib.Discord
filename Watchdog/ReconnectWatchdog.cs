using Discord;
using HelperLib.Discord.Util;
using Microsoft.Extensions.Logging;

namespace HelperLib.Discord.Watchdog;

internal class ReconnectWatchdog(
    TimeSpan resetTimeout,
    Func<TimeSpan, CancellationToken, Task> delayAsync,
    Action failFast,
    ILogger logger) : IDisposable
{
    private readonly Lock _reconnectCtsGate = new();
    private CancellationTokenSource _reconnectCts = new();
    private volatile bool _disposed;

    private CancellationToken? _runningCheckToken;

    internal CancellationToken GetReconnectCancellationToken()
    {
        lock (_reconnectCtsGate)
        {
            return CurrentTokenUnderGate();
        }
    }

    internal void Arm()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        TryReplaceReconnectCts();
    }

    internal Task ClientOnConnected()
    {
        logger.ClientReconnected();

        if (TryReplaceReconnectCts())
        {
            logger.CancelTokensReset();
        }

        return Task.CompletedTask;
    }

    internal async Task ClientOnDisconnected(IDiscordClient client)
    {
        if (_disposed)
        {
            return;
        }

        // Discord.Net raises Disconnected on every failed reconnect attempt. A second concurrent check
        // could call StartAsync on a client that is already connecting and wrongly end the process.
        if (!TryBeginCheck(out var token))
        {
            logger.DuplicateDisconnectIgnored();
            return;
        }

        try
        {
            logger.ClientDisconnected();

            try
            {
                await delayAsync(resetTimeout, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (token.IsCancellationRequested)
            {
                return;
            }

            logger.TimeoutExpired();
            await CheckStateAsync(client, token).ConfigureAwait(false);
            if (!token.IsCancellationRequested)
            {
                logger.StateCameBackOkay();
            }
        }
        finally
        {
            EndCheck(token);
        }
    }

    private bool TryBeginCheck(out CancellationToken checkToken)
    {
        lock (_reconnectCtsGate)
        {
            checkToken = CurrentTokenUnderGate();
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
        lock (_reconnectCtsGate)
        {
            if (_runningCheckToken == checkToken)
            {
                _runningCheckToken = null;
            }
        }
    }

    private CancellationToken CurrentTokenUnderGate() =>
        _disposed ? new CancellationToken(canceled: true) : _reconnectCts.Token;

    internal Task CheckStateAsync(IDiscordClient client, CancellationToken cancellationToken = default) =>
        ReconnectStateChecker.CheckStateAsync(
            () => client.ConnectionState,
            client.StartAsync,
            delayAsync,
            resetTimeout,
            logger,
            terminate: reason =>
            {
                logger.KillingProcess(reason);
                failFast();
            },
            cancellationToken);

    public void Dispose()
    {
        CancellationTokenSource lastCts;
        lock (_reconnectCtsGate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            lastCts = _reconnectCts;
        }

        CancelAndDispose(lastCts);
    }

    private bool TryReplaceReconnectCts()
    {
        CancellationTokenSource previousCts;
        lock (_reconnectCtsGate)
        {
            if (_disposed)
            {
                return false;
            }

            previousCts = _reconnectCts;
            _reconnectCts = new CancellationTokenSource();
        }

        CancelAndDispose(previousCts);
        return true;
    }

    private static void CancelAndDispose(CancellationTokenSource cts)
    {
        cts.Cancel();
        cts.Dispose();
    }
}
