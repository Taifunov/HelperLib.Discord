using Microsoft.Extensions.Configuration;

namespace HelperLib.Discord.Tests.Integration;

/// <summary>
/// The settings of the integration tests, read once from environment variables and then from the user secrets.
/// </summary>
internal static class IntegrationSettings
{
    private const string TokenEnvironmentVariable = "HELPERLIB_DISCORD_TOKEN";
    private const string TokenUserSecretsKey = "HelperLibDiscord:Token";
    private const string GuildIdEnvironmentVariable = "HELPERLIB_DISCORD_GUILD_ID";
    private const string GuildIdUserSecretsKey = "HelperLibDiscord:GuildId";

    // A public guild id, not a secret.
    private const ulong DefaultGuildId = 872032510962446346;

    // The configuration is built while xUnit discovers the tests, so a failure here must not break the discovery.
    private static readonly (IConfigurationRoot? Root, string? Error) Loaded = Load();

    /// <summary>Why the configuration could not be read; <see langword="null"/> when it was read. It never contains the token.</summary>
    public static string? ConfigurationError => Loaded.Error;

    /// <summary>The bot token; <see langword="null"/> when none is configured, which skips the integration tests.</summary>
    public static string? Token => Read(TokenEnvironmentVariable, TokenUserSecretsKey);

    public static ulong GuildId
    {
        get
        {
            var value = Read(GuildIdEnvironmentVariable, GuildIdUserSecretsKey);
            if (string.IsNullOrWhiteSpace(value))
            {
                return DefaultGuildId;
            }

            return ulong.TryParse(value, out var guildId)
                ? guildId
                : throw new InvalidOperationException(
                    $"The guild id setting ({GuildIdEnvironmentVariable} or {GuildIdUserSecretsKey}) is not a valid guild id. Use the numeric id of the guild.");
        }
    }

    private static (IConfigurationRoot? Root, string? Error) Load()
    {
        try
        {
            var root = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .AddUserSecrets(typeof(IntegrationSettings).Assembly)
                .Build();
            return (root, null);
        }
        catch (Exception exception) when (exception is FormatException or InvalidDataException)
        {
            return (null, $"The integration test configuration cannot be read: {exception.GetBaseException().Message}");
        }
    }

    private static string? Read(string environmentVariableName, string userSecretsKey)
    {
        var value = Loaded.Root?[environmentVariableName];
        return string.IsNullOrWhiteSpace(value) ? Loaded.Root?[userSecretsKey] : value;
    }
}
