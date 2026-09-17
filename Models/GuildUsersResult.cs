using global::Discord;

namespace HelperLib.Discord.Models
{
    public record GuildUsersResult(bool Success, List<IGuildUser> Users);
}
