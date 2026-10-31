using Discord;
using Discord.WebSocket;

namespace HelperLib.Discord.Util;

/// <summary>
/// Checks whether a guild member holds given roles.
/// </summary>
public static class RoleChecks
{
    internal static bool HasRolesAny(IEnumerable<IRole> roles, params ReadOnlySpan<ulong> roleIds)
    {
        foreach (var role in roles)
        {
            if (roleIds.Contains(role.Id))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Checks whether a member holds at least one of the roles.</summary>
    /// <param name="sgUser">The guild member.</param>
    /// <param name="roleIds">The role ids to look for.</param>
    /// <returns><see langword="true"/> when the member holds any of the roles; <see langword="false"/> when <paramref name="roleIds"/> is empty.</returns>
    public static bool HasRolesAny(SocketGuildUser sgUser, params ReadOnlySpan<ulong> roleIds) =>
        HasRolesAny(sgUser.Roles, roleIds);

    internal static bool HasRolesAll(IEnumerable<IRole> roles, params ReadOnlySpan<ulong> roleIds)
    {
        var heldRoleIds = roles.Select(role => role.Id).ToHashSet();
        foreach (var roleId in roleIds)
        {
            if (!heldRoleIds.Contains(roleId))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Checks whether a member holds every one of the roles.</summary>
    /// <param name="sgUser">The guild member.</param>
    /// <param name="roleIds">The role ids to look for.</param>
    /// <returns><see langword="true"/> when the member holds all of the roles, including when <paramref name="roleIds"/> is empty.</returns>
    public static bool HasRolesAll(SocketGuildUser sgUser, params ReadOnlySpan<ulong> roleIds) =>
        HasRolesAll(sgUser.Roles, roleIds);
}
