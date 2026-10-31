namespace HelperLib.Discord.Tests.Watchdog;

internal static class FakeTimeProviderExtensions
{
    private static readonly TimeSpan Step = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Advances time one second at a time while <paramref name="watched"/> polls the clock. After a step that
    /// fired a timer, it waits until the watched code has registered its next timer or has completed.
    /// </summary>
    public static async Task AdvanceAsync(this SteppingTimeProvider time, TimeSpan duration, Task watched)
    {
        for (var elapsed = TimeSpan.Zero; elapsed < duration; elapsed += Step)
        {
            await AdvanceStepAsync(time, watched);
        }
    }

    /// <summary>Advances time like <see cref="AdvanceAsync"/> until the task completes, and fails when it does not within <paramref name="limit"/>.</summary>
    public static async Task AdvanceUntilCompletedAsync(this SteppingTimeProvider time, Task task, TimeSpan limit)
    {
        for (var elapsed = TimeSpan.Zero; !task.IsCompleted; elapsed += Step)
        {
            Assert.True(elapsed < limit, $"The task did not complete within {limit} of fake time.");
            await AdvanceStepAsync(time, task);
        }

        await task;
    }

    private static async Task AdvanceStepAsync(SteppingTimeProvider time, Task watched)
    {
        var timersCreated = time.TimersCreated;
        var timersFired = time.TimersFired;

        time.Advance(Step);

        if (time.TimersFired == timersFired)
        {
            await Task.Yield();
            return;
        }

        await WatchdogHarness.WaitUntilAsync(
            () => watched.IsCompleted || time.TimersCreated > timersCreated,
            "The code under test neither registered its next timer nor completed after a timer fired.");
    }
}
