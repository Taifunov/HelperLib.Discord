using global::Discord;
using HelperLib.Discord.Watchdog;
using Moq;

namespace HelperLib.Discord.Tests.Watchdog;

public class ReconnectWatchdogTests
{
    [Fact]
    public async Task ClientOnConnected_resets_reconnect_token()
    {
        var watchdog = new ReconnectWatchdog(
            resetTimeout: TimeSpan.FromSeconds(1),
            delayAsync: (_, _) => Task.CompletedTask,
            delayWithoutCancellationAsync: _ => Task.CompletedTask,
            failFast: () => { });

        var firstToken = watchdog.GetReconnectCancellationToken();

        await watchdog.ClientOnConnected();

        var secondToken = watchdog.GetReconnectCancellationToken();
        Assert.True(firstToken.IsCancellationRequested);
        Assert.False(secondToken.IsCancellationRequested);
    }

    [Fact]
    public async Task ClientOnDisconnected_does_not_check_state_after_reconnect_resets_token()
    {
        var startCalls = 0;
        var delayReleased = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = CreateClient(ConnectionState.Disconnected, () =>
        {
            startCalls++;
            return Task.CompletedTask;
        });
        var watchdog = new ReconnectWatchdog(
            resetTimeout: TimeSpan.FromSeconds(1),
            delayAsync: (_, _) => delayReleased.Task,
            delayWithoutCancellationAsync: _ => Task.Delay(Timeout.InfiniteTimeSpan),
            failFast: () => { });

        var disconnectedTask = watchdog.ClientOnDisconnected(client.Object);
        await watchdog.ClientOnConnected();
        delayReleased.SetResult();
        await disconnectedTask;

        Assert.Equal(0, startCalls);
    }

    [Fact]
    public async Task CheckStateAsync_returns_when_client_is_connected()
    {
        var startCalls = 0;
        var client = CreateClient(ConnectionState.Connected, () =>
        {
            startCalls++;
            return Task.CompletedTask;
        });
        var watchdog = CreateWatchdog();

        await watchdog.CheckStateAsync(client.Object);

        Assert.Equal(0, startCalls);
    }

    [Fact]
    public async Task CheckStateAsync_fails_fast_when_client_stays_connecting()
    {
        var startCalls = 0;
        var failFastCalls = 0;
        var client = CreateClient(ConnectionState.Connecting, () =>
        {
            startCalls++;
            return Task.CompletedTask;
        });
        var watchdog = new ReconnectWatchdog(
            resetTimeout: TimeSpan.FromSeconds(1),
            delayAsync: (_, _) => Task.CompletedTask,
            delayWithoutCancellationAsync: _ => Task.CompletedTask,
            failFast: () => failFastCalls++);

        await watchdog.CheckStateAsync(client.Object);

        Assert.Equal(0, startCalls);
        Assert.Equal(1, failFastCalls);
    }

    [Fact]
    public async Task CheckStateAsync_fails_fast_when_client_stays_disconnecting()
    {
        var startCalls = 0;
        var failFastCalls = 0;
        var client = CreateClient(ConnectionState.Disconnecting, () =>
        {
            startCalls++;
            return Task.CompletedTask;
        });
        var watchdog = new ReconnectWatchdog(
            resetTimeout: TimeSpan.FromSeconds(1),
            delayAsync: (_, _) => Task.CompletedTask,
            delayWithoutCancellationAsync: _ => Task.CompletedTask,
            failFast: () => failFastCalls++);

        await watchdog.CheckStateAsync(client.Object);

        Assert.Equal(0, startCalls);
        Assert.Equal(1, failFastCalls);
    }

    [Fact]
    public async Task CheckStateAsync_recovers_without_restart_when_connecting_settles_to_connected()
    {
        var startCalls = 0;
        var state = ConnectionState.Connecting;
        var client = CreateClient(() => state, () =>
        {
            startCalls++;
            return Task.CompletedTask;
        });
        var failFastCalls = 0;
        var watchdog = new ReconnectWatchdog(
            resetTimeout: TimeSpan.FromSeconds(5),
            delayAsync: (_, _) => Task.CompletedTask,
            delayWithoutCancellationAsync: _ =>
            {
                state = ConnectionState.Connected;
                return Task.CompletedTask;
            },
            failFast: () => failFastCalls++);

        await watchdog.CheckStateAsync(client.Object);

        Assert.Equal(0, startCalls);
        Assert.Equal(0, failFastCalls);
    }

    [Fact]
    public async Task CheckStateAsync_restarts_when_connecting_settles_to_disconnected()
    {
        var startCalls = 0;
        var state = ConnectionState.Connecting;
        var client = CreateClient(() => state, () =>
        {
            startCalls++;
            state = ConnectionState.Connected;
            return Task.CompletedTask;
        });
        var failFastCalls = 0;
        var watchdog = new ReconnectWatchdog(
            resetTimeout: TimeSpan.FromSeconds(5),
            delayAsync: (_, _) => Task.CompletedTask,
            delayWithoutCancellationAsync: _ =>
            {
                if (state == ConnectionState.Connecting)
                {
                    state = ConnectionState.Disconnected;
                }

                return Task.CompletedTask;
            },
            failFast: () => failFastCalls++);

        await watchdog.CheckStateAsync(client.Object);

        Assert.Equal(1, startCalls);
        Assert.Equal(0, failFastCalls);
    }

    [Fact]
    public async Task CheckStateAsync_starts_client_and_succeeds_when_it_reconnects()
    {
        var startCalls = 0;
        var client = CreateClient(ConnectionState.Disconnected, () =>
        {
            startCalls++;
            return Task.CompletedTask;
        });
        // Simulate Discord.Net reaching Connected after the restart is dispatched.
        client.SetupSequence(c => c.ConnectionState)
            .Returns(ConnectionState.Disconnected)
            .Returns(ConnectionState.Disconnected)
            .Returns(ConnectionState.Connecting)
            .Returns(ConnectionState.Connected);
        var failFastCalls = 0;
        var watchdog = new ReconnectWatchdog(
            resetTimeout: TimeSpan.FromSeconds(5),
            delayAsync: (_, _) => Task.CompletedTask,
            delayWithoutCancellationAsync: _ => Task.CompletedTask,
            failFast: () => failFastCalls++);

        await watchdog.CheckStateAsync(client.Object);

        Assert.Equal(1, startCalls);
        Assert.Equal(0, failFastCalls);
    }

    [Fact]
    public async Task CheckStateAsync_fails_fast_when_client_never_reconnects()
    {
        var failFastCalls = 0;
        var client = CreateClient(ConnectionState.Disconnected, () => Task.CompletedTask);
        var watchdog = new ReconnectWatchdog(
            resetTimeout: TimeSpan.FromSeconds(1),
            delayAsync: (_, _) => Task.CompletedTask,
            delayWithoutCancellationAsync: _ => Task.CompletedTask,
            failFast: () => failFastCalls++);

        await watchdog.CheckStateAsync(client.Object);

        Assert.Equal(1, failFastCalls);
    }

    [Fact]
    public async Task CheckStateAsync_fails_fast_when_restart_faults()
    {
        var failFastCalls = 0;
        var client = CreateClient(
            ConnectionState.Disconnected,
            () => Task.FromException(new InvalidOperationException("boom")));
        var watchdog = new ReconnectWatchdog(
            resetTimeout: TimeSpan.FromSeconds(1),
            delayAsync: (_, _) => Task.Delay(Timeout.InfiniteTimeSpan),
            delayWithoutCancellationAsync: _ => Task.Delay(Timeout.InfiniteTimeSpan),
            failFast: () => failFastCalls++);

        await watchdog.CheckStateAsync(client.Object);

        Assert.Equal(1, failFastCalls);
    }

    [Fact]
    public async Task CheckStateAsync_fails_fast_when_start_async_throws_synchronously()
    {
        var failFastCalls = 0;
        var client = CreateClient(
            ConnectionState.Disconnected,
            () => throw new InvalidOperationException("boom"));
        var watchdog = new ReconnectWatchdog(
            resetTimeout: TimeSpan.FromSeconds(1),
            delayAsync: (_, _) => Task.Delay(Timeout.InfiniteTimeSpan),
            delayWithoutCancellationAsync: _ => Task.Delay(Timeout.InfiniteTimeSpan),
            failFast: () => failFastCalls++);

        await watchdog.CheckStateAsync(client.Object);

        Assert.Equal(1, failFastCalls);
    }

    [Fact]
    public async Task CheckStateAsync_still_fails_fast_via_timeout_when_restart_faults_asynchronously()
    {
        // IsFaulted is only checked synchronously right after StartAsync() returns.
        // A real Discord.Net client that fails some time after StartAsync() is called
        // (its normal failure mode) is not caught by that check, and falls through to
        // WaitForConnectedAsync instead, timing out. The backstop still fires (failFast
        // is still called), just via the timeout path rather than the fault path.
        var failFastCalls = 0;
        var connectFaults = new TaskCompletionSource();
        var client = CreateClient(ConnectionState.Disconnected, () => connectFaults.Task);
        var watchdog = new ReconnectWatchdog(
            resetTimeout: TimeSpan.Zero,
            delayAsync: (_, _) => Task.CompletedTask,
            delayWithoutCancellationAsync: _ => Task.CompletedTask,
            failFast: () => failFastCalls++);

        await watchdog.CheckStateAsync(client.Object);

        Assert.Equal(1, failFastCalls);

        connectFaults.SetException(new InvalidOperationException("boom, but too late to matter"));
    }

    private static ReconnectWatchdog CreateWatchdog() =>
        new(
            resetTimeout: TimeSpan.FromSeconds(1),
            delayAsync: (_, _) => Task.CompletedTask,
            delayWithoutCancellationAsync: _ => Task.Delay(Timeout.InfiniteTimeSpan),
            failFast: () => { });

    private static Mock<IDiscordClient> CreateClient(
        ConnectionState connectionState,
        Func<Task> startAsync) =>
        CreateClient(() => connectionState, startAsync);

    private static Mock<IDiscordClient> CreateClient(
        Func<ConnectionState> getConnectionState,
        Func<Task> startAsync)
    {
        var client = new Mock<IDiscordClient>();
        client.Setup(c => c.ConnectionState).Returns(getConnectionState);
        client.Setup(c => c.StartAsync()).Returns(startAsync);
        return client;
    }
}
