using Microsoft.Extensions.Logging;

namespace HelperLib.Discord.Util;

/// <summary>Every log message of the library, source-generated so that the message templates stay constant.</summary>
internal static partial class Log
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Reconnect watchdog failed.")]
    internal static partial void ReconnectWatchdogFailed(this ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Client disconnected again while a reconnect check is running, ignoring the duplicate.")]
    internal static partial void DuplicateDisconnectIgnored(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Client reconnected, resetting cancel tokens...")]
    internal static partial void ClientReconnected(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Client reconnected, cancel tokens reset")]
    internal static partial void CancelTokensReset(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Client disconnected, starting timeout task...")]
    internal static partial void ClientDisconnected(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Timeout expired, continuing to check client state...")]
    internal static partial void TimeoutExpired(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "State came back okay")]
    internal static partial void StateCameBackOkay(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Critical, Message = "{Reason} Killing process.")]
    internal static partial void KillingProcess(this ILogger logger, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Asking the host to stop failed; the process still ends after the grace period.")]
    internal static partial void GracefulStopFailed(this ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Waiting for the shutdown grace period failed; ending the process now.")]
    internal static partial void GracePeriodWaitFailed(this ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No heartbeat acknowledgement for {Timeout}; terminating.")]
    internal static partial void NoHeartbeatAcknowledgement(this ILogger logger, TimeSpan timeout);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Client is already reconnecting, waiting for it to settle...")]
    internal static partial void ClientAlreadyReconnecting(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Attempting to reset the client")]
    internal static partial void ResettingClient(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Client reset faulted.")]
    internal static partial void ClientResetFaulted(this ILogger logger, Exception? exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Client reset was cancelled.")]
    internal static partial void ClientResetCancelled(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Client reset successfully.")]
    internal static partial void ClientResetSucceeded(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No guild id given and no default guild id set!")]
    internal static partial void NoGuildIdGiven(this ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No such guild {GuildId}")]
    internal static partial void NoSuchGuild(this ILogger logger, ulong guildId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No such {ChannelKind} {ChannelId} on guild {GuildId}")]
    internal static partial void NoSuchChannel(this ILogger logger, string channelKind, ulong channelId, ulong? guildId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No such category {CategoryId}")]
    internal static partial void NoSuchCategory(this ILogger logger, ulong categoryId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No such role {RoleId} on guild {GuildId}")]
    internal static partial void NoSuchRole(this ILogger logger, ulong roleId, ulong? guildId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No such emote {EmoteName} on guild {GuildId}")]
    internal static partial void NoSuchEmote(this ILogger logger, string emoteName, ulong? guildId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get SocketGuildUser {UserId}")]
    internal static partial void GuildUserNotFound(this ILogger logger, ulong userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get user {UserId} from Discord API")]
    internal static partial void UserNotFoundInApi(this ILogger logger, ulong userId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to fetch user {UserId} from API")]
    internal static partial void UserFetchFailed(this ILogger logger, Exception exception, ulong userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get SocketGuildOwner")]
    internal static partial void GuildOwnerNotFound(this ILogger logger);
}
