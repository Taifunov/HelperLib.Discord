using Discord;
using Discord.WebSocket;
using HelperLib.Discord.Models;
using HelperLib.Discord.Util;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace HelperLib.Discord.Tests;

public class GuildLookupTests : IDisposable
{
    private static readonly Dictionary<string, Func<GuildLookup, ulong?, bool>> Lookups = new()
    {
        [nameof(GuildLookup.GetGuild)] = (lookup, guildId) => lookup.GetGuild(guildId, out _),
        [nameof(GuildLookup.GetTextChannel)] = (lookup, guildId) => lookup.GetTextChannel(guildId, 456, out _),
        [nameof(GuildLookup.GetVoiceChannel)] = (lookup, guildId) => lookup.GetVoiceChannel(guildId, 456, out _),
        [nameof(GuildLookup.GetCategory)] = (lookup, guildId) => lookup.GetCategory(guildId, 456, out _),
        [nameof(GuildLookup.GetRole)] = (lookup, guildId) => lookup.GetRole(guildId, 456, out _),
        [nameof(GuildLookup.GetEmote)] = (lookup, guildId) => lookup.GetEmote(guildId, "wave", out _),
        [nameof(GuildLookup.GetGuildUserById)] = (lookup, guildId) => lookup.GetGuildUserById(guildId, 456, out _),
        [nameof(GuildLookup.GetGuildOwner)] = (lookup, guildId) => lookup.GetGuildOwner(guildId, out _),
        [nameof(GuildLookup.GetGuildAdmins)] = (lookup, guildId) => lookup.GetGuildAdmins(guildId, out _),
    };

    // The same delegates the lookups pass to TrySelect, with the message each one must log for guild 123.
    private static readonly Dictionary<string, (Action<ILogger> LogMissingTarget, string Message)> MissingTargetLogs = new()
    {
        ["text channel"] = (logger => logger.NoSuchChannel("text channel", 456, 123), "No such text channel 456 on guild 123"),
        ["voice channel"] = (logger => logger.NoSuchChannel("voice channel", 456, 123), "No such voice channel 456 on guild 123"),
        ["category"] = (logger => logger.NoSuchCategory(456), "No such category 456"),
        ["role"] = (logger => logger.NoSuchRole(456, 123), "No such role 456 on guild 123"),
        ["emote"] = (logger => logger.NoSuchEmote("wave", 123), "No such emote wave on guild 123"),
        ["guild user"] = (logger => logger.GuildUserNotFound(456), "Failed to get SocketGuildUser 456"),
        ["owner"] = (logger => logger.GuildOwnerNotFound(), "Failed to get SocketGuildOwner"),
    };

    private readonly DiscordSocketClient _client = new(new DiscordSocketConfig());
    private readonly ListLogger<GuildLookup> _logger = new();

    public static TheoryData<string> LookupNames => NamesOf(Lookups.Keys);

    public static TheoryData<string> MissingTargetKinds => NamesOf(MissingTargetLogs.Keys);

    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }

    [Theory]
    [MemberData(nameof(LookupNames))]
    public void Lookup_returns_false_and_logs_only_the_unknown_guild_when_the_guild_is_unknown(string lookupName)
    {
        var found = Lookups[lookupName](CreateLookup(), 123);

        Assert.False(found);
        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("No such guild 123", entry.Message);
    }

    [Theory]
    [MemberData(nameof(LookupNames))]
    public void Lookup_returns_false_and_reports_missing_id_when_no_guild_id_and_no_default(string lookupName)
    {
        var found = Lookups[lookupName](CreateLookup(), null);

        Assert.False(found);
        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("No guild id given and no default guild id set!", entry.Message);
    }

    [Fact]
    public void GetGuild_uses_the_default_guild_id_when_no_guild_id_is_given()
    {
        var lookup = CreateLookup(defaultGuildId: 789);

        var found = lookup.GetGuild(null, out _);

        Assert.False(found);
        Assert.Equal("No such guild 789", Assert.Single(_logger.Entries).Message);
    }

    [Fact]
    public async Task GetGuildUserAsync_returns_null_and_reports_missing_id_when_no_guild_id_and_no_default()
    {
        var lookup = CreateLookup();

        var user = await lookup.GetGuildUserAsync(null, 456, forceRefresh: true);

        Assert.Null(user);
        Assert.Equal("No guild id given and no default guild id set!", Assert.Single(_logger.Entries).Message);
    }

    [Fact]
    public async Task FetchGuildUserAsync_returns_the_fetched_user()
    {
        var expected = new Mock<IGuildUser>().Object;

        var user = await GuildLookup.FetchGuildUserAsync(
            NullLogger.Instance,
            () => Task.FromResult<IGuildUser?>(expected),
            456,
            CancellationToken.None);

        Assert.Same(expected, user);
    }

    [Fact]
    public async Task FetchGuildUserAsync_returns_null_and_logs_the_exception_when_fetch_throws()
    {
        var logger = new ListLogger();
        var user = await GuildLookup.FetchGuildUserAsync(
            logger,
            () => Task.FromException<IGuildUser?>(new InvalidOperationException("boom")),
            456,
            CancellationToken.None);

        Assert.Null(user);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal("Failed to fetch user 456 from API", entry.Message);
        Assert.Equal("boom", entry.Exception?.Message);
    }

    [Fact]
    public async Task FetchGuildUserAsync_returns_null_when_request_times_out_without_caller_cancellation()
    {
        var user = await GuildLookup.FetchGuildUserAsync(
            NullLogger.Instance,
            () => Task.FromException<IGuildUser?>(new TaskCanceledException("HTTP timeout")),
            456,
            CancellationToken.None);

        Assert.Null(user);
    }

    [Fact]
    public async Task FetchGuildUserAsync_propagates_cancellation_requested_by_the_caller()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => GuildLookup.FetchGuildUserAsync(
            NullLogger.Instance,
            () => Task.FromException<IGuildUser?>(new OperationCanceledException(cancellation.Token)),
            456,
            cancellation.Token));
    }

    [Fact]
    public async Task GetGuildUsersByIdAsync_returns_unsuccessful_result_when_guild_is_missing()
    {
        GuildUsersResult result = await CreateLookup().GetGuildUsersByIdAsync(123);

        Assert.False(result.Success);
        Assert.Empty(result.Users);
    }

    [Fact]
    public void TrySelect_returns_the_found_target_without_logging()
    {
        var expected = new object();

        var selected = GuildLookup.TrySelect(_logger, true, () => expected, _ => Assert.Fail("Nothing is missing."), out var target);

        Assert.True(selected);
        Assert.Same(expected, target);
        Assert.Empty(_logger.Entries);
    }

    [Theory]
    [MemberData(nameof(MissingTargetKinds))]
    public void TrySelect_returns_false_and_logs_the_lookup_message_when_the_target_is_missing(string kind)
    {
        var (logMissingTarget, message) = MissingTargetLogs[kind];

        var selected = GuildLookup.TrySelect<object>(_logger, true, () => null, logMissingTarget, out var target);

        Assert.False(selected);
        Assert.Null(target);
        var entry = Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(message, entry.Message);
    }

    [Fact]
    public void TrySelect_returns_false_without_searching_or_logging_when_the_guild_is_missing()
    {
        var selected = GuildLookup.TrySelect<object>(
            _logger,
            false,
            () => throw new InvalidOperationException("The search must not run."),
            _ => Assert.Fail("The missing guild is logged by the guild lookup."),
            out var target);

        Assert.False(selected);
        Assert.Null(target);
        Assert.Empty(_logger.Entries);
    }

    [Fact]
    public void SelectAdmins_keeps_only_administrators_that_are_not_bots()
    {
        var admin = CreateGuildUser(isBot: false, administrator: true);
        var guildUsers = new[]
        {
            CreateGuildUser(isBot: false, administrator: false),
            CreateGuildUser(isBot: true, administrator: true),
            admin,
            CreateGuildUser(isBot: true, administrator: false),
        };

        var admins = GuildLookup.SelectAdmins(guildUsers);

        Assert.Same(admin, Assert.Single(admins));
    }

    [Fact]
    public void SelectAdmins_returns_nothing_when_there_are_no_users()
    {
        Assert.Empty(GuildLookup.SelectAdmins([]));
    }

    [Theory]
    [InlineData("wave")]
    [InlineData("WAVE")]
    public void FindEmoteByName_finds_the_emote_ignoring_case(string requestedName)
    {
        var wave = CreateEmote("wave");
        var emotes = new[] { CreateEmote("smile"), wave };

        var found = GuildLookup.FindEmoteByName(emotes, requestedName);

        Assert.Same(wave, found);
    }

    [Fact]
    public void FindEmoteByName_returns_null_when_no_emote_has_the_name()
    {
        var found = GuildLookup.FindEmoteByName(new[] { CreateEmote("smile") }, "wave");

        Assert.Null(found);
    }

    private static TheoryData<string> NamesOf(IEnumerable<string> names) => [.. names];

    private static IGuildUser CreateGuildUser(bool isBot, bool administrator)
    {
        var guildUser = new Mock<IGuildUser>();
        guildUser.SetupGet(user => user.IsBot).Returns(isBot);
        guildUser.SetupGet(user => user.GuildPermissions).Returns(new GuildPermissions(administrator: administrator));
        return guildUser.Object;
    }

    private static IEmote CreateEmote(string name)
    {
        var emote = new Mock<IEmote>();
        emote.SetupGet(mock => mock.Name).Returns(name);
        return emote.Object;
    }

    private GuildLookup CreateLookup(ulong? defaultGuildId = null) =>
        new(_client, Options.Create(new HelperLibDiscordOptions { DefaultGuildId = defaultGuildId }), _logger);
}
