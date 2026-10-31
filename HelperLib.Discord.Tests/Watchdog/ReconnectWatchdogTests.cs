using Discord;
using Microsoft.Extensions.Logging;
using Moq;

namespace HelperLib.Discord.Tests.Watchdog;

public class ReconnectWatchdogTests
{
    private static readonly TimeSpan ReconnectTimeout = TimeSpan.FromMinutes(1);

    [Fact]
    public async Task ClientOnDisconnected_does_not_check_state_after_the_client_reconnects()
    {
        using var harness = new WatchdogHarness();

        var check = harness.Watchdog.ClientOnDisconnected();
        await harness.Watchdog.ClientOnConnected();
        await check;
        harness.Time.Advance(ReconnectTimeout);

        harness.AssertNoRestartOrTermination();
    }

    [Fact]
    public async Task ClientOnDisconnected_restarts_the_client_after_the_reconnect_timeout()
    {
        using var harness = new WatchdogHarness();
        harness.ConnectOnStart();

        var check = harness.Watchdog.ClientOnDisconnected();
        await harness.Time.AdvanceAsync(ReconnectTimeout - TimeSpan.FromSeconds(1), check);

        Assert.False(check.IsCompleted);
        Assert.Equal(0, harness.StartCalls);

        await harness.Time.AdvanceUntilCompletedAsync(check, TimeSpan.FromSeconds(5));

        Assert.Equal(1, harness.StartCalls);
        harness.VerifyProcessTerminationRequested(Times.Never());
        Assert.True(harness.Logger.Entries.HasEntry(LogLevel.Information, "State came back okay"));
    }

    [Fact]
    public async Task ClientOnDisconnected_while_a_check_is_running_is_dropped_and_logged()
    {
        using var harness = new WatchdogHarness();
        harness.ConnectOnStart();

        var firstCheck = harness.Watchdog.ClientOnDisconnected();
        await harness.Watchdog.ClientOnDisconnected();
        await harness.Time.AdvanceUntilCompletedAsync(firstCheck, ReconnectTimeout + TimeSpan.FromSeconds(5));

        Assert.Equal(1, harness.StartCalls);
        Assert.Single(
            harness.Logger.Entries,
            entry => entry.Message == "Client disconnected again while a reconnect check is running, ignoring the duplicate.");
    }

    [Fact]
    public async Task ClientOnDisconnected_after_Connected_starts_a_new_check_and_still_drops_duplicates_of_it()
    {
        using var harness = new WatchdogHarness();
        var firstCheck = harness.Watchdog.ClientOnDisconnected();
        await harness.Watchdog.ClientOnConnected();
        await firstCheck;

        var secondCheck = harness.Watchdog.ClientOnDisconnected();
        await harness.Watchdog.ClientOnDisconnected();

        Assert.False(secondCheck.IsCompleted);
        Assert.Single(harness.Logger.Entries, entry => entry.Message.StartsWith("Client disconnected again", StringComparison.Ordinal));
        Assert.Equal(2, harness.Logger.Entries.Count(entry => entry.Message == "Client disconnected, starting timeout task..."));
    }

    [Fact]
    public async Task ClientOnDisconnected_ends_the_process_when_the_client_stays_stuck_connecting()
    {
        using var harness = new WatchdogHarness(ConnectionState.Connecting);

        var check = harness.Watchdog.ClientOnDisconnected();
        await harness.Time.AdvanceUntilCompletedAsync(check, ReconnectTimeout + ReconnectTimeout + TimeSpan.FromSeconds(1));

        Assert.Equal(0, harness.StartCalls);
        harness.VerifyProcessTerminationRequested(Times.Once());
        harness.TerminatorLogger.Entries.AssertSingleEntry(LogLevel.Critical, "Client stuck reconnecting. Killing process.");
    }

    [Fact]
    public async Task Dispose_cancels_a_pending_reconnect_wait_without_checking_state()
    {
        using var harness = new WatchdogHarness();

        var check = harness.Watchdog.ClientOnDisconnected();
        harness.Watchdog.Dispose();
        await check;
        harness.Time.Advance(ReconnectTimeout);

        harness.AssertNoRestartOrTermination();
    }

    [Fact]
    public async Task Dispose_while_polling_stops_the_check_without_restarting_or_terminating()
    {
        using var harness = new WatchdogHarness(ConnectionState.Connecting);

        var check = harness.Watchdog.ClientOnDisconnected();
        await harness.Time.AdvanceAsync(ReconnectTimeout + TimeSpan.FromSeconds(3), check);
        Assert.False(check.IsCompleted);
        harness.Watchdog.Dispose();
        await check;
        harness.Time.Advance(ReconnectTimeout);

        harness.AssertNoRestartOrTermination();
    }

    [Fact]
    public async Task ClientOnDisconnected_after_Dispose_logs_nothing_and_starts_no_check()
    {
        using var harness = new WatchdogHarness();
        harness.Watchdog.Dispose();

        await harness.Watchdog.ClientOnDisconnected();
        harness.Time.Advance(ReconnectTimeout);

        Assert.Equal(0, harness.StartCalls);
        Assert.Empty(harness.Logger.Entries);
    }

    [Fact]
    public async Task ClientOnConnected_after_Dispose_does_not_throw_and_logs_nothing()
    {
        using var harness = new WatchdogHarness();
        harness.Watchdog.Dispose();

        var exception = await Record.ExceptionAsync(harness.Watchdog.ClientOnConnected);

        Assert.Null(exception);
        Assert.Empty(harness.Logger.Entries);
    }

    [Fact]
    public async Task StartAsync_subscribes_to_the_client_events_and_StopAsync_unsubscribes()
    {
        using var harness = new WatchdogHarness();
        Assert.Equal(0, harness.Events.SubscriberCount);

        await harness.StartAsync();
        Assert.Equal(3, harness.Events.SubscriberCount);

        await harness.Watchdog.StopAsync(CancellationToken.None);
        Assert.Equal(0, harness.Events.SubscriberCount);
    }

    [Fact]
    public async Task StartAsync_called_twice_subscribes_once()
    {
        using var harness = new WatchdogHarness();

        await harness.StartAsync();
        await harness.StartAsync();

        Assert.Equal(3, harness.Events.SubscriberCount);
    }

    [Fact]
    public async Task StartAsync_after_StopAsync_throws()
    {
        using var harness = new WatchdogHarness();
        await harness.Watchdog.StopAsync(CancellationToken.None);

        await Assert.ThrowsAsync<ObjectDisposedException>(harness.StartAsync);
    }

    [Fact]
    public async Task Disconnected_event_returns_to_the_gateway_at_once_and_the_check_runs_in_the_background()
    {
        using var harness = new WatchdogHarness();
        harness.ConnectOnStart();
        await harness.StartAsync();

        var handlerTask = harness.Events.RaiseDisconnected();

        Assert.True(handlerTask.IsCompletedSuccessfully);
        Assert.Equal(0, harness.StartCalls);

        harness.Time.Advance(ReconnectTimeout);
        await WatchdogHarness.WaitUntilAsync(() => harness.StartCalls == 1);
    }

    [Fact]
    public async Task Connected_event_cancels_the_check_started_by_the_Disconnected_event()
    {
        using var harness = new WatchdogHarness();
        await harness.StartAsync();
        await harness.Events.RaiseDisconnected();

        await harness.Events.RaiseConnected();
        harness.Time.Advance(ReconnectTimeout);

        harness.AssertNoRestartOrTermination();
    }

    [Fact]
    public async Task Disconnected_event_logs_a_failing_check_instead_of_throwing()
    {
        using var harness = new WatchdogHarness();
        await harness.StartAsync();
        harness.Client.Setup(client => client.ConnectionState).Throws(new InvalidOperationException("boom"));

        await harness.Events.RaiseDisconnected();
        harness.Time.Advance(ReconnectTimeout);

        await WatchdogHarness.WaitUntilAsync(() => harness.Logger.Entries.Any(entry => entry.Level == LogLevel.Error));
        var entry = Assert.Single(harness.Logger.Entries, entry => entry.Level == LogLevel.Error);
        Assert.Equal("Reconnect watchdog failed.", entry.Message);
        Assert.Equal("boom", entry.Exception?.Message);
    }

    [Fact]
    public async Task Overlapping_events_and_StopAsync_from_many_threads_never_throw_or_restart_the_client()
    {
        using var harness = new WatchdogHarness();
        await harness.StartAsync();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var eventTasks = Enumerable.Range(0, 200).Select(index => Task.Run(async () =>
        {
            await start.Task;
            switch (index % 4)
            {
                case 0:
                    await harness.Events.RaiseConnected();
                    break;
                case 1:
                    await harness.Events.RaiseDisconnected();
                    break;
                case 2:
                    await harness.Events.RaiseLatencyUpdated();
                    break;
                default:
                    await harness.Watchdog.ClientOnDisconnected();
                    break;
            }

            if (index == 100)
            {
                await harness.Watchdog.StopAsync(CancellationToken.None);
            }
        })).ToArray();
        start.SetResult();
        var exception = await Record.ExceptionAsync(() => Task.WhenAll(eventTasks));
        harness.Watchdog.Dispose();
        harness.Time.Advance(TimeSpan.FromMinutes(10));

        Assert.Null(exception);
        harness.AssertNoRestartOrTermination();
        Assert.DoesNotContain(harness.Logger.Entries, entry => entry.Level >= LogLevel.Error);
    }
}
