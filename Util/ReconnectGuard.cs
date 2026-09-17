using global::Discord;

namespace HelperLib.Discord.Util;

internal static class ReconnectGuard
{
    /// <summary>
    /// Discord.Net's StartAsync() throws "Cannot start an already running client."
    /// for any state other than Disconnected, so only Disconnected is safe to restart.
    /// Connecting/Disconnecting are left alone: they are transient states Discord.Net
    /// is already handling on its own.
    /// </summary>
    public static bool IsSafeToRestart(ConnectionState state) => state == ConnectionState.Disconnected;
}
