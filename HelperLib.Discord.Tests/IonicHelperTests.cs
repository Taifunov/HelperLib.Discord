using Discord;
using Discord.WebSocket;

namespace HelperLib.Discord.Tests;

[Collection(ConsoleAndStaticStateCollection.Name)]
public class IonicHelperTests
{
    [Fact]
    public async Task RunAsync_throws_when_token_is_empty()
    {
        var helper = new IonicHelper(string.Empty);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            helper.RunAsync(new DiscordSocketConfig(), _ => Task.CompletedTask));

        Assert.Equal("Incorrect token! Pass valid one into IonicHelper.ctor", exception.Message);
    }

    [Fact]
    public void AttachToExistingClient_setsClientAndCancellationTokenSource()
    {
        using var client = new DiscordSocketClient(new DiscordSocketConfig());
        var helper = new IonicHelper();

        helper.AttachToExistingClient(client);

        Assert.Same(client, IonicHelper.Client);
        Assert.Equal(ConnectionState.Disconnected, client.ConnectionState);
        Assert.NotNull(IonicHelper.GetCancellationTokenSource());
    }

    [Fact]
    public async Task RunAsync_throws_when_config_is_null()
    {
        var helper = new IonicHelper(string.Empty);

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            helper.RunAsync(null!, _ => Task.CompletedTask));

        Assert.Equal("config", exception.ParamName);
    }

    [Fact]
    public async Task RunAsync_throws_when_logger_is_null()
    {
        var helper = new IonicHelper(string.Empty);

        var exception = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            helper.RunAsync(new DiscordSocketConfig(), null!));

        Assert.Equal("logger", exception.ParamName);
    }

    [Fact]
    public void AttachToExistingClient_throws_when_client_is_null()
    {
        var helper = new IonicHelper();

        var exception = Assert.Throws<ArgumentNullException>(() => helper.AttachToExistingClient(null!));

        Assert.Equal("client", exception.ParamName);
    }

    [Fact]
    public async Task AttachToExistingClient_twice_with_same_client_does_not_double_handlers()
    {
        var delayCalls = 0;
        using var client = new DiscordSocketClient(new DiscordSocketConfig());
        var helper = CreateHelper(() => delayCalls++);
        var events = new FakeClientEvents();

        helper.AttachClient(client, events.Events);
        helper.AttachClient(client, events.Events);
        await events.RaiseDisconnectedAsync();

        Assert.Single(events.ConnectedHandlers);
        Assert.Single(events.DisconnectedHandlers);
        Assert.Equal(1, delayCalls);
    }

    [Fact]
    public void AttachToExistingClient_with_new_client_unsubscribes_previous_client()
    {
        using var previousClient = new DiscordSocketClient(new DiscordSocketConfig());
        using var newClient = new DiscordSocketClient(new DiscordSocketConfig());
        var helper = CreateHelper(() => { });
        var previousEvents = new FakeClientEvents();
        var newEvents = new FakeClientEvents();

        helper.AttachClient(previousClient, previousEvents.Events);
        helper.AttachClient(newClient, newEvents.Events);

        Assert.Empty(previousEvents.ConnectedHandlers);
        Assert.Empty(previousEvents.DisconnectedHandlers);
        Assert.Single(newEvents.ConnectedHandlers);
        Assert.Single(newEvents.DisconnectedHandlers);
    }

    [Fact]
    public void Dispose_unsubscribes_handlers_and_can_be_called_twice()
    {
        using var client = new DiscordSocketClient(new DiscordSocketConfig());
        var helper = CreateHelper(() => { });
        var events = new FakeClientEvents();
        helper.AttachClient(client, events.Events);

        helper.Dispose();
        var exception = Record.Exception(helper.Dispose);

        Assert.Empty(events.ConnectedHandlers);
        Assert.Empty(events.DisconnectedHandlers);
        Assert.Null(exception);
    }

    [Fact]
    public void AttachToExistingClient_after_Dispose_throws()
    {
        using var client = new DiscordSocketClient(new DiscordSocketConfig());
        var helper = new IonicHelper();
        helper.Dispose();

        Assert.Throws<ObjectDisposedException>(() => helper.AttachToExistingClient(client));
    }

    [Fact]
    public async Task Disconnected_handler_logs_watchdog_failure_instead_of_throwing()
    {
        using var client = new DiscordSocketClient(new DiscordSocketConfig());
        // The delay throws synchronously, so the fire-and-forget watchdog finishes before the handler returns.
        var helper = new IonicHelper(
            string.Empty,
            TimeSpan.FromSeconds(1),
            (_, _) => throw new InvalidOperationException("delay failed"),
            _ => Task.CompletedTask,
            () => { });
        var events = new FakeClientEvents();
        helper.AttachClient(client, events.Events);
        var originalOutput = Console.Out;
        using var capturedOutput = new StringWriter();
        Console.SetOut(capturedOutput);

        try
        {
            await events.RaiseDisconnectedAsync();
        }
        finally
        {
            Console.SetOut(originalOutput);
        }

        var output = capturedOutput.ToString();
        Assert.Contains("Reconnect watchdog failed", output);
        Assert.Contains("delay failed", output);
    }

    // The watchdog's delay never completes, so the client is never asked to restart.
    private static IonicHelper CreateHelper(Action onDisconnectDelayStarted) =>
        new(
            string.Empty,
            TimeSpan.FromSeconds(1),
            (_, cancellationToken) =>
            {
                onDisconnectDelayStarted();
                return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            },
            _ => Task.CompletedTask,
            () => { });

    // Discord.Net exposes no way to raise its events, so the helper subscribes through this fake instead.
    private sealed class FakeClientEvents
    {
        public FakeClientEvents()
        {
            Events = new ClientEvents(
                ConnectedHandlers.Add,
                handler => ConnectedHandlers.Remove(handler),
                DisconnectedHandlers.Add,
                handler => DisconnectedHandlers.Remove(handler));
        }

        public ClientEvents Events { get; }

        public List<Func<Task>> ConnectedHandlers { get; } = [];

        public List<Func<Exception, Task>> DisconnectedHandlers { get; } = [];

        public async Task RaiseDisconnectedAsync()
        {
            foreach (var handler in DisconnectedHandlers.ToList())
            {
                await handler(new InvalidOperationException("disconnected"));
            }
        }
    }
}
