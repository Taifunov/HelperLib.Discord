namespace HelperLib.Discord.Watchdog;

/// <summary>
/// The two process-level effects of a termination. Tests replace this seam, because they must not
/// end the test process or leave a failing exit code behind.
/// </summary>
internal sealed record ProcessExit(Action<int> SetExitCode, Action<int> Exit)
{
    internal static ProcessExit Default { get; } = new(
        exitCode => Environment.ExitCode = exitCode,
        Environment.Exit);
}
