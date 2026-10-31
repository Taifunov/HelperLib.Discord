using Discord;
using Microsoft.Extensions.Logging;

namespace HelperLib.Discord.Util;

/// <summary>
/// Backstop logic used by HelperLib.Discord.Watchdog.ReconnectWatchdog: restart the client if it's Disconnected,
/// wait out transient Connecting/Disconnecting states, and fail fast if the client
/// never reaches Connected within the timeout.
/// </summary>
internal static class ReconnectStateChecker
{
    /// <summary>
    /// Returns quietly, without restarting the client or terminating, once
    /// <paramref name="cancellationToken"/> is cancelled (the watchdog is disposed or the client reconnected).
    /// </summary>
    internal static async Task CheckStateAsync(
        Func<ConnectionState> getConnectionState,
        Func<Task> startAsync,
        Func<TimeSpan, CancellationToken, Task> delayAsync,
        TimeSpan timeout,
        ILogger logger,
        Action<string> terminate,
        CancellationToken cancellationToken)
    {
        try
        {
            await CheckStateCoreAsync(getConnectionState, startAsync, delayAsync, timeout, logger, terminate, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static async Task CheckStateCoreAsync(
        Func<ConnectionState> getConnectionState,
        Func<Task> startAsync,
        Func<TimeSpan, CancellationToken, Task> delayAsync,
        TimeSpan timeout,
        ILogger logger,
        Action<string> terminate,
        CancellationToken cancellationToken)
    {
        if (getConnectionState() == ConnectionState.Connected)
        {
            return;
        }

        if (!ReconnectGuard.IsSafeToRestart(getConnectionState()))
        {
            logger.ClientAlreadyReconnecting();
            if (!await WaitForSettledStateAsync(getConnectionState, delayAsync, timeout, cancellationToken).ConfigureAwait(false))
            {
                TerminateUnlessCancelled("Client stuck reconnecting.");
                return;
            }

            if (getConnectionState() == ConnectionState.Connected)
            {
                return;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        logger.ResettingClient();

        Task connect;
        try
        {
            connect = startAsync();
            // Reading Exception marks it observed on every exit path, so finalization raises no UnobservedTaskException.
            _ = connect.ContinueWith(
                static task => _ = task.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.ClientResetFaulted(ex);
            TerminateUnlessCancelled("Client reset faulted.");
            return;
        }

        // StartAsync completes once the reconnect is scheduled, so it can still fault while we poll.
        if (!await WaitForConnectedOrFaultAsync(getConnectionState, connect, delayAsync, timeout, cancellationToken).ConfigureAwait(false))
        {
            TerminateUnlessCancelled("Client reset timed out.");
            return;
        }

        if (HasFailed(connect) && getConnectionState() != ConnectionState.Connected)
        {
            if (connect.IsFaulted)
            {
                logger.ClientResetFaulted(connect.Exception);
            }
            else
            {
                logger.ClientResetCancelled();
            }

            TerminateUnlessCancelled("Client reset faulted.");
            return;
        }

        logger.ClientResetSucceeded();

        void TerminateUnlessCancelled(string reason)
        {
            cancellationToken.ThrowIfCancellationRequested();
            terminate(reason);
        }
    }

    private static Task<bool> WaitForConnectedOrFaultAsync(
        Func<ConnectionState> getConnectionState,
        Task connect,
        Func<TimeSpan, CancellationToken, Task> delayAsync,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        WaitForStateAsync(
            getConnectionState,
            state => state == ConnectionState.Connected || HasFailed(connect),
            delayAsync,
            timeout,
            cancellationToken);

    private static bool HasFailed(Task connect) => connect.IsFaulted || connect.IsCanceled;

    /// <summary>
    /// Polls while the client is transiently Connecting/Disconnecting, so that a reconnect
    /// truly stuck in one of those states (rather than just slow) is still caught by the
    /// backstop instead of being ignored forever.
    /// </summary>
    private static Task<bool> WaitForSettledStateAsync(
        Func<ConnectionState> getConnectionState,
        Func<TimeSpan, CancellationToken, Task> delayAsync,
        TimeSpan timeout,
        CancellationToken cancellationToken) =>
        WaitForStateAsync(
            getConnectionState,
            static state => ReconnectGuard.IsSafeToRestart(state) || state == ConnectionState.Connected,
            delayAsync,
            timeout,
            cancellationToken);

    private static async Task<bool> WaitForStateAsync(
        Func<ConnectionState> getConnectionState,
        Func<ConnectionState, bool> isTargetState,
        Func<TimeSpan, CancellationToken, Task> delayAsync,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var elapsed = TimeSpan.Zero;
        while (!isTargetState(getConnectionState()))
        {
            if (elapsed >= timeout)
            {
                return false;
            }

            await delayAsync(PollInterval, cancellationToken).ConfigureAwait(false);
            elapsed += PollInterval;
        }

        return true;
    }

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
}
