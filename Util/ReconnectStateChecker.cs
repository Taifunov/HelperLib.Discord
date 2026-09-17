using global::Discord;

namespace HelperLib.Discord.Util;

/// <summary>
/// Shared backstop logic used by both HelperLib.Discord.Watchdog.ReconnectWatchdog and
/// HelperLib.Discord.Services.ReliabilityService: restart the client if it's Disconnected,
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
        Action<string> log,
        Action<string> terminate)
    {
        if (getConnectionState() == ConnectionState.Connected)
        {
            return;
        }

        if (!ReconnectGuard.IsSafeToRestart(getConnectionState()))
        {
            log("Client is already reconnecting, waiting for it to settle...");
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

        log("Attempting to reset the client");

        Task connect;
        try
        {
            connect = startAsync();
        }
        catch (Exception ex)
        {
            log($"Client reset faulted: {ex}");
            terminate("Client reset faulted.");
            return;
        }

        if (connect.IsFaulted)
        {
            log($"Client reset faulted: {connect.Exception}");
            terminate("Client reset faulted.");
            return;
        }

        if (!await WaitForConnectedAsync(getConnectionState, delayAsync, timeout))
        {
            terminate("Client reset timed out.");
            return;
        }

        log("Client reset successfully.");
    }

    private static Task<bool> WaitForConnectedAsync(
        Func<ConnectionState> getConnectionState,
        Func<TimeSpan, Task> delayAsync,
        TimeSpan timeout) =>
        WaitForStateAsync(
            getConnectionState,
            static state => state == ConnectionState.Connected,
            delayAsync,
            timeout);

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
            if (elapsed >= timeout) return false;
            await delayAsync(PollInterval);
            elapsed += PollInterval;
        }

        return true;
    }

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
}
