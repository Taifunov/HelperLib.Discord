using HelperLib.Discord.Watchdog;

namespace HelperLib.Discord.Tests.Watchdog;

public class HeartbeatMonitorTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);

    private readonly SteppingTimeProvider _time = new();
    private int _timeouts;
    private bool _isConnected = true;

    [Fact]
    public void A_stale_callback_queued_before_a_Beat_does_not_time_out()
    {
        using var monitor = CreateArmedMonitor();
        _time.Advance(Timeout - TimeSpan.FromSeconds(1));
        monitor.Beat();
        _time.Advance(TimeSpan.FromSeconds(1));

        _time.RaiseLastTimerCallback();

        Assert.Equal(0, _timeouts);
    }

    [Fact]
    public void A_Beat_before_the_expiry_moves_the_timeout_a_full_period_later()
    {
        using var monitor = CreateArmedMonitor();
        _time.Advance(Timeout - TimeSpan.FromSeconds(1));
        monitor.Beat();
        _time.RaiseLastTimerCallback();

        _time.Advance(Timeout - TimeSpan.FromSeconds(1));
        Assert.Equal(0, _timeouts);

        _time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(1, _timeouts);
    }

    [Fact]
    public void Expiry_without_a_Beat_times_out_once()
    {
        using var monitor = CreateArmedMonitor();

        _time.Advance(Timeout);

        Assert.Equal(1, _timeouts);
    }

    [Fact]
    public void A_callback_queued_before_Pause_does_not_time_out()
    {
        using var monitor = CreateArmedMonitor();
        _time.Advance(Timeout);
        var timeoutsBeforePause = _timeouts;
        monitor.Pause();

        _time.RaiseLastTimerCallback();

        Assert.Equal(timeoutsBeforePause, _timeouts);
    }

    [Fact]
    public void A_stale_callback_after_Pause_and_Arm_does_not_time_out()
    {
        using var monitor = CreateArmedMonitor();
        _time.Advance(Timeout - TimeSpan.FromSeconds(1));
        monitor.Pause();
        monitor.Arm();
        _time.Advance(TimeSpan.FromSeconds(1));

        _time.RaiseLastTimerCallback();

        Assert.Equal(0, _timeouts);
    }

    [Fact]
    public void Expiry_while_the_client_is_not_connected_does_not_time_out()
    {
        using var monitor = CreateArmedMonitor();
        _isConnected = false;

        _time.Advance(Timeout);

        Assert.Equal(0, _timeouts);
    }

    [Fact]
    public void One_expiry_times_out_once_when_the_callback_runs_twice()
    {
        using var monitor = CreateArmedMonitor();
        _time.Advance(Timeout);

        _time.RaiseLastTimerCallback();

        Assert.Equal(1, _timeouts);
    }

    private HeartbeatMonitor CreateArmedMonitor()
    {
        var monitor = new HeartbeatMonitor(
            _time,
            Timeout,
            () => _isConnected,
            () => Interlocked.Increment(ref _timeouts));
        monitor.Arm();
        return monitor;
    }
}
