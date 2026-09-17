using global::Discord;
using global::Discord.WebSocket;

namespace HelperLib.Discord.Util;

public static class RoleChecks
{
    internal static bool HasRolesAny(IEnumerable<IRole> roles, params ulong[] roleIds)
    {
        var roleIdsSet = new HashSet<ulong>(roleIds);
        return roles.Any(role => roleIdsSet.Contains(role.Id));
    }

    public static bool HasRolesAny(SocketGuildUser sgUser, params ulong[] roleIds) =>
        HasRolesAny(sgUser.Roles, roleIds);

    internal static bool HasRolesAll(IEnumerable<IRole> roles, params ulong[] roleIds)
    {
        var roleIdsSet = new HashSet<ulong>(roles.Select(role => role.Id));
        return roleIds.All(roleIdsSet.Contains);
    }

    public static bool HasRolesAll(SocketGuildUser sgUser, params ulong[] roleIds) =>
        HasRolesAll(sgUser.Roles, roleIds);

    public static bool HasRolesAny(ulong? guildId, ulong userId, params ulong[] roleIds) =>
        GuildLookup.GetGuildUserById(guildId, userId, out var user) && HasRolesAny(user!, roleIds);

    public static bool HasRolesAll(ulong? guildId, ulong userId, params ulong[] roleIds) =>
        GuildLookup.GetGuildUserById(guildId, userId, out var user) && HasRolesAll(user!, roleIds);
}
