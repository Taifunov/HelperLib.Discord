using HelperLib.Discord.Util;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HelperLib.Discord.Watchdog;

/// <summary>
/// Ends the process once. It asks the host to stop gracefully, then ends the process forcibly
/// when the host is still running after <see cref="HelperLibDiscordOptions.ShutdownGracePeriod"/>.
/// A failure of the graceful stop is logged and does not prevent the forced exit.
/// </summary>
internal sealed class ProcessTerminator(
    IHostApplicationLifetime applicationLifetime,
    TimeProvider timeProvider,
    IOptions<HelperLibDiscordOptions> options,
    ProcessExit processExit,
    ILogger<ProcessTerminator> logger)
{
    private const int FailureExitCode = 1;

    private int _terminationStarted;

    /// <summary>Starts the shutdown. Only the first call does anything.</summary>
    /// <param name="reason">Why the process ends; it goes into the Critical log entry.</param>
    internal void Terminate(string reason)
    {
        if (Interlocked.Exchange(ref _terminationStarted, 1) == 1)
        {
            return;
        }

        // The hard exit is armed first: its delay registers synchronously, so a failure below cannot prevent it.
        _ = ExitAfterGracePeriodAsync();
        logger.KillingProcess(reason);
        RequestGracefulStop();
    }

    private void RequestGracefulStop()
    {
        try
        {
            processExit.SetExitCode(FailureExitCode);
            applicationLifetime.StopApplication();
        }
        catch (Exception exception)
        {
            logger.GracefulStopFailed(exception);
        }
    }

    private async Task ExitAfterGracePeriodAsync()
    {
        try
        {
            // The wait must outlive the host's stopping token, which is the very shutdown it backstops.
            await Task.Delay(options.Value.ShutdownGracePeriod, timeProvider).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.GracePeriodWaitFailed(exception);
        }

        processExit.Exit(FailureExitCode);
    }
}
