namespace HelperLib.Discord.Tests.Integration;

/// <summary>A test that talks to a real Discord guild; it is skipped when no bot token is configured.</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute()
    {
        if (IntegrationSettings.ConfigurationError is { } configurationError)
        {
            Skip = configurationError;
        }
        else if (string.IsNullOrWhiteSpace(IntegrationSettings.Token))
        {
            Skip = "No bot token. Set HELPERLIB_DISCORD_TOKEN or the user secret HelperLibDiscord:Token to run the integration tests.";
        }
    }
}
