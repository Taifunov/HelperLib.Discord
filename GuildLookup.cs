using System.Diagnostics.CodeAnalysis;
using Discord;
using Discord.WebSocket;
using HelperLib.Discord.Models;
using HelperLib.Discord.Util;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HelperLib.Discord;

/// <summary>
/// Looks up guilds, channels, roles, emotes and users through the host's <see cref="DiscordSocketClient"/>.
/// The synchronous lookups return <see langword="false"/> and log a warning when the target is missing.
/// It keeps no mutable state, so it is safe to use from any thread.
/// </summary>
public sealed class GuildLookup
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger _logger;
    private readonly ulong? _defaultGuildId;

    /// <summary>Creates a lookup over a client.</summary>
    /// <param name="client">The client whose cache the lookups read.</param>
    /// <param name="options">Supplies <see cref="HelperLibDiscordOptions.DefaultGuildId"/>, read once.</param>
    /// <param name="logger">Receives the warnings of lookups that find nothing.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public GuildLookup(DiscordSocketClient client, IOptions<HelperLibDiscordOptions> options,
        ILogger<GuildLookup> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _client = client;
        _logger = logger;
        _defaultGuildId = options.Value.DefaultGuildId;
    }

    /// <summary>Finds a text channel in a guild.</summary>
    /// <param name="guildId">The guild id; the default guild when <see langword="null"/>.</param>
    /// <param name="channelId">The channel id.</param>
    /// <param name="channel">The channel, or <see langword="null"/> when not found.</param>
    /// <returns><see langword="true"/> when the channel was found.</returns>
    public bool GetTextChannel(ulong? guildId, ulong channelId, [NotNullWhen(true)] out SocketTextChannel? channel) =>
        TryFind(
            guildId,
            guild => guild.GetTextChannel(channelId),
            (logger, resolvedGuildId) => logger.NoSuchChannel("text channel", channelId, resolvedGuildId),
            out channel);

    /// <summary>Finds a voice channel in a guild.</summary>
    /// <param name="guildId">The guild id; the default guild when <see langword="null"/>.</param>
    /// <param name="channelId">The channel id.</param>
    /// <param name="channel">The channel, or <see langword="null"/> when not found.</param>
    /// <returns><see langword="true"/> when the channel was found.</returns>
    public bool GetVoiceChannel(ulong? guildId, ulong channelId, [NotNullWhen(true)] out SocketVoiceChannel? channel) =>
        TryFind(
            guildId,
            guild => guild.GetVoiceChannel(channelId),
            (logger, resolvedGuildId) => logger.NoSuchChannel("voice channel", channelId, resolvedGuildId),
            out channel);

    /// <summary>Finds a guild the client is connected to.</summary>
    /// <param name="guildId">The guild id; the default guild when <see langword="null"/>.</param>
    /// <param name="outGuild">The guild, or <see langword="null"/> when no guild id is available or the guild is unknown.</param>
    /// <returns><see langword="true"/> when the guild was found.</returns>
    public bool GetGuild(ulong? guildId, [NotNullWhen(true)] out SocketGuild? outGuild)
    {
        if (!TryResolveGuildId(guildId, out var resolvedGuildId))
        {
            outGuild = null;
            return false;
        }

        outGuild = _client.GetGuild(resolvedGuildId);
        if (outGuild != null)
        {
            return true;
        }

        _logger.NoSuchGuild(resolvedGuildId);
        return false;
    }

    /// <summary>Finds a channel category in a guild.</summary>
    /// <param name="guildId">The guild id; the default guild when <see langword="null"/>.</param>
    /// <param name="categoryId">The category id.</param>
    /// <param name="outCategory">The category, or <see langword="null"/> when not found.</param>
    /// <returns><see langword="true"/> when the category was found.</returns>
    public bool GetCategory(ulong? guildId, ulong categoryId,
        [NotNullWhen(true)] out SocketCategoryChannel? outCategory) =>
        TryFind(
            guildId,
            guild => guild.GetCategoryChannel(categoryId),
            (logger, _) => logger.NoSuchCategory(categoryId),
            out outCategory);

    /// <summary>Finds a role in a guild.</summary>
    /// <param name="guildId">The guild id; the default guild when <see langword="null"/>.</param>
    /// <param name="roleId">The role id.</param>
    /// <param name="outRole">The role, or <see langword="null"/> when not found.</param>
    /// <returns><see langword="true"/> when the role was found.</returns>
    public bool GetRole(ulong? guildId, ulong roleId, [NotNullWhen(true)] out IRole? outRole) =>
        TryFind(
            guildId,
            guild => guild.GetRole(roleId),
            (logger, resolvedGuildId) => logger.NoSuchRole(roleId, resolvedGuildId),
            out outRole);

    /// <summary>Finds a custom emote in a guild by name, ignoring case.</summary>
    /// <param name="guildId">The guild id; the default guild when <see langword="null"/>.</param>
    /// <param name="emoteName">The emote name.</param>
    /// <param name="outEmote">The emote, or <see langword="null"/> when not found.</param>
    /// <returns><see langword="true"/> when the emote was found.</returns>
    public bool GetEmote(ulong? guildId, string emoteName, [NotNullWhen(true)] out GuildEmote? outEmote) =>
        TryFind(
            guildId,
            guild => FindEmoteByName(guild.Emotes, emoteName),
            (logger, resolvedGuildId) => logger.NoSuchEmote(emoteName, resolvedGuildId),
            out outEmote);

    /// <summary>Finds a guild member in the client's cache; it does not call the Discord API.</summary>
    /// <param name="guildId">The guild id; the default guild when <see langword="null"/>.</param>
    /// <param name="userId">The user id.</param>
    /// <param name="user">The member, or <see langword="null"/> when not cached.</param>
    /// <returns><see langword="true"/> when the member was found.</returns>
    public bool GetGuildUserById(ulong? guildId, ulong userId, [NotNullWhen(true)] out SocketGuildUser? user) =>
        TryFind(
            guildId,
            guild => guild.GetUser(userId),
            (logger, _) => logger.GuildUserNotFound(userId),
            out user);

    /// <summary>
    /// Gets a guild user, trying cache first then fetching from Discord API if needed.
    /// </summary>
    /// <param name="guildId">The guild ID (uses default if null)</param>
    /// <param name="userId">The user ID to fetch</param>
    /// <param name="forceRefresh">If true, bypasses cache and always fetches from API</param>
    /// <param name="cancellationToken">Cancels the Discord API request; cancellation is thrown, not swallowed</param>
    /// <returns>The guild user, or null if not found</returns>
    public async Task<IGuildUser?> GetGuildUserAsync(
        ulong? guildId,
        ulong userId,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveGuildId(guildId, out var resolvedGuildId))
        {
            return null;
        }

        // Try cache first unless forceRefresh is requested
        if (!forceRefresh && GetGuildUserById(resolvedGuildId, userId, out var cachedUser))
        {
            return cachedUser;
        }

        // Fetch from Discord REST API
        var requestOptions = new RequestOptions { CancelToken = cancellationToken };
        return await FetchGuildUserAsync(
            _logger,
            async () => await _client.Rest.GetGuildUserAsync(resolvedGuildId, userId, requestOptions)
                .ConfigureAwait(false),
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
            if (user != null)
            {
                return user;
            }

            logger.UserNotFoundInApi(userId);
            return null;
        }
        catch (Exception exception) when (!(exception is OperationCanceledException &&
                                            cancellationToken.IsCancellationRequested))
        {
            logger.UserFetchFailed(exception, userId);
            return null;
        }
    }

    /// <summary>Finds the owner of a guild.</summary>
    /// <param name="guildId">The guild id; the default guild when <see langword="null"/>.</param>
    /// <param name="owner">The owner, or <see langword="null"/> when the guild or its owner is unavailable.</param>
    /// <returns><see langword="true"/> when the owner was found.</returns>
    public bool GetGuildOwner(ulong? guildId, [NotNullWhen(true)] out IGuildUser? owner) =>
        TryFind(guildId, guild => guild.Owner, (logger, _) => logger.GuildOwnerNotFound(), out owner);

    /// <summary>Finds the cached guild members that are not bots and have the Administrator permission.</summary>
    /// <param name="guildId">The guild id; the default guild when <see langword="null"/>.</param>
    /// <param name="users">The administrators, or <see langword="null"/> when the guild is unavailable.</param>
    /// <returns><see langword="true"/> when the guild was found.</returns>
    public bool GetGuildAdmins(ulong? guildId, [NotNullWhen(true)] out IEnumerable<IGuildUser>? users)
    {
        if (!GetGuild(guildId, out var guild))
        {
            users = null;
            return false;
        }

        users = SelectAdmins(guild.Users);
        return true;
    }

    /// <summary>
    /// Gets all members of a guild. When the guild has all members downloaded
    /// (<c>SocketGuild.HasAllMembers</c>), Discord.Net returns the cached members as a single page;
    /// otherwise this fetches them from the Discord API, page by page.
    /// </summary>
    /// <param name="guildId">The guild ID (uses default if null)</param>
    /// <param name="cancellationToken">Cancels the Discord API requests, if any</param>
    /// <returns>The members; unsuccessful when the guild is missing or has no members</returns>
    public async ValueTask<GuildUsersResult> GetGuildUsersByIdAsync(
        ulong? guildId,
        CancellationToken cancellationToken = default)
    {
        if (!GetGuild(guildId, out var outGuild))
        {
            return new GuildUsersResult(false, []);
        }

        var usersList = new List<IGuildUser>();
        var requestOptions = new RequestOptions { CancelToken = cancellationToken };
        await foreach (var page in outGuild
                           .GetUsersAsync(requestOptions)
                           .WithCancellation(cancellationToken)
                           .ConfigureAwait(false))
        {
            usersList.AddRange(page);
        }

        return new GuildUsersResult(usersList.Count > 0, usersList);
    }

    private bool TryResolveGuildId(ulong? guildId, out ulong resolvedGuildId)
    {
        var targetGuildId = guildId ?? _defaultGuildId;
        if (targetGuildId is null)
        {
            _logger.NoGuildIdGiven();
            resolvedGuildId = 0;
            return false;
        }

        resolvedGuildId = targetGuildId.Value;
        return true;
    }

    internal static IGuildUser[] SelectAdmins(IEnumerable<IGuildUser> guildUsers) =>
        [.. guildUsers.Where(guildUser => guildUser is { IsBot: false, GuildPermissions.Administrator: true })];

    internal static TEmote? FindEmoteByName<TEmote>(IEnumerable<TEmote> emotes, string emoteName)
        where TEmote : class, IEmote =>
        emotes.FirstOrDefault(emote => emote.Name.Equals(emoteName, StringComparison.OrdinalIgnoreCase));

    private bool TryFind<TTarget>(
        ulong? guildId,
        Func<SocketGuild, TTarget?> find,
        Action<ILogger, ulong> logMissingTarget,
        [NotNullWhen(true)] out TTarget? target)
        where TTarget : class
    {
        var guildFound = GetGuild(guildId, out var guild);
        return TrySelect(_logger, guildFound, () => find(guild!), logger => logMissingTarget(logger, guild!.Id),
            out target);
    }

    // The guild lookup already logs a missing guild, so only a missing target is logged here.
    internal static bool TrySelect<TTarget>(
        ILogger logger,
        bool guildFound,
        Func<TTarget?> find,
        Action<ILogger> logMissingTarget,
        [NotNullWhen(true)] out TTarget? target)
        where TTarget : class
    {
        target = null;
        if (!guildFound)
        {
            return false;
        }

        target = find();
        if (target != null)
        {
            return true;
        }

        logMissingTarget(logger);
        return false;
    }
}
