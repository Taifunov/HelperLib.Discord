using Discord;
using HelperLib.Discord.Util;
using HelperLib.Discord.Watchdog;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HelperLib.Discord.Tests.Watchdog;

[Collection(ConsoleAndStaticStateCollection.Name)]
public class ReconnectWatchdogTests
{
    [Fact]
    public async Task ClientOnConnected_resets_reconnect_token()
    {
        var watchdog = new ReconnectWatchdog(
            resetTimeout: TimeSpan.FromSeconds(1),
            delayAsync: (_, _) => Task.CompletedTask,
            failFast: () => { },
            logger: NullLogger.Instance);

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
            failFast: () => { },
            logger: NullLogger.Instance);

        var disconnectedTask = watchdog.ClientOnDisconnected(client.Object);
        await watchdog.ClientOnConnected();
        delayReleased.SetResult();
        await disconnectedTask;

        Assert.Equal(0, startCalls);
    }

    [Fact]
    public async Task Dispose_cancels_pending_reconnect_wait_without_checking_state()
    {
        var startCalls = 0;
        var failFastCalls = 0;
        var client = CreateClient(ConnectionState.Disconnected, () =>
        {
            startCalls++;
            return Task.CompletedTask;
        });
        var watchdog = new ReconnectWatchdog(
            resetTimeout: TimeSpan.FromSeconds(1),
            delayAsync: (_, token) => Task.Delay(Timeout.InfiniteTimeSpan, token),
            failFast: () => failFastCalls++,
            logger: NullLogger.Instance);

        var disconnectedTask = watchdog.ClientOnDisconnected(client.Object);
        watchdog.Dispose();
        await disconnectedTask;

        Assert.Equal(0, startCalls);
        Assert.Equal(0, failFastCalls);
    }

    [Fact]
    public async Task ClientOnDisconnected_while_a_check_is_running_is_dropped()
    {
        var delayCalls = 0;
        var startCalls = 0;
        var failFastCalls = 0;
        var state = ConnectionState.Disconnected;
        var timeoutWait = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = CreateClient(() => state, () =>
        {
            startCalls++;
            state = ConnectionState.Connected;
            return Task.CompletedTask;
        });
        var watchdog = new ReconnectWatchdog(
            resetTimeout: TimeSpan.FromSeconds(1),
            delayAsync: (_, _) =>
            {
                delayCalls++;
                return timeoutWait.Task;
            },
            failFast: () => failFastCalls++,
            logger: NullLogger.Instance);

        var firstCheck = watchdog.ClientOnDisconnected(client.Object);
        await watchdog.ClientOnDisconnected(client.Object);
        timeoutWait.SetResult();
        await firstCheck;

        Assert.Equal(1, delayCalls);
        Assert.Equal(1, startCalls);
        Assert.Equal(0, failFastCalls);
    }

    [Fact]
    public async Task ClientOnDisconnected_after_Dispose_logs_nothing_and_starts_no_check()
    {
        var delayCalls = 0;
        var logger = new ListLogger();
        var client = CreateClient(ConnectionState.Disconnected, () => Task.CompletedTask);
        var watchdog = new ReconnectWatchdog(
            resetTimeout: TimeSpan.FromSeconds(1),
            delayAsync: (_, _) =>
            {
                delayCalls++;
                return Task.CompletedTask;
            },
            failFast: () => { },
            logger: logger);
        watchdog.Dispose();

        await watchdog.ClientOnDisconnected(client.Object);

        Assert.Equal(0, delayCalls);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task ClientOnDisconnected_after_Connected_starts_a_new_check_before_the_cancelled_one_unwinds()
    {
        var delayCalls = 0;
        var delays = new[]
        {
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
        };
        var client = CreateClient(ConnectionState.Connected, () => Task.CompletedTask);
        var watchdog = new ReconnectWatchdog(
            resetTimeout: TimeSpan.FromSeconds(1),
            // The first delay ignores cancellation, standing in for a check that has not unwound yet.
            delayAsync: (_, _) => delays[Interlocked.Increment(ref delayCalls) - 1].Task,
            failFast: () => { },
            logger: NullLogger.Instance);

        var firstCheck = watchdog.ClientOnDisconnected(client.Object);
        await watchdog.ClientOnConnected();
        var secondCheck = watchdog.ClientOnDisconnected(client.Object);

        Assert.Equal(2, delayCalls);

        // The first check unwinding must not free the slot of the second one.
        delays[0].SetResult();
        await firstCheck;
        await watchdog.ClientOnDisconnected(client.Object);

        Assert.Equal(2, delayCalls);

        delays[1].SetResult();
        await secondCheck;
    }

    [Fact]
    public async Task Overlapping_events_and_Dispose_from_many_threads_never_throw_or_restart_the_client()
    {
        var startCalls = 0;
        var failFastCalls = 0;
        var client = CreateClient(ConnectionState.Disconnected, () =>
        {
            Interlocked.Increment(ref startCalls);
            return Task.CompletedTask;
        });
        var watchdog = new ReconnectWatchdog(
            resetTimeout: TimeSpan.FromSeconds(1),
            // Only cancellation ends the wait, so no check can reach the state check.
            delayAsync: (_, token) => Task.Delay(Timeout.InfiniteTimeSpan, token),
            failFast: () => Interlocked.Increment(ref failFastCalls),
            logger: NullLogger.Instance);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var eventTasks = Enumerable.Range(0, 200).Select(index => Task.Run(async () =>
        {
            await start.Task;
            if (index == 100)
            {
                watchdog.Dispose();
                return;
            }

            await watchdog.ClientOnConnected();
            await watchdog.ClientOnDisconnected(client.Object);
        })).ToArray();
        start.SetResult();
        var exception = await Record.ExceptionAsync(() => Task.WhenAll(eventTasks));

        Assert.Null(exception);
        Assert.Equal(0, startCalls);
        Assert.Equal(0, failFastCalls);
    }

    [Fact]
    public async Task Dispose_while_polling_stops_the_check_without_restarting_or_failing_fast()
    {
        var delayCalls = 0;
        var startCalls = 0;
        var failFastCalls = 0;
        var pollingStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = CreateClient(ConnectionState.Connecting, () =>
        {
            startCalls++;
            return Task.CompletedTask;
        });
        var watchdog = new ReconnectWatchdog(
            resetTimeout: TimeSpan.FromSeconds(5),
            delayAsync: (_, token) =>
            {
                if (++delayCalls == 1)
                {
                    return Task.CompletedTask;
                }

                pollingStarted.TrySetResult();
                return Task.Delay(Timeout.InfiniteTimeSpan, token);
            },
            failFast: () => failFastCalls++,
            logger: NullLogger.Instance);

        var disconnectedTask = watchdog.ClientOnDisconnected(client.Object);
        await pollingStarted.Task;
        watchdog.Dispose();
        await disconnectedTask;

        Assert.Equal(0, startCalls);
        Assert.Equal(0, failFastCalls);
    }

    [Fact]
    public async Task ClientOnConnected_after_Dispose_does_not_throw()
    {
        var watchdog = CreateWatchdog();
        watchdog.Dispose();

        var exception = await Record.ExceptionAsync(watchdog.ClientOnConnected);

        Assert.Null(exception);
    }

    [Fact]
    public void Arm_after_Dispose_throws()
    {
        var watchdog = CreateWatchdog();
        watchdog.Dispose();

        Assert.Throws<ObjectDisposedException>(watchdog.Arm);
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
            failFast: () => failFastCalls++,
            logger: NullLogger.Instance);

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
            failFast: () => failFastCalls++,
            logger: NullLogger.Instance);

        await watchdog.CheckStateAsync(client.Object);

        Assert.Equal(0, startCalls);
        Assert.Equal(1, failFastCalls);
    }

    [Fact]
    public async Task CheckStateAsync_logs_critical_before_failing_fast()
    {
        var logger = new ListLogger();
        var levelsLoggedBeforeFailFast = new List<LogLevel>();
        var client = CreateClient(ConnectionState.Connecting, () => Task.CompletedTask);
        var watchdog = new ReconnectWatchdog(
            resetTimeout: TimeSpan.FromSeconds(1),
            delayAsync: (_, _) => Task.CompletedTask,
            failFast: () => levelsLoggedBeforeFailFast.AddRange(logger.Entries.Select(entry => entry.Level)),
            logger: logger);

        await watchdog.CheckStateAsync(client.Object);

        Assert.Equal([LogLevel.Warning, LogLevel.Critical], levelsLoggedBeforeFailFast);
        Assert.Equal("Client stuck reconnecting. Killing process.", logger.Entries[^1].Message);
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
            delayAsync: (_, _) =>
            {
                state = ConnectionState.Connected;
                return Task.CompletedTask;
            },
            failFast: () => failFastCalls++,
            logger: NullLogger.Instance);

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
            delayAsync: (_, _) =>
            {
                if (state == ConnectionState.Connecting)
                {
                    state = ConnectionState.Disconnected;
                }

                return Task.CompletedTask;
            },
            failFast: () => failFastCalls++,
            logger: NullLogger.Instance);

        await watchdog.CheckStateAsync(client.Object);

        Assert.Equal(1, startCalls);
        Assert.Equal(0, failFastCalls);
    }

    [Fact]
    public async Task CheckStateAsync_starts_client_and_succeeds_when_it_reconnects()
    {
        var startCalls = 0;
        var state = ConnectionState.Disconnected;
        var client = CreateClient(() => state, () =>
        {
            startCalls++;
            state = ConnectionState.Connecting;
            return Task.CompletedTask;
        });
        // Discord.Net reaches Connected on the poll after the restart is dispatched.
        var failFastCalls = 0;
        var watchdog = new ReconnectWatchdog(
            resetTimeout: TimeSpan.FromSeconds(5),
            delayAsync: (_, _) =>
            {
                if (state == ConnectionState.Connecting)
                {
                    state = ConnectionState.Connected;
                }

                return Task.CompletedTask;
            },
            failFast: () => failFastCalls++,
            logger: NullLogger.Instance);

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
            failFast: () => failFastCalls++,
            logger: NullLogger.Instance);

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
            delayAsync: (_, _) => Task.Delay(Timeout.InfiniteTimeSpan, CancellationToken.None),
            failFast: () => failFastCalls++,
            logger: NullLogger.Instance);

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
            delayAsync: (_, _) => Task.Delay(Timeout.InfiniteTimeSpan, CancellationToken.None),
            failFast: () => failFastCalls++,
            logger: NullLogger.Instance);

        await watchdog.CheckStateAsync(client.Object);

        Assert.Equal(1, failFastCalls);
    }

    [Fact]
    public async Task CheckStateAsync_terminates_immediately_when_restart_faults_while_waiting_for_connected()
    {
        var delayCalls = 0;
        var terminateMessages = new List<string>();
        var logger = new ListLogger();
        var connectFaults = new TaskCompletionSource();

        await ReconnectStateChecker.CheckStateAsync(
            () => ConnectionState.Disconnected,
            () => connectFaults.Task,
            (_, _) =>
            {
                delayCalls++;
                connectFaults.TrySetException(new InvalidOperationException("boom"));
                return Task.CompletedTask;
            },
            TimeSpan.FromSeconds(5),
            logger,
            terminateMessages.Add,
            CancellationToken.None);

        Assert.Equal(1, delayCalls);
        Assert.Equal(["Client reset faulted."], terminateMessages);
        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Error
                && entry.Message == "Client reset faulted."
                && entry.Exception?.GetBaseException().Message == "boom");
    }

    [Fact]
    public async Task CheckStateAsync_terminates_immediately_when_restart_is_cancelled_while_waiting_for_connected()
    {
        var delayCalls = 0;
        var terminateMessages = new List<string>();
        var logger = new ListLogger();
        var connectCancelled = new TaskCompletionSource();

        await ReconnectStateChecker.CheckStateAsync(
            () => ConnectionState.Disconnected,
            () => connectCancelled.Task,
            (_, _) =>
            {
                delayCalls++;
                connectCancelled.TrySetCanceled(CancellationToken.None);
                return Task.CompletedTask;
            },
            TimeSpan.FromSeconds(5),
            logger,
            terminateMessages.Add,
            CancellationToken.None);

        Assert.Equal(1, delayCalls);
        Assert.Equal(["Client reset faulted."], terminateMessages);
        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Error && entry.Message == "Client reset was cancelled.");
    }

    [Fact]
    public async Task ClientOnConnected_cancels_and_disposes_the_previous_token_source()
    {
        var watchdog = CreateWatchdog();
        var previousToken = watchdog.GetReconnectCancellationToken();

        await watchdog.ClientOnConnected();

        Assert.True(previousToken.IsCancellationRequested);
        Assert.Throws<ObjectDisposedException>(() => previousToken.WaitHandle);
    }

    [Fact]
    public async Task CheckStateAsync_succeeds_when_restart_faults_but_client_is_already_connected()
    {
        var state = ConnectionState.Disconnected;
        var terminateMessages = new List<string>();
        var logger = new ListLogger();

        await ReconnectStateChecker.CheckStateAsync(
            () => state,
            () =>
            {
                state = ConnectionState.Connected;
                return Task.FromException(new InvalidOperationException("boom"));
            },
            (_, _) => Task.CompletedTask,
            TimeSpan.FromSeconds(5),
            logger,
            terminateMessages.Add,
            CancellationToken.None);

        Assert.Empty(terminateMessages);
        Assert.Contains(
            logger.Entries,
            entry => entry.Level == LogLevel.Information && entry.Message == "Client reset successfully.");
    }

    [Fact]
    public async Task CheckStateAsync_still_fails_fast_via_timeout_when_restart_faults_after_the_timeout()
    {
        // The timeout elapses before the restart task faults, so the backstop fires
        // through the timeout path and the late fault must not matter.
        var failFastCalls = 0;
        var connectFaults = new TaskCompletionSource();
        var client = CreateClient(ConnectionState.Disconnected, () => connectFaults.Task);
        var watchdog = new ReconnectWatchdog(
            resetTimeout: TimeSpan.Zero,
            delayAsync: (_, _) => Task.CompletedTask,
            failFast: () => failFastCalls++,
            logger: NullLogger.Instance);

        await watchdog.CheckStateAsync(client.Object);

        Assert.Equal(1, failFastCalls);

        connectFaults.SetException(new InvalidOperationException("boom, but too late to matter"));
    }

    private static ReconnectWatchdog CreateWatchdog() =>
        new(
            resetTimeout: TimeSpan.FromSeconds(1),
            delayAsync: (_, _) => Task.CompletedTask,
            failFast: () => { },
            logger: NullLogger.Instance);

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
