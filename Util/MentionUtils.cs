using System.Globalization;

namespace HelperLib.Discord.Util;

/// <summary>
/// Creates and parses Discord mention strings for users, channels and roles.
/// </summary>
public static class MentionUtils
{
    internal static string MentionUser(string id) => $"<@{id}>";

    /// <summary>Creates a user mention.</summary>
    /// <param name="id">The user id.</param>
    /// <returns>The mention in the form <c>&lt;@id&gt;</c>.</returns>
    public static string MentionUser(ulong id) => MentionUser(id.ToString(CultureInfo.InvariantCulture));

    internal static string MentionChannel(string id) => $"<#{id}>";

    /// <summary>Creates a channel mention.</summary>
    /// <param name="id">The channel id.</param>
    /// <returns>The mention in the form <c>&lt;#id&gt;</c>.</returns>
    public static string MentionChannel(ulong id) => MentionChannel(id.ToString(CultureInfo.InvariantCulture));

    internal static string MentionRole(string id) => $"<@&{id}>";

    /// <summary>Creates a role mention.</summary>
    /// <param name="id">The role id.</param>
    /// <returns>The mention in the form <c>&lt;@&amp;id&gt;</c>.</returns>
    public static string MentionRole(ulong id) => MentionRole(id.ToString(CultureInfo.InvariantCulture));

    /// <summary>Tries to read the user id from a user mention, with or without the nickname marker (<c>&lt;@!id&gt;</c>).</summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="userId">The user id, or 0 when parsing fails.</param>
    /// <returns><see langword="true"/> when <paramref name="text"/> is a valid user mention.</returns>
    public static bool TryParseUser(string text, out ulong userId)
    {
        return TryParseMention(text, "<@!", out userId) ||
               TryParseMention(text, "<@", out userId);
    }

    /// <summary>Reads the user id from a user mention.</summary>
    /// <param name="text">The mention to parse.</param>
    /// <returns>The user id.</returns>
    /// <exception cref="ArgumentException"><paramref name="text"/> is not a valid user mention.</exception>
    public static ulong ParseUser(string text)
    {
        return TryParseUser(text, out var id)
            ? id
            : throw new ArgumentException(message: "Invalid mention format.", paramName: nameof(text));
    }

    /// <summary>Reads the channel id from a channel mention.</summary>
    /// <param name="text">The mention to parse.</param>
    /// <returns>The channel id.</returns>
    /// <exception cref="ArgumentException"><paramref name="text"/> is not a valid channel mention.</exception>
    public static ulong ParseChannel(string text)
    {
        return TryParseChannel(text, out var id)
            ? id
            : throw new ArgumentException(message: "Invalid mention format.", paramName: nameof(text));
    }

    /// <summary>Tries to read the channel id from a channel mention.</summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="channelId">The channel id, or 0 when parsing fails.</param>
    /// <returns><see langword="true"/> when <paramref name="text"/> is a valid channel mention.</returns>
    public static bool TryParseChannel(string text, out ulong channelId)
    {
        return TryParseMention(text, "<#", out channelId);
    }

    /// <summary>Reads the role id from a role mention.</summary>
    /// <param name="text">The mention to parse.</param>
    /// <returns>The role id.</returns>
    /// <exception cref="ArgumentException"><paramref name="text"/> is not a valid role mention.</exception>
    public static ulong ParseRole(string text)
    {
        return TryParseRole(text, out var id)
            ? id
            : throw new ArgumentException(message: "Invalid mention format.", paramName: nameof(text));
    }

    /// <summary>Tries to read the role id from a role mention.</summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="roleId">The role id, or 0 when parsing fails.</param>
    /// <returns><see langword="true"/> when <paramref name="text"/> is a valid role mention.</returns>
    public static bool TryParseRole(string text, out ulong roleId)
    {
        return TryParseMention(text, "<@&", out roleId);
    }

    private static bool TryParseMention(string text, string prefix, out ulong id)
    {
        if (text.Length > prefix.Length + 1 &&
            text.StartsWith(prefix, StringComparison.Ordinal) &&
            text[^1] == '>')
        {
            return ulong.TryParse(
                text.AsSpan(prefix.Length, text.Length - prefix.Length - 1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out id);
        }

        id = 0;
        return false;
    }
}
