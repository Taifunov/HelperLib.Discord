using Microsoft.Extensions.Time.Testing;

namespace HelperLib.Discord.Tests.Watchdog;

/// <summary>
/// A fake clock that counts the timers it creates and fires. <c>Task.Delay</c> completes its task when its
/// timer fires, but the awaiting code resumes later on another thread. Tests use the counts to wait until
/// that code has registered its next timer before they advance the clock again.
/// </summary>
internal sealed class SteppingTimeProvider : FakeTimeProvider
{
    private long _timersCreated;
    private long _timersFired;
    private TimerCallback? _lastTimerCallback;

    public long TimersCreated => Volatile.Read(ref _timersCreated);

    public long TimersFired => Volatile.Read(ref _timersFired);

    /// <summary>
    /// Runs the callback of the newest timer again, as a thread-pool thread that was queued before the timer
    /// was cancelled or replaced would.
    /// </summary>
    public void RaiseLastTimerCallback()
    {
        var callback = _lastTimerCallback
            ?? throw new InvalidOperationException("No timer has been created yet.");
        callback(null);
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        Interlocked.Increment(ref _timersCreated);
        _lastTimerCallback = callback;
        return base.CreateTimer(
            timerState =>
            {
                Interlocked.Increment(ref _timersFired);
                callback(timerState);
            },
            state,
            dueTime,
            period);
    }
}
