using Discord;
using Microsoft.Extensions.Logging;
using Moq;

namespace HelperLib.Discord.Tests.Watchdog;

public class ReconnectWatchdogHeartbeatTests
{
    private static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromMinutes(3);

    [Fact]
    public async Task Missing_heartbeat_acknowledgement_while_connected_terminates_the_process()
    {
        using var harness = new WatchdogHarness(ConnectionState.Connected);
        await harness.StartConnectedAsync();

        harness.Time.Advance(HeartbeatTimeout - TimeSpan.FromSeconds(1));
        harness.VerifyProcessTerminationRequested(Times.Never());

        harness.Time.Advance(TimeSpan.FromSeconds(1));

        harness.VerifyProcessTerminationRequested(Times.Once());
        Assert.True(harness.Logger.Entries.HasEntry(
            LogLevel.Warning,
            "No heartbeat acknowledgement for 00:03:00; terminating."));
        Assert.True(harness.TerminatorLogger.Entries.HasEntry(LogLevel.Critical, "Heartbeat lost. Killing process."));
    }

    [Fact]
    public async Task LatencyUpdated_before_the_timeout_restarts_the_wait()
    {
        using var harness = new WatchdogHarness(ConnectionState.Connected);
        await harness.StartConnectedAsync();

        harness.Time.Advance(TimeSpan.FromMinutes(2));
        await harness.Events.RaiseLatencyUpdated();
        harness.Time.Advance(TimeSpan.FromMinutes(2));
        harness.VerifyProcessTerminationRequested(Times.Never());

        harness.Time.Advance(TimeSpan.FromMinutes(1));
        harness.VerifyProcessTerminationRequested(Times.Once());
    }

    [Fact]
    public async Task Disconnected_pauses_the_heartbeat_wait_because_the_reconnect_check_owns_that_state()
    {
        // The client mock stays Connected, so only the pause can keep the heartbeat from terminating.
        using var harness = new WatchdogHarness(ConnectionState.Connected);
        await harness.StartConnectedAsync();

        await harness.Events.RaiseDisconnected();
        harness.Time.Advance(TimeSpan.FromMinutes(10));

        harness.VerifyProcessTerminationRequested(Times.Never());
    }

    [Fact]
    public async Task LatencyUpdated_while_disconnected_does_not_restart_the_heartbeat_wait()
    {
        using var harness = new WatchdogHarness(ConnectionState.Connected);
        await harness.StartConnectedAsync();
        await harness.Events.RaiseDisconnected();

        await harness.Events.RaiseLatencyUpdated();
        harness.Time.Advance(TimeSpan.FromMinutes(10));

        harness.VerifyProcessTerminationRequested(Times.Never());
    }

    [Fact]
    public async Task Connected_restarts_the_heartbeat_wait_after_a_disconnect()
    {
        using var harness = new WatchdogHarness(ConnectionState.Connected);
        await harness.StartAsync();
        await harness.Events.RaiseDisconnected();

        await harness.Events.RaiseConnected();
        harness.Time.Advance(HeartbeatTimeout);

        harness.VerifyProcessTerminationRequested(Times.Once());
    }

    [Fact]
    public async Task Heartbeat_timeout_does_nothing_when_the_client_is_no_longer_connected()
    {
        using var harness = new WatchdogHarness(ConnectionState.Connected);
        await harness.StartConnectedAsync();

        harness.State = ConnectionState.Connecting;
        harness.Time.Advance(HeartbeatTimeout);

        harness.VerifyProcessTerminationRequested(Times.Never());
    }

    [Fact]
    public async Task StartAsync_arms_the_heartbeat_wait_when_the_client_is_already_connected()
    {
        using var harness = new WatchdogHarness(ConnectionState.Connected);

        await harness.StartAsync();
        harness.Time.Advance(HeartbeatTimeout);

        harness.VerifyProcessTerminationRequested(Times.Once());
    }

    [Fact]
    public async Task StartAsync_does_not_arm_the_heartbeat_wait_before_the_client_connects()
    {
        using var harness = new WatchdogHarness();

        await harness.StartAsync();
        harness.Time.Advance(HeartbeatTimeout);

        harness.VerifyProcessTerminationRequested(Times.Never());
    }

    [Fact]
    public async Task StopAsync_cancels_the_heartbeat_wait()
    {
        using var harness = new WatchdogHarness(ConnectionState.Connected);
        await harness.StartConnectedAsync();

        await harness.Watchdog.StopAsync(CancellationToken.None);
        harness.Time.Advance(HeartbeatTimeout);

        harness.VerifyProcessTerminationRequested(Times.Never());
    }
}
