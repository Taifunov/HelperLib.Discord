using Microsoft.Extensions.Logging;
using Moq;

namespace HelperLib.Discord.Tests.Watchdog;

public class ProcessTerminatorTests
{
    private static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(30);

    [Fact]
    public void Terminate_logs_critical_with_the_reason_before_asking_the_host_to_stop()
    {
        using var harness = new WatchdogHarness();
        IReadOnlyList<LogEntry>? entriesWhenStopped = null;
        harness.Lifetime
            .Setup(lifetime => lifetime.StopApplication())
            .Callback(() => entriesWhenStopped = harness.TerminatorLogger.Entries);

        harness.Terminator.Terminate("Client stuck reconnecting.");

        entriesWhenStopped!.AssertSingleEntry(LogLevel.Critical, "Client stuck reconnecting. Killing process.");
    }

    [Fact]
    public void Terminate_sets_the_failure_exit_code_and_stops_the_application_without_exiting_yet()
    {
        using var harness = new WatchdogHarness();

        harness.Terminator.Terminate("reason");

        Assert.Equal([1], harness.ExitCodesSet);
        harness.VerifyProcessTerminationRequested(Times.Once());
        Assert.Empty(harness.HardExitCodes);
    }

    [Fact]
    public async Task Terminate_ends_the_process_only_after_the_grace_period()
    {
        using var harness = new WatchdogHarness();
        harness.Terminator.Terminate("reason");

        harness.Time.Advance(GracePeriod - TimeSpan.FromSeconds(1));
        Assert.Empty(harness.HardExitCodes);

        harness.Time.Advance(TimeSpan.FromSeconds(1));
        await harness.HardExit.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal([1], harness.HardExitCodes);
    }

    [Fact]
    public void Terminate_arms_the_hard_exit_before_asking_the_host_to_stop()
    {
        using var harness = new WatchdogHarness();
        var timersBeforeTerminate = harness.Time.TimersCreated;
        long timersWhenStopped = 0;
        harness.Lifetime
            .Setup(lifetime => lifetime.StopApplication())
            .Callback(() => timersWhenStopped = harness.Time.TimersCreated);

        harness.Terminator.Terminate("reason");

        Assert.Equal(timersBeforeTerminate + 1, timersWhenStopped);
    }

    [Fact]
    public async Task Terminate_still_ends_the_process_after_the_grace_period_when_stopping_the_host_throws()
    {
        using var harness = new WatchdogHarness();
        harness.Lifetime
            .Setup(lifetime => lifetime.StopApplication())
            .Throws(new InvalidOperationException("host is broken"));

        harness.Terminator.Terminate("reason");
        harness.Time.Advance(GracePeriod);
        await harness.HardExit.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal([1], harness.HardExitCodes);
        Assert.Contains(
            harness.TerminatorLogger.Entries,
            entry => entry.Level == LogLevel.Error && entry.Exception?.Message == "host is broken");
    }

    [Fact]
    public void Terminate_ends_the_process_at_once_when_the_grace_period_wait_fails()
    {
        var invalidGracePeriod = new HelperLibDiscordOptions { ShutdownGracePeriod = TimeSpan.FromSeconds(-5) };
        using var harness = new WatchdogHarness(options: invalidGracePeriod);

        harness.Terminator.Terminate("reason");

        Assert.Equal([1], harness.HardExitCodes);
        Assert.True(harness.TerminatorLogger.Entries.HasEntry(
            LogLevel.Error,
            "Waiting for the shutdown grace period failed; ending the process now."));
    }

    [Fact]
    public async Task Terminate_called_again_does_nothing()
    {
        using var harness = new WatchdogHarness();
        harness.Terminator.Terminate("first");

        harness.Terminator.Terminate("second");
        harness.Time.Advance(GracePeriod);
        await harness.HardExit.Task.WaitAsync(TimeSpan.FromSeconds(5));

        harness.VerifyProcessTerminationRequested(Times.Once());
        Assert.Equal([1], harness.ExitCodesSet);
        Assert.Equal([1], harness.HardExitCodes);
        Assert.Single(harness.TerminatorLogger.Entries);
    }

    [Fact]
    public async Task Terminate_from_many_threads_runs_once()
    {
        using var harness = new WatchdogHarness();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var terminations = Enumerable.Range(0, 100).Select(index => Task.Run(async () =>
        {
            await start.Task;
            harness.Terminator.Terminate($"reason {index}");
        })).ToArray();
        start.SetResult();
        await Task.WhenAll(terminations);
        harness.Time.Advance(GracePeriod);
        await harness.HardExit.Task.WaitAsync(TimeSpan.FromSeconds(5));

        harness.VerifyProcessTerminationRequested(Times.Once());
        Assert.Equal([1], harness.HardExitCodes);
    }
}
