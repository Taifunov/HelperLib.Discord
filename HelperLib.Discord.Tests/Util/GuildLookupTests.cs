using Discord;
using HelperLib.Discord.Models;
using HelperLib.Discord.Util;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HelperLib.Discord.Tests.Util;

[Collection(ConsoleAndStaticStateCollection.Name)]
public class GuildLookupTests : IDisposable
{
    // Tests swap GuildLookup's static logger for a ListLogger; restore it so tests stay order-independent.
    public void Dispose()
    {
        GuildLookup.SetLogger(ConsoleLogger.Instance);
        GuildLookup.SetDefaultGuildId(null);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void GetGuild_returns_false_when_client_is_not_initialized()
    {
        var found = GuildLookup.GetGuild(123, out var guild);

        Assert.False(found);
        Assert.Null(guild);
    }

    [Fact]
    public void GetGuild_returns_false_and_reports_missing_id_when_no_guild_id_and_no_default()
    {
        var logger = UseListLogger();
        GuildLookup.SetDefaultGuildId(null);

        var found = GuildLookup.GetGuild(null, out _);

        Assert.False(found);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("No guild id given and no default guild id set!", entry.Message);
    }

    [Fact]
    public void GetRole_returns_false_and_reports_missing_id_when_no_guild_id_and_no_default()
    {
        var logger = UseListLogger();
        GuildLookup.SetDefaultGuildId(null);

        var found = GuildLookup.GetRole(null, 456, out _);

        Assert.False(found);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("No guild id given and no default guild id set!", entry.Message);
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
    public async Task GetGuildUserAsync_returns_null_when_no_guild_id_and_no_default()
    {
        GuildLookup.SetDefaultGuildId(null);

        var user = await GuildLookup.GetGuildUserAsync(null, 456, forceRefresh: true);

        Assert.Null(user);
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
        var found = GuildLookup.TryGetChannel(NullLogger.Instance, false, new object(), "text channel", 1, 2, out object? channel);

        Assert.False(found);
        Assert.Null(channel);
    }

    [Fact]
    public void TryGetChannel_returns_false_when_channel_is_missing()
    {
        var found = GuildLookup.TryGetChannel<object>(NullLogger.Instance, true, null, "text channel", 1, 2, out var channel);

        Assert.False(found);
        Assert.Null(channel);
    }

    [Fact]
    public void TryGetChannel_returns_true_when_channel_exists()
    {
        var expected = new object();

        var found = GuildLookup.TryGetChannel(NullLogger.Instance, true, expected, "text channel", 1, 2, out var channel);

        Assert.True(found);
        Assert.Same(expected, channel);
    }

    [Fact]
    public void TryGetCategory_returns_false_when_categories_are_missing()
    {
        var found = GuildLookup.TryGetCategory<object>(
            NullLogger.Instance,
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
            NullLogger.Instance,
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
            NullLogger.Instance,
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
        var logger = new ListLogger();

        var found = GuildLookup.TryGetRole<IRole>(logger, true, null, 1, 2, out var role);

        Assert.False(found);
        Assert.Null(role);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("No such role 2 on guild 1", entry.Message);
    }

    [Fact]
    public void TryGetRole_returns_true_when_role_exists()
    {
        var expected = new Mock<IRole>().Object;

        var found = GuildLookup.TryGetRole(NullLogger.Instance, true, expected, 1, 2, out var role);

        Assert.True(found);
        Assert.Same(expected, role);
    }

    [Fact]
    public void TryGetEmote_matches_name_ignoring_case()
    {
        var expected = new LookupItem(1, "Wave");

        var found = GuildLookup.TryGetEmote(
            NullLogger.Instance,
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
        var found = GuildLookup.TryGetGuildOwner<IGuildUser>(NullLogger.Instance, true, null, out var owner);

        Assert.False(found);
        Assert.Null(owner);
    }

    [Fact]
    public void TryGetGuildOwner_returns_true_when_owner_exists()
    {
        var expected = new Mock<IGuildUser>().Object;

        var found = GuildLookup.TryGetGuildOwner(NullLogger.Instance, true, expected, out var owner);

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
            NullLogger.Instance,
            true,
            [admin, botAdmin, regularUser],
            out var admins);

        Assert.True(found);
        Assert.Equal([admin], admins);
    }

    [Fact]
    public async Task Concurrent_setters_and_readers_never_observe_a_torn_state_or_lose_an_update()
    {
        // Enough iterations that the writers and readers overlap instead of finishing one after another.
        const int iterations = 20_000;
        var firstLogger = new ListLogger();
        var secondLogger = new ListLogger();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tornReads = 0;

        var guildIdWriter = Task.Run(async () =>
        {
            await start.Task;
            for (var i = 0; i < iterations; i++)
            {
                GuildLookup.SetDefaultGuildId(i % 2 == 0 ? null : ulong.MaxValue);
            }

            GuildLookup.SetDefaultGuildId(42UL);
        });
        var loggerWriter = Task.Run(async () =>
        {
            await start.Task;
            for (var i = 0; i < iterations; i++)
            {
                GuildLookup.SetLogger(i % 2 == 0 ? firstLogger : secondLogger);
            }

            GuildLookup.SetLogger(firstLogger);
        });
        var readers = Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
        {
            await start.Task;
            for (var i = 0; i < iterations; i++)
            {
                var state = GuildLookup.Current;
                var guildIdIsKnown = state.DefaultGuildId is null or ulong.MaxValue or 42UL;
                var loggerIsKnown = ReferenceEquals(state.Logger, firstLogger)
                    || ReferenceEquals(state.Logger, secondLogger)
                    || ReferenceEquals(state.Logger, ConsoleLogger.Instance);
                if (!guildIdIsKnown || !loggerIsKnown)
                {
                    Interlocked.Increment(ref tornReads);
                }
            }
        })).ToArray();

        start.SetResult();
        await Task.WhenAll(readers.Append(guildIdWriter).Append(loggerWriter));

        Assert.Equal(0, tornReads);
        Assert.Equal(42UL, GuildLookup.Current.DefaultGuildId);
        Assert.Same(firstLogger, GuildLookup.Current.Logger);
    }

    private static ListLogger UseListLogger()
    {
        var logger = new ListLogger();
        GuildLookup.SetLogger(logger);
        return logger;
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
