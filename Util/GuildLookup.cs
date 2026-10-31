using Discord;
using Discord.WebSocket;
using HelperLib.Discord.Models;

namespace HelperLib.Discord.Util;

/// <summary>
/// Looks up guilds, channels, roles, emotes and users through <see cref="IonicHelper.Client"/>.
/// The synchronous lookups return <see langword="false"/> and write a message to the console
/// when the target is missing, and work only after <see cref="IonicHelper"/> has a client.
/// </summary>
public static class GuildLookup
{
    private const string NoGuildIdMessage = "No guild id given and no default guild id set!";

    private static ulong? _defaultGuildId;

    internal static void SetDefaultGuildId(ulong? defaultGuildId) => _defaultGuildId = defaultGuildId;

    /// <summary>Finds a text channel in a guild.</summary>
    /// <param name="guildId">The guild id; the default guild when <see langword="null"/>.</param>
    /// <param name="channelId">The channel id.</param>
    /// <param name="channel">The channel, or <see langword="null"/> when not found.</param>
    /// <returns><see langword="true"/> when the channel was found.</returns>
    public static bool GetTextChannel(ulong? guildId, ulong channelId, out SocketTextChannel? channel)
    {
        var guildFound = GetGuild(guildId, out var outGuild);
        return TryGetChannel(
            guildFound,
            outGuild?.GetTextChannel(channelId),
            "text channel",
            guildId,
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
        var guildFound = GetGuild(guildId, out var outGuild);
        return TryGetChannel(
            guildFound,
            outGuild?.GetVoiceChannel(channelId),
            "voice channel",
            guildId,
            channelId,
            out channel);
    }

    /// <summary>Finds a guild the client is connected to.</summary>
    /// <param name="guildId">The guild id; the default guild when <see langword="null"/>.</param>
    /// <param name="outGuild">The guild, or <see langword="null"/> when no guild id is available or the guild is unknown.</param>
    /// <returns><see langword="true"/> when the guild was found.</returns>
    public static bool GetGuild(ulong? guildId, out SocketGuild? outGuild)
    {
        var targetGuildId = guildId ?? _defaultGuildId;
        if (targetGuildId is null)
        {
            Console.WriteLine(NoGuildIdMessage);
            outGuild = null;
            return false;
        }

        var guild = IonicHelper.Client?.GetGuild(targetGuildId.Value);
        if (guild == null)
        {
            Console.WriteLine($"No such guild \"{targetGuildId}\"!");
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
        var guildFound = GetGuild(guildId, out var outGuild);
        return TryGetCategory(
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
        var guildFound = GetGuild(guildId, out var outGuild);
        return TryGetRole(guildFound, outGuild?.GetRole(roleId), guildId, roleId, out outRole);
    }

    /// <summary>Finds a custom emote in a guild by name, ignoring case.</summary>
    /// <param name="guildId">The guild id; the default guild when <see langword="null"/>.</param>
    /// <param name="emoteName">The emote name.</param>
    /// <param name="outEmote">The emote, or <see langword="null"/> when not found.</param>
    /// <returns><see langword="true"/> when the emote was found.</returns>
    public static bool GetEmote(ulong? guildId, string emoteName, out GuildEmote? outEmote)
    {
        var guildFound = GetGuild(guildId, out var outGuild);
        return TryGetEmote(guildFound, outGuild?.Emotes, emote => emote.Name, guildId, emoteName, out outEmote);
    }

    /// <summary>Finds a guild member in the client's cache; it does not call the Discord API.</summary>
    /// <param name="guildId">The guild id; the default guild when <see langword="null"/>.</param>
    /// <param name="userId">The user id.</param>
    /// <param name="user">The member, or <see langword="null"/> when not cached.</param>
    /// <returns><see langword="true"/> when the member was found.</returns>
    public static bool GetGuildUserById(ulong? guildId, ulong userId, out SocketGuildUser? user)
    {
        if (!GetGuild(guildId, out var outGuild))
        {
            user = null;
            return false;
        }

        if (outGuild?.GetUser(userId) is not { } user1)
        {
            Console.WriteLine($"Failed to get SocketGuildUser \"{userId}\"");
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
        var targetGuildId = guildId ?? _defaultGuildId;
        if (targetGuildId is null)
        {
            Console.WriteLine(NoGuildIdMessage);
            return null;
        }

        // Try cache first unless forceRefresh is requested
        if (!forceRefresh && GetGuildUserById(targetGuildId, userId, out var cachedUser))
        {
            return cachedUser;
        }

        // Fetch from Discord REST API
        if (IonicHelper.Client == null)
        {
            Console.WriteLine("Discord client is not initialized");
            return null;
        }

        var rest = IonicHelper.Client.Rest;
        var requestOptions = new RequestOptions { CancelToken = cancellationToken };
        return await FetchGuildUserAsync(
            async () => await rest.GetGuildUserAsync(targetGuildId.Value, userId, requestOptions),
            userId,
            cancellationToken);
    }

    // An HTTP timeout surfaces as an OperationCanceledException too, so only the caller's own cancellation is rethrown.
    internal static async Task<IGuildUser?> FetchGuildUserAsync(
        Func<Task<IGuildUser?>> fetch,
        ulong userId,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await fetch();
            if (user == null)
            {
                Console.WriteLine($"Failed to get user \"{userId}\" from Discord API");
                return null;
            }

            return user;
        }
        catch (Exception ex) when (!(ex is OperationCanceledException && cancellationToken.IsCancellationRequested))
        {
            Console.WriteLine($"Failed to fetch user {userId} from API: {ex.Message}");
            return null;
        }
    }

    /// <summary>Finds the owner of a guild.</summary>
    /// <param name="guildId">The guild id; the default guild when <see langword="null"/>.</param>
    /// <param name="owner">The owner, or <see langword="null"/> when the guild or its owner is unavailable.</param>
    /// <returns><see langword="true"/> when the owner was found.</returns>
    public static bool GetGuildOwner(ulong? guildId, out IGuildUser? owner)
    {
        var guildFound = GetGuild(guildId, out var outGuild);
        return TryGetGuildOwner(guildFound, outGuild?.Owner, out owner);

    }

    /// <summary>Finds the cached guild members that are not bots and have the Administrator permission.</summary>
    /// <param name="guildId">The guild id; the default guild when <see langword="null"/>.</param>
    /// <param name="users">The administrators, or <see langword="null"/> when the guild's members are unavailable.</param>
    /// <returns><see langword="true"/> when the guild's members were available.</returns>
    public static bool GetGuildAdmins(ulong? guildId, out IEnumerable<IGuildUser>? users)
    {
        var guildFound = GetGuild(guildId, out var outGuild);
        return TryGetGuildAdmins(guildFound, outGuild?.Users, out users);
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
        if (!GetGuild(guildId, out var outGuild))
        {
            return new GuildUsersResult(false, []);
        }

        var usersList = new List<IGuildUser>();
        var requestOptions = new RequestOptions { CancelToken = cancellationToken };
        await foreach (var page in outGuild!.GetUsersAsync(requestOptions))
        {
            usersList.AddRange(page);
        }

        return new GuildUsersResult(usersList.Count > 0, usersList);
    }

    internal static bool TryGetChannel<TChannel>(
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
            Console.WriteLine($"No such {channelKind} {channelId} on guild \"{guildId}\"!");
            channel = null;
            return false;
        }

        channel = foundChannel;
        return true;
    }

    internal static bool TryGetCategory<TCategory>(
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
            Console.WriteLine($"No such category \"{categoryId}\"!");
            outCategory = null;
            return false;
        }

        outCategory = category;
        return true;
    }

    internal static bool TryGetRole<TRole>(
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
            Console.WriteLine($"No such role {roleId} on guild \"{guildId}\"");
            outRole = null;
            return false;
        }

        outRole = foundRole;
        return true;
    }

    internal static bool TryGetEmote<TEmote>(
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
            Console.WriteLine($"No such emote \"{emoteName}\" on guild \"{guildId}\"");
            outEmote = null;
            return false;
        }

        outEmote = emote;
        return true;
    }

    internal static bool TryGetGuildOwner<TUser>(
        bool guildFound,
        TUser? foundOwner,
        out TUser? owner)
        where TUser : class
    {
        if (!guildFound || foundOwner == null)
        {
            Console.WriteLine("Failed to get SocketGuildOwner");
            owner = null;
            return false;
        }

        owner = foundOwner;
        return true;
    }

    internal static bool TryGetGuildAdmins(
        bool guildFound,
        IEnumerable<IGuildUser>? guildUsers,
        out IEnumerable<IGuildUser>? users)
    {
        if (!guildFound || guildUsers == null)
        {
            Console.WriteLine("Failed to get SocketGuildUserAdmins");
            users = null;
            return false;
        }

        users = guildUsers.Where(x => !x.IsBot && x.GuildPermissions.Administrator).ToArray();
        return true;
    }
}
