using Discord;

namespace HelperLib.Discord.Models;

/// <summary>
/// The outcome of enumerating a guild's members.
/// </summary>
/// <param name="Success"><see langword="true"/> when the guild was found and has at least one member.</param>
/// <param name="Users">The members found; empty when the lookup was unsuccessful.</param>
public record GuildUsersResult(bool Success, List<IGuildUser> Users);
