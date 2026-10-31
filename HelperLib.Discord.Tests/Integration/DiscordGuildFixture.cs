using Discord;
using Discord.WebSocket;
using HelperLib.Discord.DependencyInjection;
using HelperLib.Discord.Util;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HelperLib.Discord.Tests.Integration;

/// <summary>
/// Logs a bot in to a real guild once for all integration tests. It never starts the reconnect watchdog,
/// because the watchdog ends the process when the connection is lost.
/// </summary>
public sealed class DiscordGuildFixture : IAsyncLifetime
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(60);

    // The properties below are set by InitializeAsync. Without a token it returns early and every integration test is skipped.
    public DiscordSocketClient Client { get; private set; } = null!;

    public SocketGuild Guild { get; private set; } = null!;

    public GuildLookup Lookup { get; private set; } = null!;

    internal ListLogger<GuildLookup> Logger { get; } = new();

    public async Task InitializeAsync()
    {
        var token = IntegrationSettings.Token;
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        var client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMembers | GatewayIntents.GuildEmojis,
            AlwaysDownloadUsers = true,
        });

        try
        {
            var guildId = IntegrationSettings.GuildId;
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            client.Ready += () =>
            {
                ready.TrySetResult();
                return Task.CompletedTask;
            };

            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();
            await WaitForReadyAsync(ready.Task);

            var guild = client.GetGuild(guildId)
                ?? throw new InvalidOperationException(
                    $"The bot is not a member of guild {guildId}. Invite it, or set HELPERLIB_DISCORD_GUILD_ID to a guild it is in.");
            await DownloadMembersAsync(guild);

            Lookup = CreateLookup(client);
            Guild = guild;
            Client = client;
        }
        catch
        {
            await ShutDownAsync(client);
            throw;
        }
    }

    public async Task DisposeAsync()
    {
        if (Client is not null)
        {
            await ShutDownAsync(Client);
        }
    }

    private static async Task ShutDownAsync(DiscordSocketClient client)
    {
        // Each step runs even when the one before it throws.
        try
        {
            await client.StopAsync();
        }
        finally
        {
            try
            {
                if (client.LoginState == LoginState.LoggedIn)
                {
                    await client.LogoutAsync();
                }
            }
            finally
            {
                await client.DisposeAsync();
            }
        }
    }

    private static async Task WaitForReadyAsync(Task ready)
    {
        try
        {
            await ready.WaitAsync(ConnectTimeout);
        }
        catch (TimeoutException exception)
        {
            throw new InvalidOperationException(
                $"The client was not ready after {ConnectTimeout.TotalSeconds} s. Check that the token is valid and that the network reaches Discord.",
                exception);
        }
    }

    private static async Task DownloadMembersAsync(SocketGuild guild)
    {
        try
        {
            await guild.DownloadUsersAsync().WaitAsync(ConnectTimeout);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"The guild members were not downloaded within {ConnectTimeout.TotalSeconds} s. Enable the Server Members privileged intent for the bot in the Discord Developer Portal.",
                exception);
        }
    }

    private GuildLookup CreateLookup(DiscordSocketClient client)
    {
        var services = new ServiceCollection()
            .AddSingleton(client)
            .AddSingleton<ILogger<GuildLookup>>(Logger)
            .AddHelperLibDiscord(options => options.DefaultGuildId = IntegrationSettings.GuildId);

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<GuildLookup>();
    }
}
