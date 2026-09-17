using global::Discord;
using HelperLib.Discord.Models;
using HelperLib.Discord.Util;
using Moq;

namespace HelperLib.Discord.Tests.Util;

public class GuildLookupTests
{
    [Fact]
    public void GetGuild_returns_false_when_client_is_not_initialized()
    {
        var found = GuildLookup.GetGuild(123, out var guild);

        Assert.False(found);
        Assert.Null(guild);
    }

    [Fact]
    public void GetTextChannel_returns_false_when_guild_is_missing()
    {
        var found = GuildLookup.GetTextChannel(123, 456, out var channel);

        Assert.False(found);
        Assert.Null(channel);
    }

    [Fact]
    public void GetVoiceChannel_returns_false_when_guild_is_missing()
    {
        var found = GuildLookup.GetVoiceChannel(123, 456, out var channel);

        Assert.False(found);
        Assert.Null(channel);
    }

    [Fact]
    public void GetCategory_returns_false_when_guild_is_missing()
    {
        var found = GuildLookup.GetCategory(123, 456, out var category);

        Assert.False(found);
        Assert.Null(category);
    }

    [Fact]
    public void GetRole_returns_false_when_guild_is_missing()
    {
        var found = GuildLookup.GetRole(123, 456, out var role);

        Assert.False(found);
        Assert.Null(role);
    }

    [Fact]
    public void GetEmote_returns_false_when_guild_is_missing()
    {
        var found = GuildLookup.GetEmote(123, "wave", out var emote);

        Assert.False(found);
        Assert.Null(emote);
    }

    [Fact]
    public void GetGuildUserById_returns_false_when_guild_is_missing()
    {
        var found = GuildLookup.GetGuildUserById(123, 456, out var user);

        Assert.False(found);
        Assert.Null(user);
    }

    [Fact]
    public async Task GetGuildUserAsync_returns_null_when_client_is_not_initialized()
    {
        var user = await GuildLookup.GetGuildUserAsync(123, 456);

        Assert.Null(user);
    }

    [Fact]
    public void GetGuildOwner_returns_false_when_guild_is_missing()
    {
        var found = GuildLookup.GetGuildOwner(123, out var owner);

        Assert.False(found);
        Assert.Null(owner);
    }

    [Fact]
    public void GetGuildAdmins_returns_false_when_guild_is_missing()
    {
        var found = GuildLookup.GetGuildAdmins(123, out var users);

        Assert.False(found);
        Assert.Null(users);
    }

    [Fact]
    public async Task GetGuildUsersByIdAsync_returns_unsuccessful_result_when_guild_is_missing()
    {
        GuildUsersResult result = await GuildLookup.GetGuildUsersByIdAsync(123);

        Assert.False(result.Success);
        Assert.Empty(result.Users);
    }

    [Fact]
    public void TryGetChannel_returns_false_when_guild_is_missing()
    {
        var found = GuildLookup.TryGetChannel(false, new object(), "text channel", 1, 2, out object? channel);

        Assert.False(found);
        Assert.Null(channel);
    }

    [Fact]
    public void TryGetChannel_returns_false_when_channel_is_missing()
    {
        var found = GuildLookup.TryGetChannel<object>(true, null, "text channel", 1, 2, out var channel);

        Assert.False(found);
        Assert.Null(channel);
    }

    [Fact]
    public void TryGetChannel_returns_true_when_channel_exists()
    {
        var expected = new object();

        var found = GuildLookup.TryGetChannel(true, expected, "text channel", 1, 2, out var channel);

        Assert.True(found);
        Assert.Same(expected, channel);
    }

    [Fact]
    public void TryGetCategory_returns_false_when_categories_are_missing()
    {
        var found = GuildLookup.TryGetCategory<object>(
            true,
            null,
            _ => 0,
            1,
            out var category);

        Assert.False(found);
        Assert.Null(category);
    }

    [Fact]
    public void TryGetCategory_returns_false_when_category_id_is_not_found()
    {
        var found = GuildLookup.TryGetCategory(
            true,
            [new LookupItem(10)],
            item => item.Id,
            20,
            out var category);

        Assert.False(found);
        Assert.Null(category);
    }

    [Fact]
    public void TryGetCategory_returns_true_when_category_id_matches()
    {
        var expected = new LookupItem(20);

        var found = GuildLookup.TryGetCategory(
            true,
            [new LookupItem(10), expected],
            item => item.Id,
            20,
            out var category);

        Assert.True(found);
        Assert.Same(expected, category);
    }

    [Fact]
    public void TryGetRole_returns_false_when_role_is_missing()
    {
        var found = GuildLookup.TryGetRole<IRole>(true, null, 1, 2, out var role);

        Assert.False(found);
        Assert.Null(role);
    }

    [Fact]
    public void TryGetRole_returns_true_when_role_exists()
    {
        var expected = new Mock<IRole>().Object;

        var found = GuildLookup.TryGetRole(true, expected, 1, 2, out var role);

        Assert.True(found);
        Assert.Same(expected, role);
    }

    [Fact]
    public void TryGetEmote_matches_name_ignoring_case()
    {
        var expected = new LookupItem(1, "Wave");

        var found = GuildLookup.TryGetEmote(
            true,
            [expected],
            item => item.Name,
            123,
            "wave",
            out var emote);

        Assert.True(found);
        Assert.Same(expected, emote);
    }

    [Fact]
    public void TryGetGuildOwner_returns_false_when_owner_is_missing()
    {
        var found = GuildLookup.TryGetGuildOwner<IGuildUser>(true, null, out var owner);

        Assert.False(found);
        Assert.Null(owner);
    }

    [Fact]
    public void TryGetGuildOwner_returns_true_when_owner_exists()
    {
        var expected = new Mock<IGuildUser>().Object;

        var found = GuildLookup.TryGetGuildOwner(true, expected, out var owner);

        Assert.True(found);
        Assert.Same(expected, owner);
    }

    [Fact]
    public void GetGuildAdmins_filters_out_bots_and_non_administrators()
    {
        var admin = CreateGuildUser(isBot: false, administrator: true);
        var botAdmin = CreateGuildUser(isBot: true, administrator: true);
        var regularUser = CreateGuildUser(isBot: false, administrator: false);

        var found = GuildLookup.TryGetGuildAdmins(
            true,
            [admin, botAdmin, regularUser],
            out var admins);

        Assert.True(found);
        Assert.Equal([admin], admins);
    }

    private static IGuildUser CreateGuildUser(bool isBot, bool administrator)
    {
        var user = new Mock<IGuildUser>();
        user.Setup(u => u.IsBot).Returns(isBot);
        user.Setup(u => u.GuildPermissions).Returns(
            administrator ? GuildPermissions.All : GuildPermissions.None);
        return user.Object;
    }

    private sealed record LookupItem(ulong Id, string Name = "");
}
