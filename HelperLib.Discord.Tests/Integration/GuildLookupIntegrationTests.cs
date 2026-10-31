using System.Diagnostics.CodeAnalysis;
using Discord.WebSocket;
using HelperLib.Discord.Util;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace HelperLib.Discord.Tests.Integration;

/// <summary>
/// Runs <see cref="GuildLookup"/> against a real guild. These cover the Discord.Net lookups that unit tests cannot,
/// because <see cref="SocketGuild"/> is sealed and has no public constructor. The ids come from the guild itself.
/// </summary>
[Collection(DiscordGuildCollection.Name)]
public sealed class GuildLookupIntegrationTests
{
    private const ulong MissingId = 1;
    private const string MissingEmoteName = "no-such-emote";

    private readonly DiscordGuildFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly int _entriesBeforeTest;

    public GuildLookupIntegrationTests(DiscordGuildFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _entriesBeforeTest = fixture.Logger.Entries.Count;
    }

    private static ulong GuildId => IntegrationSettings.GuildId;

    private GuildLookup Lookup => _fixture.Lookup;

    private SocketGuild Guild => _fixture.Guild;

    private ulong BotUserId => _fixture.Client.CurrentUser.Id;

    private IReadOnlyList<LogEntry> NewEntries => [.. _fixture.Logger.Entries.Skip(_entriesBeforeTest)];

    [IntegrationFact]
    public void GetGuild_WithoutGuildId_ReturnsDefaultGuild()
    {
        Assert.True(Lookup.GetGuild(null, out var guild));
        Assert.Equal(GuildId, guild.Id);
    }

    [IntegrationFact]
    public void GetGuild_WithGuildId_ReturnsGuild()
    {
        Assert.True(Lookup.GetGuild(GuildId, out var guild));
        Assert.Equal(GuildId, guild.Id);
    }

    [IntegrationFact]
    public void GetGuild_UnknownGuild_ReturnsFalseAndLogsWarning()
    {
        Assert.False(Lookup.GetGuild(MissingId, out _));

        NewEntries.AssertSingleEntry(LogLevel.Warning, $"No such guild {MissingId}");
    }

    [IntegrationFact]
    public void GetTextChannel_FirstTextChannel_ReturnsChannel()
    {
        if (!TryGetFirst(Guild.TextChannels, "text channels", out var expectedChannel))
        {
            return;
        }

        Assert.True(Lookup.GetTextChannel(GuildId, expectedChannel.Id, out var channel));
        Assert.Equal(expectedChannel.Id, channel.Id);
    }

    [IntegrationFact]
    public void GetTextChannel_UnknownChannel_ReturnsFalseAndLogsWarning()
    {
        Assert.False(Lookup.GetTextChannel(GuildId, MissingId, out _));

        NewEntries.AssertSingleEntry(LogLevel.Warning, $"No such text channel {MissingId} on guild {GuildId}");
    }

    [IntegrationFact]
    public void GetVoiceChannel_FirstVoiceChannel_ReturnsChannel()
    {
        if (!TryGetFirst(Guild.VoiceChannels, "voice channels", out var expectedChannel))
        {
            return;
        }

        Assert.True(Lookup.GetVoiceChannel(GuildId, expectedChannel.Id, out var channel));
        Assert.Equal(expectedChannel.Id, channel.Id);
    }

    [IntegrationFact]
    public void GetVoiceChannel_UnknownChannel_ReturnsFalseAndLogsWarning()
    {
        Assert.False(Lookup.GetVoiceChannel(GuildId, MissingId, out _));

        NewEntries.AssertSingleEntry(LogLevel.Warning, $"No such voice channel {MissingId} on guild {GuildId}");
    }

    [IntegrationFact]
    public void GetCategory_FirstCategory_ReturnsCategory()
    {
        if (!TryGetFirst(Guild.CategoryChannels, "categories", out var expectedCategory))
        {
            return;
        }

        Assert.True(Lookup.GetCategory(GuildId, expectedCategory.Id, out var category));
        Assert.Equal(expectedCategory.Id, category.Id);
    }

    [IntegrationFact]
    public void GetCategory_UnknownCategory_ReturnsFalseAndLogsWarning()
    {
        Assert.False(Lookup.GetCategory(GuildId, MissingId, out _));

        NewEntries.AssertSingleEntry(LogLevel.Warning, $"No such category {MissingId}");
    }

    [IntegrationFact]
    public void GetRole_EveryoneRole_ReturnsRole()
    {
        var everyoneRoleId = Guild.EveryoneRole.Id;

        Assert.True(Lookup.GetRole(GuildId, everyoneRoleId, out var role));
        Assert.Equal(everyoneRoleId, role.Id);
    }

    [IntegrationFact]
    public void GetRole_UnknownRole_ReturnsFalseAndLogsWarning()
    {
        Assert.False(Lookup.GetRole(GuildId, MissingId, out _));

        NewEntries.AssertSingleEntry(LogLevel.Warning, $"No such role {MissingId} on guild {GuildId}");
    }

    [IntegrationFact]
    public void GetEmote_FirstEmote_ReturnsEmote()
    {
        if (!TryGetFirst(Guild.Emotes, "emotes", out var expectedEmote))
        {
            return;
        }

        Assert.True(Lookup.GetEmote(GuildId, expectedEmote.Name, out var emote));
        Assert.Equal(expectedEmote.Id, emote.Id);
    }

    [IntegrationFact]
    public void GetEmote_NameInDifferentCase_ReturnsEmote()
    {
        if (!TryGetFirst(Guild.Emotes, "emotes", out var expectedEmote))
        {
            return;
        }

        Assert.True(Lookup.GetEmote(GuildId, expectedEmote.Name.ToUpperInvariant(), out var emote));
        Assert.Equal(expectedEmote.Id, emote.Id);
    }

    [IntegrationFact]
    public void GetEmote_UnknownEmote_ReturnsFalseAndLogsWarning()
    {
        Assert.False(Lookup.GetEmote(GuildId, MissingEmoteName, out _));

        NewEntries.AssertSingleEntry(LogLevel.Warning, $"No such emote {MissingEmoteName} on guild {GuildId}");
    }

    [IntegrationFact]
    public void GetGuildUserById_BotUser_ReturnsUser()
    {
        Assert.True(Lookup.GetGuildUserById(GuildId, BotUserId, out var user));
        Assert.Equal(BotUserId, user.Id);
    }

    [IntegrationFact]
    public void GetGuildUserById_UnknownUser_ReturnsFalseAndLogsWarning()
    {
        Assert.False(Lookup.GetGuildUserById(GuildId, MissingId, out _));

        NewEntries.AssertSingleEntry(LogLevel.Warning, $"Failed to get SocketGuildUser {MissingId}");
    }

    [IntegrationFact]
    public async Task GetGuildUserAsync_ForceRefresh_FetchesBotUserFromRestApi()
    {
        var user = await Lookup.GetGuildUserAsync(GuildId, BotUserId, forceRefresh: true);

        Assert.NotNull(user);
        Assert.Equal(BotUserId, user.Id);
    }

    [IntegrationFact]
    public async Task GetGuildUserAsync_UnknownUser_ReturnsNullAndLogsWarning()
    {
        var user = await Lookup.GetGuildUserAsync(GuildId, MissingId, forceRefresh: true);

        Assert.Null(user);
        NewEntries.AssertSingleEntry(LogLevel.Warning, $"Failed to get user {MissingId} from Discord API");
    }

    [IntegrationFact]
    public void GetGuildOwner_ReturnsGuildOwner()
    {
        Assert.True(Lookup.GetGuildOwner(GuildId, out var owner));
        Assert.Equal(Guild.OwnerId, owner.Id);
    }

    [IntegrationFact]
    public void GetGuildAdmins_ReturnsOnlyAdministratorsThatAreNotBots()
    {
        Assert.True(Lookup.GetGuildAdmins(GuildId, out var admins));

        Assert.All(admins, admin =>
        {
            Assert.False(admin.IsBot);
            Assert.True(admin.GuildPermissions.Administrator);
        });
    }

    [IntegrationFact]
    public async Task GetGuildUsersByIdAsync_ReturnsMembersIncludingBotUser()
    {
        var result = await Lookup.GetGuildUsersByIdAsync(GuildId);

        Assert.True(result.Success);
        Assert.Contains(result.Users, user => user.Id == BotUserId);
    }

    [IntegrationFact]
    public void HasRolesAny_BotUserAndEveryoneRole_ReturnsTrue()
    {
        Assert.True(Lookup.GetGuildUserById(GuildId, BotUserId, out var botUser));

        Assert.True(RoleChecks.HasRolesAny(botUser, Guild.EveryoneRole.Id));
    }

    // A guild may lack channels or emotes; the lookup is then not exercised, and the test does not fail.
    private bool TryGetFirst<TItem>(IEnumerable<TItem> items, string itemKind, [NotNullWhen(true)] out TItem? first)
        where TItem : class
    {
        first = items.FirstOrDefault();
        if (first is null)
        {
            _output.WriteLine($"The guild has no {itemKind}, so this lookup was not exercised.");
            return false;
        }

        return true;
    }
}
