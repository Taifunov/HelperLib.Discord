using Discord;
using Discord.WebSocket;
using HelperLib.Discord.Util;
using Microsoft.Extensions.Logging;

namespace HelperLib.Discord.Tests;

[Collection(ConsoleAndStaticStateCollection.Name)]
public class IonicHelperTests : IDisposable
{
    // The constructors and attach calls set static state (logger, default guild id, client); reset it for the next test.
    public void Dispose()
    {
        IonicHelper.Client = null;
        GuildLookup.SetLogger(ConsoleLogger.Instance);
        GuildLookup.SetDefaultGuildId(null);
        GC.SuppressFinalize(this);
    }

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
    public async Task RunAsync_after_Dispose_throws()
    {
        var helper = new IonicHelper("token");
        helper.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            helper.RunAsync(new DiscordSocketConfig(), _ => Task.CompletedTask));
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
    public async Task AttachClient_racing_Dispose_throws_only_ObjectDisposedException_and_leaves_no_handlers()
    {
        using var client = new DiscordSocketClient(new DiscordSocketConfig());
        var helper = CreateHelper(() => { });
        var fakes = Enumerable.Range(0, 200).Select(_ => new FakeClientEvents()).ToArray();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var attachTasks = fakes.Select(fake => Task.Run(async () =>
        {
            await start.Task;
            try
            {
                helper.AttachClient(client, fake.Events);
            }
            catch (ObjectDisposedException)
            {
                // Expected for attaches that lose the race against Dispose.
            }
        })).ToArray();
        var disposeTask = Task.Run(async () =>
        {
            await start.Task;
            helper.Dispose();
        });
        start.SetResult();
        await Task.WhenAll(attachTasks.Append(disposeTask));

        Assert.All(fakes, fake =>
        {
            Assert.Empty(fake.ConnectedHandlers);
            Assert.Empty(fake.DisconnectedHandlers);
        });
    }

    [Fact]
    public async Task Dispose_called_from_many_threads_is_a_no_op_after_the_first_call()
    {
        using var client = new DiscordSocketClient(new DiscordSocketConfig());
        var helper = CreateHelper(() => { });
        var events = new FakeClientEvents();
        helper.AttachClient(client, events.Events);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var disposeTasks = Enumerable.Range(0, 50).Select(_ => Task.Run(async () =>
        {
            await start.Task;
            helper.Dispose();
        })).ToArray();
        start.SetResult();
        var exception = await Record.ExceptionAsync(() => Task.WhenAll(disposeTasks));

        Assert.Null(exception);
        Assert.Empty(events.ConnectedHandlers);
        Assert.Empty(events.DisconnectedHandlers);
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
        var logger = new ListLogger();
        using var client = new DiscordSocketClient(new DiscordSocketConfig());
        // The delay throws synchronously, so the fire-and-forget watchdog finishes before the handler returns.
        var helper = new IonicHelper(
            string.Empty,
            TimeSpan.FromSeconds(1),
            (_, _) => throw new InvalidOperationException("delay failed"),
            () => { },
            logger: logger);
        var events = new FakeClientEvents();
        helper.AttachClient(client, events.Events);

        await events.RaiseDisconnectedAsync();

        var failure = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Error);
        Assert.Equal("Reconnect watchdog failed.", failure.Message);
        Assert.Equal("delay failed", failure.Exception?.Message);
    }

    [Fact]
    public void Constructor_forwards_the_logger_to_GuildLookup()
    {
        var logger = new ListLogger();
        using var helper = new IonicHelper(defaultGuildId: null, logger: logger);

        GuildLookup.GetGuild(null, out _);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("No guild id given and no default guild id set!", entry.Message);
    }

    [Fact]
    public void Constructor_without_a_logger_keeps_the_logger_GuildLookup_already_has()
    {
        var logger = new ListLogger();
        GuildLookup.SetLogger(logger);
        using var helper = new IonicHelper();

        GuildLookup.GetGuild(null, out _);

        Assert.Single(logger.Entries);
    }

    [Fact]
    public void Constructor_without_a_default_guild_id_keeps_the_current_one()
    {
        var logger = new ListLogger();
        GuildLookup.SetLogger(logger);
        GuildLookup.SetDefaultGuildId(42UL);
        using var helper = new IonicHelper();

        GuildLookup.GetGuild(null, out _);

        Assert.DoesNotContain(logger.Entries, entry => entry.Message == "No guild id given and no default guild id set!");
    }

    [Fact]
    public void Constructor_logs_the_default_guild_id_when_one_is_given()
    {
        var logger = new ListLogger();
        using var helper = new IonicHelper(123UL, logger);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("Default GuildId was successfully set to 123", entry.Message);
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
