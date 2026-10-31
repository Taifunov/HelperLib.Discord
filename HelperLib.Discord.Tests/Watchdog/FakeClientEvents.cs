using HelperLib.Discord.Watchdog;

namespace HelperLib.Discord.Tests.Watchdog;

/// <summary>Stands in for the gateway client's events, so tests can raise them without a connection.</summary>
internal sealed class FakeClientEvents
{
    public event Func<Task>? Connected;

    public event Func<Exception, Task>? Disconnected;

    public event Func<int, int, Task>? LatencyUpdated;

    public ClientEvents Seam => new(
        handler => Connected += handler,
        handler => Connected -= handler,
        handler => Disconnected += handler,
        handler => Disconnected -= handler,
        handler => LatencyUpdated += handler,
        handler => LatencyUpdated -= handler);

    public int SubscriberCount =>
        (Connected?.GetInvocationList().Length ?? 0)
        + (Disconnected?.GetInvocationList().Length ?? 0)
        + (LatencyUpdated?.GetInvocationList().Length ?? 0);

    public Task RaiseConnected() => Connected?.Invoke() ?? Task.CompletedTask;

    public Task RaiseDisconnected() =>
        Disconnected?.Invoke(new InvalidOperationException("gateway closed")) ?? Task.CompletedTask;

    public Task RaiseLatencyUpdated() => LatencyUpdated?.Invoke(40, 41) ?? Task.CompletedTask;
}
