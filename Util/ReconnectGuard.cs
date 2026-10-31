using Discord;

namespace HelperLib.Discord.Util;

internal static class ReconnectGuard
{
    public static bool IsSafeToRestart(ConnectionState state) => state == ConnectionState.Disconnected;
}
