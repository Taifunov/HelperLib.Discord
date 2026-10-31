using Discord;
using HelperLib.Discord.Util;
using Microsoft.Extensions.Logging;

namespace HelperLib.Discord.Watchdog;

internal class ReconnectWatchdog(
    TimeSpan resetTimeout,
    Func<TimeSpan, CancellationToken, Task> delayAsync,
    Func<TimeSpan, Task> delayWithoutCancellationAsync,
    Action failFast,
    ILogger logger) : IDisposable
{
    private readonly Lock _reconnectCtsGate = new();
    private CancellationTokenSource _reconnectCts = new();
    private volatile bool _disposed;

    internal CancellationToken GetReconnectCancellationToken()
    {
        lock (_reconnectCtsGate)
        {
            return _disposed ? new CancellationToken(canceled: true) : _reconnectCts.Token;
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
        logger.ClientDisconnected();

        var token = GetReconnectCancellationToken();
        try
        {
            await delayAsync(resetTimeout, token);
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
        await CheckStateAsync(client);
        logger.StateCameBackOkay();
    }

    internal Task CheckStateAsync(IDiscordClient client) =>
        ReconnectStateChecker.CheckStateAsync(
            () => client.ConnectionState,
            client.StartAsync,
            delayWithoutCancellationAsync,
            resetTimeout,
            logger,
            terminate: reason =>
            {
                logger.KillingProcess(reason);
                failFast();
            });

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
