using global::Discord.Commands;
using global::Discord.WebSocket;
using global::Discord;
using HelperLib.Discord.Util;

namespace HelperLib.Discord.Preconditions;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false)]
public class RequireRoleAttribute(ulong roleId, string errorMessage = "You are not allowed to use this command!") : PreconditionAttribute
{
    public override async Task<PreconditionResult> CheckPermissionsAsync(
        ICommandContext context,
        CommandInfo command,
        IServiceProvider services)
    {
        if (context.User is not SocketGuildUser guildUser)
        {
            return PreconditionResult.FromError("This command can only be used in a server.");
        }

        return await CheckRolesAsync(guildUser.Roles);
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
