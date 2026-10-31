using Discord;
using Discord.Commands;
using Discord.WebSocket;
using HelperLib.Discord.Util;

namespace HelperLib.Discord.Preconditions;

/// <summary>
/// Restricts a command or command module to guild members who hold a given role.
/// </summary>
/// <param name="roleId">The id of the required role.</param>
/// <param name="errorMessage">The error returned to users who do not hold the role.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
public class RequireRoleAttribute(ulong roleId, string errorMessage = "You are not allowed to use this command!") : PreconditionAttribute
{
    /// <summary>
    /// Succeeds when the invoking user is a guild member who holds the required role.
    /// Fails when the command is used outside a server or the user lacks the role.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="command">The command being checked.</param>
    /// <param name="services">The service provider.</param>
    /// <returns>The precondition result.</returns>
    public override async Task<PreconditionResult> CheckPermissionsAsync(
        ICommandContext context,
        CommandInfo command,
        IServiceProvider services)
    {
        if (context.User is not SocketGuildUser guildUser)
        {
            return PreconditionResult.FromError("This command can only be used in a server.");
        }

        return await CheckRolesAsync(guildUser.Roles).ConfigureAwait(false);
    }

    internal Task<PreconditionResult> CheckRolesAsync(IEnumerable<IRole> roles)
    {
        var hasRequiredRole = RoleChecks.HasRolesAny(roles, roleId);
        var result = hasRequiredRole
            ? PreconditionResult.FromSuccess()
            : PreconditionResult.FromError(errorMessage);

        return Task.FromResult(result);
    }
}
