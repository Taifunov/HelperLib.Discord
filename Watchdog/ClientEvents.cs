using Discord.WebSocket;

namespace HelperLib.Discord.Watchdog;

/// <summary>
/// The Connected, Disconnected and LatencyUpdated subscriptions of one client. Discord.Net gives no way to
/// raise its events, so tests replace this seam with a fake.
/// </summary>
internal sealed record ClientEvents(
    Action<Func<Task>> AddConnected,
    Action<Func<Task>> RemoveConnected,
    Action<Func<Exception, Task>> AddDisconnected,
    Action<Func<Exception, Task>> RemoveDisconnected,
    Action<Func<int, int, Task>> AddLatencyUpdated,
    Action<Func<int, int, Task>> RemoveLatencyUpdated)
{
    internal static ClientEvents For(DiscordSocketClient client) =>
        new(
            handler => client.Connected += handler,
            handler => client.Connected -= handler,
            handler => client.Disconnected += handler,
            handler => client.Disconnected -= handler,
            handler => client.LatencyUpdated += handler,
            handler => client.LatencyUpdated -= handler);
}
