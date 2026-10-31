using Discord;
using Discord.WebSocket;

namespace HelperLib.Discord.Util;

/// <summary>
/// Checks whether a guild member holds given roles.
/// </summary>
public static class RoleChecks
{
    internal static bool HasRolesAny(IEnumerable<IRole> roles, params ulong[] roleIds)
    {
        var roleIdsSet = new HashSet<ulong>(roleIds);
        return roles.Any(role => roleIdsSet.Contains(role.Id));
    }

    /// <summary>Checks whether a member holds at least one of the roles.</summary>
    /// <param name="sgUser">The guild member.</param>
    /// <param name="roleIds">The role ids to look for.</param>
    /// <returns><see langword="true"/> when the member holds any of the roles; <see langword="false"/> when <paramref name="roleIds"/> is empty.</returns>
    public static bool HasRolesAny(SocketGuildUser sgUser, params ulong[] roleIds) =>
        HasRolesAny(sgUser.Roles, roleIds);

    internal static bool HasRolesAll(IEnumerable<IRole> roles, params ulong[] roleIds)
    {
        var roleIdsSet = new HashSet<ulong>(roles.Select(role => role.Id));
        return roleIds.All(roleIdsSet.Contains);
    }

    /// <summary>Checks whether a member holds every one of the roles.</summary>
    /// <param name="sgUser">The guild member.</param>
    /// <param name="roleIds">The role ids to look for.</param>
    /// <returns><see langword="true"/> when the member holds all of the roles, including when <paramref name="roleIds"/> is empty.</returns>
    public static bool HasRolesAll(SocketGuildUser sgUser, params ulong[] roleIds) =>
        HasRolesAll(sgUser.Roles, roleIds);

    /// <summary>Checks whether a cached guild member holds at least one of the roles.</summary>
    /// <param name="guildId">The guild id; the default guild when <see langword="null"/>.</param>
    /// <param name="userId">The user id.</param>
    /// <param name="roleIds">The role ids to look for.</param>
    /// <returns><see langword="true"/> when the member was found and holds any of the roles; <see langword="false"/> when the member is not found.</returns>
    public static bool HasRolesAny(ulong? guildId, ulong userId, params ulong[] roleIds) =>
        GuildLookup.GetGuildUserById(guildId, userId, out var user) && HasRolesAny(user!, roleIds);

    /// <summary>Checks whether a cached guild member holds every one of the roles.</summary>
    /// <param name="guildId">The guild id; the default guild when <see langword="null"/>.</param>
    /// <param name="userId">The user id.</param>
    /// <param name="roleIds">The role ids to look for.</param>
    /// <returns><see langword="true"/> when the member was found and holds all of the roles; <see langword="false"/> when the member is not found.</returns>
    public static bool HasRolesAll(ulong? guildId, ulong userId, params ulong[] roleIds) =>
        GuildLookup.GetGuildUserById(guildId, userId, out var user) && HasRolesAll(user!, roleIds);
}
