using Microsoft.Extensions.Options;

namespace HelperLib.Discord.DependencyInjection;

/// <summary>Rejects timeouts that the watchdog cannot use.</summary>
internal sealed class HelperLibDiscordOptionsValidator : IValidateOptions<HelperLibDiscordOptions>
{
    // Task.Delay and ITimer accept at most uint.MaxValue - 1 milliseconds, which is about 49.7 days.
    private static readonly TimeSpan MaxTimeout = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

    public ValidateOptionsResult Validate(string? name, HelperLibDiscordOptions options)
    {
        List<string> failures = [];
        ValidateTimeout(nameof(options.ReconnectTimeout), options.ReconnectTimeout, failures);
        ValidateTimeout(nameof(options.ShutdownGracePeriod), options.ShutdownGracePeriod, failures);
        ValidateTimeout(nameof(options.HeartbeatTimeout), options.HeartbeatTimeout, failures);

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateTimeout(string optionName, TimeSpan value, List<string> failures)
    {
        if (value <= TimeSpan.Zero || value > MaxTimeout)
        {
            failures.Add($"{optionName} must be positive and at most 49.7 days.");
        }
    }
}
