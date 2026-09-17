using global::Discord;
using global::Discord.WebSocket;
using HelperLib.Discord.Models;

namespace HelperLib.Discord.Util;

public static class GuildLookup
{
    private static ulong? _defaultGuildId;

    internal static void SetDefaultGuildId(ulong? defaultGuildId) => _defaultGuildId = defaultGuildId;

    public static bool GetTextChannel(ulong? guildId, ulong channelId, out SocketTextChannel? channel)
    {
        var guildFound = GetGuild(guildId ?? _defaultGuildId.GetValueOrDefault(), out var outGuild);
        return TryGetChannel(
            guildFound,
            outGuild?.GetTextChannel(channelId),
            "text channel",
            guildId,
            channelId,
            out channel);
    }

    public static bool GetVoiceChannel(
        ulong? guildId,
        ulong channelId,
        out SocketVoiceChannel? channel)
    {
        var guildFound = GetGuild(guildId ?? _defaultGuildId.GetValueOrDefault(), out var outGuild);
        return TryGetChannel(
            guildFound,
            outGuild?.GetVoiceChannel(channelId),
            "voice channel",
            guildId,
            channelId,
            out channel);
    }

    public static bool GetGuild(ulong? guildId, out SocketGuild? outGuild)
    {
        var guild = IonicHelper.Client?.GetGuild(guildId ?? _defaultGuildId.GetValueOrDefault());
        if (guild == null)
        {
            Console.WriteLine($"No such guild \"{guildId}\"!");
            outGuild = null;
            return false;
        }

        outGuild = guild;
        return true;
    }

    public static bool GetCategory(
        ulong? guildId,
        ulong categoryId,
        out SocketCategoryChannel? outCategory)
    {
        var guildFound = GetGuild(guildId ?? _defaultGuildId.GetValueOrDefault(), out var outGuild);
        return TryGetCategory(
            guildFound,
            outGuild?.CategoryChannels,
            category => category.Id,
            categoryId,
            out outCategory);
    }

    public static bool GetRole(ulong? guildId, ulong roleId, out IRole? outRole)
    {
        var guildFound = GetGuild(guildId ?? _defaultGuildId.GetValueOrDefault(), out var outGuild);
        return TryGetRole(guildFound, outGuild?.GetRole(roleId), guildId, roleId, out outRole);
    }

    public static bool GetEmote(ulong? guildId, string emoteName, out GuildEmote? outEmote)
    {
        var guildFound = GetGuild(guildId ?? _defaultGuildId.GetValueOrDefault(), out var outGuild);
        return TryGetEmote(guildFound, outGuild?.Emotes, emote => emote.Name, guildId, emoteName, out outEmote);
    }

    public static bool GetGuildUserById(ulong? guildId, ulong userId, out SocketGuildUser? user)
    {
        if (!GetGuild(guildId ?? _defaultGuildId.GetValueOrDefault(), out var outGuild))
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
    /// <returns>The guild user, or null if not found</returns>
    public static async Task<IGuildUser?> GetGuildUserAsync(
        ulong? guildId,
        ulong userId,
        bool forceRefresh = false)
    {
        var targetGuildId = guildId ?? _defaultGuildId.GetValueOrDefault();

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

        try
        {
            var user = await IonicHelper.Client.Rest.GetGuildUserAsync(targetGuildId, userId);
            if (user == null)
            {
                Console.WriteLine($"Failed to get user \"{userId}\" from Discord API");
                return null;
            }

            return user;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to fetch user {userId} from API: {ex.Message}");
            return null;
        }
    }

    public static bool GetGuildOwner(ulong? guildId, out IGuildUser? owner)
    {
        var guildFound = GetGuild(guildId ?? _defaultGuildId.GetValueOrDefault(), out var outGuild);
        return TryGetGuildOwner(guildFound, outGuild?.Owner, out owner);

    }

    public static bool GetGuildAdmins(ulong? guildId, out IEnumerable<IGuildUser>? users)
    {
        var guildFound = GetGuild(guildId ?? _defaultGuildId.GetValueOrDefault(), out var outGuild);
        return TryGetGuildAdmins(guildFound, outGuild?.Users, out users);
    }

    public static async ValueTask<GuildUsersResult> GetGuildUsersByIdAsync(ulong? guildId)
    {
        if (!GetGuild(guildId ?? _defaultGuildId.GetValueOrDefault(), out var outGuild))
        {
            return new GuildUsersResult(false, []);
        }

        var usersList = new List<IGuildUser>();
        await foreach (var page in outGuild!.GetUsersAsync())
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
            getName(x).Equals(emoteName, StringComparison.InvariantCultureIgnoreCase));
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
