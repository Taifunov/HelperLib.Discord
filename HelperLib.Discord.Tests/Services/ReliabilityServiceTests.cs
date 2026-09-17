using global::Discord;
using HelperLib.Discord.Services;

namespace HelperLib.Discord.Tests.Services;

public class ReliabilityServiceTests
{
    [Fact]
    public async Task CheckStateAsync_returns_without_starting_client_when_already_connected()
    {
        var startCalls = 0;
        var service = new ReliabilityService(
            getConnectionState: () => ConnectionState.Connected,
            startAsync: () =>
            {
                startCalls++;
                return Task.CompletedTask;
            },
            delayWithCancellationAsync: (_, _) => Task.CompletedTask,
            delayAsync: _ => Task.CompletedTask,
            writeLine: _ => { },
            terminate: _ => { },
            timeout: TimeSpan.Zero);

        await service.CheckStateAsync();

        Assert.Equal(0, startCalls);
    }

    [Fact]
    public async Task CheckStateAsync_terminates_when_client_stays_connecting()
    {
        var startCalls = 0;
        var terminationMessages = new List<string>();
        var service = new ReliabilityService(
            getConnectionState: () => ConnectionState.Connecting,
            startAsync: () =>
            {
                startCalls++;
                return Task.CompletedTask;
            },
            delayWithCancellationAsync: (_, _) => Task.CompletedTask,
            delayAsync: _ => Task.CompletedTask,
            writeLine: _ => { },
            terminate: terminationMessages.Add,
            timeout: TimeSpan.FromSeconds(1));

        await service.CheckStateAsync();

        Assert.Equal(0, startCalls);
        Assert.Equal(["Client stuck reconnecting."], terminationMessages);
    }

    [Fact]
    public async Task CheckStateAsync_terminates_when_client_stays_disconnecting()
    {
        var startCalls = 0;
        var terminationMessages = new List<string>();
        var service = new ReliabilityService(
            getConnectionState: () => ConnectionState.Disconnecting,
            startAsync: () =>
            {
                startCalls++;
                return Task.CompletedTask;
            },
            delayWithCancellationAsync: (_, _) => Task.CompletedTask,
            delayAsync: _ => Task.CompletedTask,
            writeLine: _ => { },
            terminate: terminationMessages.Add,
            timeout: TimeSpan.FromSeconds(1));

        await service.CheckStateAsync();

        Assert.Equal(0, startCalls);
        Assert.Equal(["Client stuck reconnecting."], terminationMessages);
    }

    [Fact]
    public async Task CheckStateAsync_recovers_without_restart_when_connecting_settles_to_connected()
    {
        var startCalls = 0;
        var state = ConnectionState.Connecting;
        var terminationMessages = new List<string>();
        var service = new ReliabilityService(
            getConnectionState: () => state,
            startAsync: () =>
            {
                startCalls++;
                return Task.CompletedTask;
            },
            delayWithCancellationAsync: (_, _) => Task.CompletedTask,
            delayAsync: _ =>
            {
                state = ConnectionState.Connected;
                return Task.CompletedTask;
            },
            writeLine: _ => { },
            terminate: terminationMessages.Add,
            timeout: TimeSpan.FromSeconds(5));

        await service.CheckStateAsync();

        Assert.Equal(0, startCalls);
        Assert.Empty(terminationMessages);
    }

    [Fact]
    public async Task CheckStateAsync_restarts_when_connecting_settles_to_disconnected()
    {
        var startCalls = 0;
        var state = ConnectionState.Connecting;
        var terminationMessages = new List<string>();
        var service = new ReliabilityService(
            getConnectionState: () => state,
            startAsync: () =>
            {
                startCalls++;
                state = ConnectionState.Connected;
                return Task.CompletedTask;
            },
            delayWithCancellationAsync: (_, _) => Task.CompletedTask,
            delayAsync: _ =>
            {
                if (state == ConnectionState.Connecting)
                {
                    state = ConnectionState.Disconnected;
                }

                return Task.CompletedTask;
            },
            writeLine: _ => { },
            terminate: terminationMessages.Add,
            timeout: TimeSpan.FromSeconds(5));

        await service.CheckStateAsync();

        Assert.Equal(1, startCalls);
        Assert.Empty(terminationMessages);
    }

    [Fact]
    public async Task CheckStateAsync_starts_client_and_succeeds_when_it_reconnects()
    {
        var startCalls = 0;
        var stateCalls = 0;
        var states = new[]
        {
            ConnectionState.Disconnected,
            ConnectionState.Disconnected,
            ConnectionState.Connecting,
            ConnectionState.Connected,
        };
        var terminationMessages = new List<string>();
        var service = new ReliabilityService(
            getConnectionState: () => states[Math.Min(stateCalls++, states.Length - 1)],
            startAsync: () =>
            {
                startCalls++;
                return Task.CompletedTask;
            },
            delayWithCancellationAsync: (_, _) => Task.CompletedTask,
            delayAsync: _ => Task.CompletedTask,
            writeLine: _ => { },
            terminate: terminationMessages.Add,
            timeout: TimeSpan.FromSeconds(5));

        await service.CheckStateAsync();

        Assert.Equal(1, startCalls);
        Assert.Empty(terminationMessages);
    }

    [Fact]
    public async Task CheckStateAsync_terminates_when_client_never_reconnects()
    {
        var terminationMessages = new List<string>();
        var service = new ReliabilityService(
            getConnectionState: () => ConnectionState.Disconnected,
            startAsync: () => Task.CompletedTask,
            delayWithCancellationAsync: (_, _) => Task.CompletedTask,
            delayAsync: _ => Task.CompletedTask,
            writeLine: _ => { },
            terminate: terminationMessages.Add,
            timeout: TimeSpan.FromSeconds(1));

        await service.CheckStateAsync();

        Assert.Equal(["Client reset timed out."], terminationMessages);
    }

    [Fact]
    public async Task CheckStateAsync_terminates_when_restart_faults()
    {
        var terminationMessages = new List<string>();
        var service = new ReliabilityService(
            getConnectionState: () => ConnectionState.Disconnected,
            startAsync: () => Task.FromException(new InvalidOperationException("boom")),
            delayWithCancellationAsync: (_, _) => Task.Delay(Timeout.InfiniteTimeSpan),
            delayAsync: _ => Task.Delay(Timeout.InfiniteTimeSpan),
            writeLine: _ => { },
            terminate: terminationMessages.Add,
            timeout: TimeSpan.FromSeconds(1));

        await service.CheckStateAsync();

        Assert.Equal(["Client reset faulted."], terminationMessages);
    }

    [Fact]
    public async Task CheckStateAsync_terminates_when_start_async_throws_synchronously()
    {
        var terminationMessages = new List<string>();
        var service = new ReliabilityService(
            getConnectionState: () => ConnectionState.Disconnected,
            startAsync: () => throw new InvalidOperationException("boom"),
            delayWithCancellationAsync: (_, _) => Task.Delay(Timeout.InfiniteTimeSpan),
            delayAsync: _ => Task.Delay(Timeout.InfiniteTimeSpan),
            writeLine: _ => { },
            terminate: terminationMessages.Add,
            timeout: TimeSpan.FromSeconds(1));

        await service.CheckStateAsync();

        Assert.Equal(["Client reset faulted."], terminationMessages);
    }

    [Fact]
    public async Task CheckStateAsync_still_terminates_via_timeout_when_restart_faults_asynchronously()
    {
        // IsFaulted is only checked synchronously right after startAsync() returns.
        // A real Discord.Net client that fails some time after StartAsync() is called
        // (its normal failure mode) is not caught by that check, and falls through to
        // WaitForConnectedAsync instead, timing out. The backstop still fires (terminate
        // is still called), just via the timeout path rather than the fault path.
        var terminationMessages = new List<string>();
        var connectFaults = new TaskCompletionSource();
        var service = new ReliabilityService(
            getConnectionState: () => ConnectionState.Disconnected,
            startAsync: () => connectFaults.Task,
            delayWithCancellationAsync: (_, _) => Task.CompletedTask,
            delayAsync: _ => Task.CompletedTask,
            writeLine: _ => { },
            terminate: terminationMessages.Add,
            timeout: TimeSpan.Zero);

        await service.CheckStateAsync();

        Assert.Equal(["Client reset timed out."], terminationMessages);

        connectFaults.SetException(new InvalidOperationException("boom, but too late to matter"));
    }

    [Fact]
    public async Task StartTimeoutTask_checks_state_after_delay_when_not_reset()
    {
        var startCalls = 0;
        var service = new ReliabilityService(
            getConnectionState: () => ConnectionState.Disconnected,
            startAsync: () =>
            {
                startCalls++;
                return Task.CompletedTask;
            },
            delayWithCancellationAsync: (_, _) => Task.CompletedTask,
            delayAsync: _ => Task.Delay(Timeout.InfiniteTimeSpan),
            writeLine: _ => { },
            terminate: _ => { },
            timeout: TimeSpan.Zero);

        await service.StartTimeoutTask(new Exception("disconnect"));

        Assert.Equal(1, startCalls);
    }

    [Fact]
    public async Task ResetCancellationTokens_cancels_pending_timeout_check()
    {
        var capturedToken = CancellationToken.None;
        var checkStarted = false;
        var delayReleased = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var service = new ReliabilityService(
            getConnectionState: () => ConnectionState.Disconnected,
            startAsync: () =>
            {
                checkStarted = true;
                return Task.CompletedTask;
            },
            delayWithCancellationAsync: (_, token) =>
            {
                capturedToken = token;
                return delayReleased.Task;
            },
            delayAsync: _ => Task.Delay(Timeout.InfiniteTimeSpan),
            writeLine: _ => { },
            terminate: _ => { },
            timeout: TimeSpan.FromSeconds(1));

        var timeoutTask = service.StartTimeoutTask(new Exception("disconnect"));
        await service.ResetCancellationTokens();
        delayReleased.SetResult();
        await timeoutTask;

        Assert.True(capturedToken.IsCancellationRequested);
        Assert.False(checkStarted);
    }
}
