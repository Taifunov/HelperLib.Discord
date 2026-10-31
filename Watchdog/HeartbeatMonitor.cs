namespace HelperLib.Discord.Watchdog;

/// <summary>
/// A timer that expires when no heartbeat acknowledgement arrives within the timeout.
/// It runs only between <see cref="Arm"/> and <see cref="Pause"/>, and calls back only while the client is connected.
/// </summary>
internal sealed class HeartbeatMonitor : IDisposable
{
    private readonly Lock _gate = new();
    private readonly TimeProvider _timeProvider;
    private readonly ITimer _timer;
    private readonly TimeSpan _timeout;
    private readonly Func<bool> _isConnected;
    private readonly Action _onTimeout;
    private long _waitStartTimestamp;
    private bool _armed;
    private bool _disposed;

    internal HeartbeatMonitor(TimeProvider timeProvider, TimeSpan timeout, Func<bool> isConnected, Action onTimeout)
    {
        _timeProvider = timeProvider;
        _timeout = timeout;
        _isConnected = isConnected;
        _onTimeout = onTimeout;
        _timer = timeProvider.CreateTimer(OnTimerElapsed, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Starts waiting for a heartbeat acknowledgement.</summary>
    internal void Arm()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _armed = true;
            RestartWait();
        }
    }

    /// <summary>Restarts the wait after an acknowledgement. Does nothing while paused.</summary>
    internal void Beat()
    {
        lock (_gate)
        {
            if (_armed)
            {
                RestartWait();
            }
        }
    }

    /// <summary>Stops waiting, for example while the client is disconnected.</summary>
    internal void Pause()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _armed = false;
            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _armed = false;
        }

        _timer.Dispose();
    }

    private void RestartWait()
    {
        _waitStartTimestamp = _timeProvider.GetTimestamp();
        _timer.Change(_timeout, Timeout.InfiniteTimeSpan);
    }

    private void OnTimerElapsed(object? state)
    {
        // A callback already queued when the monitor paused or disposed must not fire.
        lock (_gate)
        {
            if (!_armed)
            {
                return;
            }

            // A Beat or Arm may have restarted the wait after this callback was queued, and a timer may fire
            // slightly early. Either way the wait is not over, so wait for the rest of it.
            var remaining = _timeout - _timeProvider.GetElapsedTime(_waitStartTimestamp);
            if (remaining > TimeSpan.Zero)
            {
                _timer.Change(remaining, Timeout.InfiniteTimeSpan);
                return;
            }

            // Disarming and checking the connection under the same lock as Arm and Pause means a callback
            // that outlives a disconnect and reconnect cannot end the new connection, and one expiry fires once.
            _armed = false;
            if (!_isConnected())
            {
                return;
            }
        }

        // Outside the lock, because the timeout action may run user callbacks.
        _onTimeout();
    }
}
