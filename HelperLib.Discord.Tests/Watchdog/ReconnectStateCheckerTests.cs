using Discord;
using HelperLib.Discord.Util;
using Microsoft.Extensions.Logging;

namespace HelperLib.Discord.Tests.Watchdog;

public class ReconnectStateCheckerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly SteppingTimeProvider _time = new();
    private readonly ListLogger _logger = new();
    private readonly List<string> _terminations = [];
    private int _startCalls;
    private ConnectionState _state;

    [Fact]
    public async Task Returns_when_client_is_connected()
    {
        _state = ConnectionState.Connected;

        await CheckAsync(StartCompleted);

        Assert.Equal(0, _startCalls);
        Assert.Empty(_terminations);
    }

    [Theory]
    [InlineData(ConnectionState.Connecting)]
    [InlineData(ConnectionState.Disconnecting)]
    public async Task Terminates_without_restart_when_client_stays_in_a_transient_state(ConnectionState transientState)
    {
        _state = transientState;

        await CheckAsync(StartCompleted);

        Assert.Equal(0, _startCalls);
        Assert.Equal(["Client stuck reconnecting."], _terminations);
    }

    [Fact]
    public async Task Waits_out_a_transient_state_for_the_whole_timeout_before_terminating()
    {
        _state = ConnectionState.Connecting;

        var check = StartCheck(StartCompleted);
        await _time.AdvanceAsync(Timeout - TimeSpan.FromSeconds(1), check);

        Assert.False(check.IsCompleted);
        Assert.Empty(_terminations);

        await _time.AdvanceUntilCompletedAsync(check, TimeSpan.FromSeconds(3));
        Assert.Equal(["Client stuck reconnecting."], _terminations);
    }

    [Fact]
    public async Task Measures_the_timeout_on_the_clock_instead_of_counting_polls()
    {
        _state = ConnectionState.Connecting;

        var check = StartCheck(StartCompleted);
        _time.Advance(Timeout);
        await WatchdogHarness.WaitUntilAsync(() => check.IsCompleted);

        Assert.Equal(0, _startCalls);
        Assert.Equal(["Client stuck reconnecting."], _terminations);
    }

    [Fact]
    public async Task Recovers_without_restart_when_connecting_settles_to_connected()
    {
        _state = ConnectionState.Connecting;

        var check = StartCheck(StartCompleted);
        await _time.AdvanceAsync(TimeSpan.FromSeconds(2), check);
        _state = ConnectionState.Connected;
        await _time.AdvanceUntilCompletedAsync(check, TimeSpan.FromSeconds(2));

        Assert.Equal(0, _startCalls);
        Assert.Empty(_terminations);
    }

    [Fact]
    public async Task Restarts_when_connecting_settles_to_disconnected()
    {
        _state = ConnectionState.Connecting;

        var check = StartCheck(ConnectOnStart);
        await _time.AdvanceAsync(TimeSpan.FromSeconds(2), check);
        _state = ConnectionState.Disconnected;
        await _time.AdvanceUntilCompletedAsync(check, TimeSpan.FromSeconds(3));

        Assert.Equal(1, _startCalls);
        Assert.Empty(_terminations);
    }

    [Fact]
    public async Task Starts_client_and_succeeds_when_it_reconnects()
    {
        _state = ConnectionState.Disconnected;

        var check = StartCheck(() =>
        {
            _state = ConnectionState.Connecting;
            return Task.CompletedTask;
        });
        await _time.AdvanceAsync(TimeSpan.FromSeconds(2), check);
        _state = ConnectionState.Connected;
        await _time.AdvanceUntilCompletedAsync(check, TimeSpan.FromSeconds(2));

        Assert.Equal(1, _startCalls);
        Assert.Empty(_terminations);
        Assert.True(_logger.Entries.HasEntry(LogLevel.Information, "Client reset successfully."));
    }

    [Fact]
    public async Task Terminates_when_client_never_reconnects()
    {
        _state = ConnectionState.Disconnected;

        await CheckAsync(StartCompleted);

        Assert.Equal(1, _startCalls);
        Assert.Equal(["Client reset timed out."], _terminations);
    }

    [Fact]
    public async Task Terminates_when_restart_faults()
    {
        _state = ConnectionState.Disconnected;

        await CheckAsync(() => Task.FromException(new InvalidOperationException("boom")));

        Assert.Equal(["Client reset faulted."], _terminations);
    }

    [Fact]
    public async Task Terminates_when_start_async_throws_synchronously()
    {
        _state = ConnectionState.Disconnected;

        await CheckAsync(() => throw new InvalidOperationException("boom"));

        Assert.Equal(["Client reset faulted."], _terminations);
        Assert.Contains(
            _logger.Entries,
            entry => entry.Level == LogLevel.Error && entry.Exception?.Message == "boom");
    }

    [Fact]
    public async Task Terminates_as_soon_as_restart_faults_while_waiting_for_connected()
    {
        _state = ConnectionState.Disconnected;
        var connectFaults = new TaskCompletionSource();

        var check = StartCheck(() => connectFaults.Task);
        connectFaults.SetException(new InvalidOperationException("boom"));
        await _time.AdvanceUntilCompletedAsync(check, TimeSpan.FromSeconds(1));

        Assert.Equal(["Client reset faulted."], _terminations);
        Assert.Contains(
            _logger.Entries,
            entry => entry.Level == LogLevel.Error
                && entry.Message == "Client reset faulted."
                && entry.Exception?.GetBaseException().Message == "boom");
    }

    [Fact]
    public async Task Terminates_as_soon_as_restart_is_cancelled_while_waiting_for_connected()
    {
        _state = ConnectionState.Disconnected;
        var connectCancelled = new TaskCompletionSource();

        var check = StartCheck(() => connectCancelled.Task);
        connectCancelled.SetCanceled(CancellationToken.None);
        await _time.AdvanceUntilCompletedAsync(check, TimeSpan.FromSeconds(1));

        Assert.Equal(["Client reset faulted."], _terminations);
        Assert.True(_logger.Entries.HasEntry(LogLevel.Error, "Client reset was cancelled."));
    }

    [Fact]
    public async Task Succeeds_when_restart_faults_but_client_is_already_connected()
    {
        _state = ConnectionState.Disconnected;

        await CheckAsync(() =>
        {
            _state = ConnectionState.Connected;
            return Task.FromException(new InvalidOperationException("boom"));
        });

        Assert.Empty(_terminations);
        Assert.True(_logger.Entries.HasEntry(LogLevel.Information, "Client reset successfully."));
    }

    [Fact]
    public async Task Terminates_through_the_timeout_when_restart_faults_after_the_timeout()
    {
        _state = ConnectionState.Disconnected;
        var connectFaults = new TaskCompletionSource();

        await CheckAsync(() => connectFaults.Task);
        connectFaults.SetException(new InvalidOperationException("boom, but too late to matter"));

        Assert.Equal(["Client reset timed out."], _terminations);
    }

    [Fact]
    public async Task Returns_quietly_when_cancelled_while_waiting_for_a_transient_state()
    {
        _state = ConnectionState.Connecting;
        using var cancellation = new CancellationTokenSource();

        var check = StartCheck(StartCompleted, cancellation.Token);
        await _time.AdvanceAsync(TimeSpan.FromSeconds(2), check);
        await cancellation.CancelAsync();
        await check;

        Assert.Equal(0, _startCalls);
        Assert.Empty(_terminations);
    }

    private static Task StartCompleted() => Task.CompletedTask;

    private Task ConnectOnStart()
    {
        _state = ConnectionState.Connected;
        return Task.CompletedTask;
    }

    private Task CheckAsync(Func<Task> startAsync) =>
        _time.AdvanceUntilCompletedAsync(StartCheck(startAsync), Timeout + TimeSpan.FromSeconds(1));

    private Task StartCheck(Func<Task> startAsync, CancellationToken cancellationToken = default) =>
        ReconnectStateChecker.CheckStateAsync(
            () => _state,
            () =>
            {
                _startCalls++;
                return startAsync();
            },
            _time,
            Timeout,
            _logger,
            _terminations.Add,
            cancellationToken);
}
