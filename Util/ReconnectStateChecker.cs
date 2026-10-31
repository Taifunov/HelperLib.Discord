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
    internal static async Task CheckStateAsync(
        Func<ConnectionState> getConnectionState,
        Func<Task> startAsync,
        Func<TimeSpan, Task> delayAsync,
        TimeSpan timeout,
        ILogger logger,
        Action<string> terminate)
    {
        if (getConnectionState() == ConnectionState.Connected)
        {
            return;
        }

        if (!ReconnectGuard.IsSafeToRestart(getConnectionState()))
        {
            logger.ClientAlreadyReconnecting();
            if (!await WaitForSettledStateAsync(getConnectionState, delayAsync, timeout))
            {
                terminate("Client stuck reconnecting.");
                return;
            }

            if (getConnectionState() == ConnectionState.Connected)
            {
                return;
            }
        }

        logger.ResettingClient();

        Task connect;
        try
        {
            connect = startAsync();
        }
        catch (Exception ex)
        {
            logger.ClientResetFaulted(ex);
            terminate("Client reset faulted.");
            return;
        }

        // StartAsync completes once the reconnect is scheduled, so it can still fault while we poll.
        if (!await WaitForConnectedOrFaultAsync(getConnectionState, connect, delayAsync, timeout))
        {
            terminate("Client reset timed out.");
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

            terminate("Client reset faulted.");
            return;
        }

        logger.ClientResetSucceeded();
    }

    private static Task<bool> WaitForConnectedOrFaultAsync(
        Func<ConnectionState> getConnectionState,
        Task connect,
        Func<TimeSpan, Task> delayAsync,
        TimeSpan timeout) =>
        WaitForStateAsync(
            getConnectionState,
            state => state == ConnectionState.Connected || HasFailed(connect),
            delayAsync,
            timeout);

    private static bool HasFailed(Task connect) => connect.IsFaulted || connect.IsCanceled;

    /// <summary>
    /// Polls while the client is transiently Connecting/Disconnecting, so that a reconnect
    /// truly stuck in one of those states (rather than just slow) is still caught by the
    /// backstop instead of being ignored forever.
    /// </summary>
    private static Task<bool> WaitForSettledStateAsync(
        Func<ConnectionState> getConnectionState,
        Func<TimeSpan, Task> delayAsync,
        TimeSpan timeout) =>
        WaitForStateAsync(
            getConnectionState,
            static state => ReconnectGuard.IsSafeToRestart(state) || state == ConnectionState.Connected,
            delayAsync,
            timeout);

    private static async Task<bool> WaitForStateAsync(
        Func<ConnectionState> getConnectionState,
        Func<ConnectionState, bool> isTargetState,
        Func<TimeSpan, Task> delayAsync,
        TimeSpan timeout)
    {
        var elapsed = TimeSpan.Zero;
        while (!isTargetState(getConnectionState()))
        {
            if (elapsed >= timeout)
            {
                return false;
            }

            await delayAsync(PollInterval);
            elapsed += PollInterval;
        }

        return true;
    }

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
}
