using Discord;
using Discord.WebSocket;
using HelperLib.Discord.Models;
using Microsoft.Extensions.Logging;

namespace HelperLib.Discord.Util;

/// <summary>
/// Looks up guilds, channels, roles, emotes and users through <see cref="IonicHelper.Client"/>.
/// The synchronous lookups return <see langword="false"/> and log a warning
/// when the target is missing, and work only after <see cref="IonicHelper"/> has a client.
/// </summary>
public static class GuildLookup
{
    internal sealed record State(ulong? DefaultGuildId, ILogger Logger);

    private static readonly Lock StateGate = new();
    private static State _state = new(null, ConsoleLogger.Instance);

    internal static State Current => Volatile.Read(ref _state);

    internal static void SetDefaultGuildId(ulong? defaultGuildId)
    {
        lock (StateGate)
        {
            Volatile.Write(ref _state, _state with { DefaultGuildId = defaultGuildId });
        }
    }

    internal static void SetLogger(ILogger logger)
    {
        lock (StateGate)
        {
            Volatile.Write(ref _state, _state with { Logger = logger });
        }
    }

    /// <summary>Finds a text channel in a guild.</summary>
    /// <param name="guildId">The guild id; the default guild when <see langword="null"/>.</param>
    /// <param name="channelId">The channel id.</param>
    /// <param name="channel">The channel, or <see langword="null"/> when not found.</param>
    /// <returns><see langword="true"/> when the channel was found.</returns>
    public static bool GetTextChannel(ulong? guildId, ulong channelId, out SocketTextChannel? channel)
    {
        var state = Current;
        var guildFound = FindGuild(state, guildId, out var outGuild);
        return TryGetChannel(
            state.Logger,
            guildFound,
            outGuild?.GetTextChannel(channelId),
            "text channel",
            outGuild?.Id,
            channelId,
            out channel);
    }

    /// <summary>Finds a voice channel in a guild.</summary>
    /// <param name="guildId">The guild id; the default guild when <see langword="null"/>.</param>
    /// <param name="channelId">The channel id.</param>
    /// <param name="channel">The channel, or <see langword="null"/> when not found.</param>
    /// <returns><see langword="true"/> when the channel was found.</returns>
    public static bool GetVoiceChannel(
        ulong? guildId,
        ulong channelId,
        out SocketVoiceChannel? channel)
    {
        var state = Current;
        var guildFound = FindGuild(state, guildId, out var outGuild);
        return TryGetChannel(
            state.Logger,
            guildFound,
            outGuild?.GetVoiceChannel(channelId),
            "voice channel",
            outGuild?.Id,
            channelId,
            out channel);
    }

    /// <summary>Finds a guild the client is connected to.</summary>
    /// <param name="guildId">The guild id; the default guild when <see langword="null"/>.</param>
    /// <param name="outGuild">The guild, or <see langword="null"/> when no guild id is available or the guild is unknown.</param>
    /// <returns><see langword="true"/> when the guild was found.</returns>
    public static bool GetGuild(ulong? guildId, out SocketGuild? outGuild) =>
        FindGuild(Current, guildId, out outGuild);

    private static bool FindGuild(State state, ulong? guildId, out SocketGuild? outGuild)
    {
        var targetGuildId = guildId ?? state.DefaultGuildId;
        if (targetGuildId is null)
        {
            state.Logger.NoGuildIdGiven();
            outGuild = null;
            return false;
        }

        var guild = IonicHelper.Client?.GetGuild(targetGuildId.Value);
        if (guild == null)
        {
            state.Logger.NoSuchGuild(targetGuildId.Value);
            outGuild = null;
            return false;
        }

        outGuild = guild;
        return true;
    }

    /// <summary>Finds a channel category in a guild.</summary>
    /// <param name="guildId">The guild id; the default guild when <see langword="null"/>.</param>
    /// <param name="categoryId">The category id.</param>
    /// <param name="outCategory">The category, or <see langword="null"/> when not found.</param>
    /// <returns><see langword="true"/> when the category was found.</returns>
    public static bool GetCategory(
        ulong? guildId,
        ulong categoryId,
        out SocketCategoryChannel? outCategory)
    {
        var state = Current;
        var guildFound = FindGuild(state, guildId, out var outGuild);
        return TryGetCategory(
            state.Logger,
            guildFound,
            outGuild?.CategoryChannels,
            category => category.Id,
            categoryId,
            out outCategory);
    }

    /// <summary>Finds a role in a guild.</summary>
    /// <param name="guildId">The guild id; the default guild when <see langword="null"/>.</param>
    /// <param name="roleId">The role id.</param>
    /// <param name="outRole">The role, or <see langword="null"/> when not found.</param>
    /// <returns><see langword="true"/> when the role was found.</returns>
    public static bool GetRole(ulong? guildId, ulong roleId, out IRole? outRole)
    {
        var state = Current;
        var guildFound = FindGuild(state, guildId, out var outGuild);
        return TryGetRole(state.Logger, guildFound, outGuild?.GetRole(roleId), outGuild?.Id, roleId, out outRole);
    }

    /// <summary>Finds a custom emote in a guild by name, ignoring case.</summary>
    /// <param name="guildId">The guild id; the default guild when <see langword="null"/>.</param>
    /// <param name="emoteName">The emote name.</param>
    /// <param name="outEmote">The emote, or <see langword="null"/> when not found.</param>
    /// <returns><see langword="true"/> when the emote was found.</returns>
    public static bool GetEmote(ulong? guildId, string emoteName, out GuildEmote? outEmote)
    {
        var state = Current;
        var guildFound = FindGuild(state, guildId, out var outGuild);
        return TryGetEmote(state.Logger, guildFound, outGuild?.Emotes, emote => emote.Name, outGuild?.Id, emoteName, out outEmote);
    }

    /// <summary>Finds a guild member in the client's cache; it does not call the Discord API.</summary>
    /// <param name="guildId">The guild id; the default guild when <see langword="null"/>.</param>
    /// <param name="userId">The user id.</param>
    /// <param name="user">The member, or <see langword="null"/> when not cached.</param>
    /// <returns><see langword="true"/> when the member was found.</returns>
    public static bool GetGuildUserById(ulong? guildId, ulong userId, out SocketGuildUser? user) =>
        FindGuildUser(Current, guildId, userId, out user);

    private static bool FindGuildUser(State state, ulong? guildId, ulong userId, out SocketGuildUser? user)
    {
        if (!FindGuild(state, guildId, out var outGuild))
        {
            user = null;
            return false;
        }

        if (outGuild?.GetUser(userId) is not { } user1)
        {
            state.Logger.GuildUserNotFound(userId);
            user = null;
            return false;
        }

        user = user1;
        return true;
    }

    /// <summary>
    /// Gets a guild user, trying cache first then fetching from Discord API if needed.
    /// </summary>
    /// <param name="guildId">The guild ID (uses default if null)</param>
    /// <param name="userId">The user ID to fetch</param>
    /// <param name="forceRefresh">If true, bypasses cache and always fetches from API</param>
    /// <param name="cancellationToken">Cancels the Discord API request; cancellation is thrown, not swallowed</param>
    /// <returns>The guild user, or null if not found</returns>
    public static async Task<IGuildUser?> GetGuildUserAsync(
        ulong? guildId,
        ulong userId,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var state = Current;
        var targetGuildId = guildId ?? state.DefaultGuildId;
        if (targetGuildId is null)
        {
            state.Logger.NoGuildIdGiven();
            return null;
        }

        // Try cache first unless forceRefresh is requested
        if (!forceRefresh && FindGuildUser(state, targetGuildId, userId, out var cachedUser))
        {
            return cachedUser;
        }

        // Fetch from Discord REST API
        var client = IonicHelper.Client;
        if (client == null)
        {
            state.Logger.ClientNotInitialized();
            return null;
        }

        var rest = client.Rest;
        var requestOptions = new RequestOptions { CancelToken = cancellationToken };
        return await FetchGuildUserAsync(
            state.Logger,
            async () => await rest.GetGuildUserAsync(targetGuildId.Value, userId, requestOptions).ConfigureAwait(false),
            userId,
            cancellationToken).ConfigureAwait(false);
    }

    // An HTTP timeout surfaces as an OperationCanceledException too, so only the caller's own cancellation is rethrown.
    internal static async Task<IGuildUser?> FetchGuildUserAsync(
        ILogger logger,
        Func<Task<IGuildUser?>> fetch,
        ulong userId,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await fetch().ConfigureAwait(false);
            if (user == null)
            {
                logger.UserNotFoundInApi(userId);
                return null;
            }

            return user;
        }
        catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            logger.UserFetchFailed(ex, userId);
            return null;
        }
    }

    /// <summary>Finds the owner of a guild.</summary>
    /// <param name="guildId">The guild id; the default guild when <see langword="null"/>.</param>
    /// <param name="owner">The owner, or <see langword="null"/> when the guild or its owner is unavailable.</param>
    /// <returns><see langword="true"/> when the owner was found.</returns>
    public static bool GetGuildOwner(ulong? guildId, out IGuildUser? owner)
    {
        var state = Current;
        var guildFound = FindGuild(state, guildId, out var outGuild);
        return TryGetGuildOwner(state.Logger, guildFound, outGuild?.Owner, out owner);

    }

    /// <summary>Finds the cached guild members that are not bots and have the Administrator permission.</summary>
    /// <param name="guildId">The guild id; the default guild when <see langword="null"/>.</param>
    /// <param name="users">The administrators, or <see langword="null"/> when the guild's members are unavailable.</param>
    /// <returns><see langword="true"/> when the guild's members were available.</returns>
    public static bool GetGuildAdmins(ulong? guildId, out IEnumerable<IGuildUser>? users)
    {
        var state = Current;
        var guildFound = FindGuild(state, guildId, out var outGuild);
        return TryGetGuildAdmins(state.Logger, guildFound, outGuild?.Users, out users);
    }

    /// <summary>
    /// Gets all members of a guild. When the guild has all members downloaded
    /// (<c>SocketGuild.HasAllMembers</c>), Discord.Net returns the cached members as a single page;
    /// otherwise this fetches them from the Discord API, page by page.
    /// </summary>
    /// <param name="guildId">The guild ID (uses default if null)</param>
    /// <param name="cancellationToken">Cancels the Discord API requests, if any</param>
    /// <returns>The members; unsuccessful when the guild is missing or has no members</returns>
    public static async ValueTask<GuildUsersResult> GetGuildUsersByIdAsync(
        ulong? guildId,
        CancellationToken cancellationToken = default)
    {
        if (!FindGuild(Current, guildId, out var outGuild))
        {
            return new GuildUsersResult(false, []);
        }

        var usersList = new List<IGuildUser>();
        var requestOptions = new RequestOptions { CancelToken = cancellationToken };
        await foreach (var page in outGuild!.GetUsersAsync(requestOptions).ConfigureAwait(false))
        {
            usersList.AddRange(page);
        }

        return new GuildUsersResult(usersList.Count > 0, usersList);
    }

    internal static bool TryGetChannel<TChannel>(
        ILogger logger,
        bool guildFound,
        TChannel? foundChannel,
        string channelKind,
        ulong? guildId,
        ulong channelId,
        out TChannel? channel)
        where TChannel : class
    {
        if (!guildFound)
        {
            channel = null;
            return false;
        }

        if (foundChannel == null)
        {
            logger.NoSuchChannel(channelKind, channelId, guildId);
            channel = null;
            return false;
        }

        channel = foundChannel;
        return true;
    }

    internal static bool TryGetCategory<TCategory>(
        ILogger logger,
        bool guildFound,
        IEnumerable<TCategory>? categories,
        Func<TCategory, ulong> getId,
        ulong categoryId,
        out TCategory? outCategory)
        where TCategory : class
    {
        if (!guildFound || categories == null)
        {
            outCategory = null;
            return false;
        }

        var category = categories.FirstOrDefault(x => getId(x) == categoryId);
        if (category == null)
        {
            logger.NoSuchCategory(categoryId);
            outCategory = null;
            return false;
        }

        outCategory = category;
        return true;
    }

    internal static bool TryGetRole<TRole>(
        ILogger logger,
        bool guildFound,
        TRole? foundRole,
        ulong? guildId,
        ulong roleId,
        out TRole? outRole)
        where TRole : class
    {
        if (!guildFound)
        {
            outRole = null;
            return false;
        }

        if (foundRole == null)
        {
            logger.NoSuchRole(roleId, guildId);
            outRole = null;
            return false;
        }

        outRole = foundRole;
        return true;
    }

    internal static bool TryGetEmote<TEmote>(
        ILogger logger,
        bool guildFound,
        IEnumerable<TEmote>? emotes,
        Func<TEmote, string> getName,
        ulong? guildId,
        string emoteName,
        out TEmote? outEmote)
        where TEmote : class
    {
        if (!guildFound || emotes == null)
        {
            outEmote = null;
            return false;
        }

        var emote = emotes.FirstOrDefault(x =>
            getName(x).Equals(emoteName, StringComparison.OrdinalIgnoreCase));
        if (emote == null)
        {
            logger.NoSuchEmote(emoteName, guildId);
            outEmote = null;
            return false;
        }

        outEmote = emote;
        return true;
    }

    internal static bool TryGetGuildOwner<TUser>(
        ILogger logger,
        bool guildFound,
        TUser? foundOwner,
        out TUser? owner)
        where TUser : class
    {
        if (!guildFound || foundOwner == null)
        {
            logger.GuildOwnerNotFound();
            owner = null;
            return false;
        }

        owner = foundOwner;
        return true;
    }

    internal static bool TryGetGuildAdmins(
        ILogger logger,
        bool guildFound,
        IEnumerable<IGuildUser>? guildUsers,
        out IEnumerable<IGuildUser>? users)
    {
        if (!guildFound || guildUsers == null)
        {
            logger.GuildAdminsNotFound();
            users = null;
            return false;
        }

        users = guildUsers.Where(x => !x.IsBot && x.GuildPermissions.Administrator).ToArray();
        return true;
    }
}
