using System.Globalization;

namespace HelperLib.Discord.Util
{
    public static class MentionUtils
    {
        internal static string MentionUser(string id) => $"<@{id}>";

        public static string MentionUser(ulong id) => MentionUser(id.ToString());

        internal static string MentionChannel(string id) => $"<#{id}>";

        public static string MentionChannel(ulong id) => MentionChannel(id.ToString());

        internal static string MentionRole(string id) => $"<@&{id}>";

        public static string MentionRole(ulong id) => MentionRole(id.ToString());

        public static bool TryParseUser(string text, out ulong userId)
        {
            return TryParseMention(text, "<@!", out userId) ||
                   TryParseMention(text, "<@", out userId);
        }

        public static ulong ParseUser(string text)
        {
            return TryParseUser(text, out var id)
                ? id
                : throw new ArgumentException(message: "Invalid mention format.", paramName: nameof(text));
        }

        public static ulong ParseChannel(string text)
        {
            return TryParseChannel(text, out var id)
                ? id
                : throw new ArgumentException(message: "Invalid mention format.", paramName: nameof(text));
        }

        public static bool TryParseChannel(string text, out ulong channelId)
        {
            return TryParseMention(text, "<#", out channelId);
        }

        public static ulong ParseRole(string text)
        {
            return TryParseRole(text, out var id)
                ? id
                : throw new ArgumentException(message: "Invalid mention format.", paramName: nameof(text));
        }

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
}
