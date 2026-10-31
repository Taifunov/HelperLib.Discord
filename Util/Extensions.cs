using Discord;

namespace HelperLib.Discord.Util;

/// <summary>
/// Extension methods for sending embeds and images to Discord channels and users.
/// </summary>
public static class Extensions
{
    private static readonly ImageDownloader ImageDownloader = new();

    /// <summary>Sends an embed to a text channel.</summary>
    /// <param name="channel">The destination.</param>
    /// <param name="e">The embed to send.</param>
    /// <returns>The sent message.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="channel"/> or <paramref name="e"/> is <see langword="null"/>.</exception>
    public static async Task<IUserMessage> SendEmbedAsync(this ITextChannel channel, Embed e) =>
        await SendEmbedAsync((IMessageChannel)channel, e);

    /// <summary>Builds an embed and sends it to a text channel.</summary>
    /// <param name="channel">The destination.</param>
    /// <param name="eb">The embed builder to build and send.</param>
    /// <returns>The sent message.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="eb"/> or <paramref name="channel"/> is <see langword="null"/>.</exception>
    public static async Task<IUserMessage> SendEmbedAsync(this ITextChannel channel, EmbedBuilder eb)
    {
        ArgumentNullException.ThrowIfNull(eb);
        return await SendEmbedAsync(channel, eb.Build());
    }

    /// <summary>Sends an embed to a message channel.</summary>
    /// <param name="channel">The destination.</param>
    /// <param name="e">The embed to send.</param>
    /// <returns>The sent message.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="channel"/> or <paramref name="e"/> is <see langword="null"/>.</exception>
    public static async Task<IUserMessage> SendEmbedAsync(this IMessageChannel channel, Embed e)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(e);
        return await channel.SendMessageAsync(string.Empty, false, e);
    }

    /// <summary>Builds an embed and sends it to a message channel.</summary>
    /// <param name="channel">The destination.</param>
    /// <param name="eb">The embed builder to build and send.</param>
    /// <returns>The sent message.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="eb"/> or <paramref name="channel"/> is <see langword="null"/>.</exception>
    public static async Task<IUserMessage> SendEmbedAsync(this IMessageChannel channel, EmbedBuilder eb)
    {
        ArgumentNullException.ThrowIfNull(eb);
        return await SendEmbedAsync(channel, eb.Build());
    }

    /// <summary>Builds an embed and sends it to a user as a direct message.</summary>
    /// <param name="user">The destination.</param>
    /// <param name="eb">The embed builder to build and send.</param>
    /// <returns>The sent message.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="eb"/> or <paramref name="user"/> is <see langword="null"/>.</exception>
    public static async Task<IUserMessage> SendEmbedAsync(this IUser user, EmbedBuilder eb)
    {
        ArgumentNullException.ThrowIfNull(eb);
        return await SendEmbedAsync(user, eb.Build());
    }

    /// <summary>Sends an embed to a user as a direct message.</summary>
    /// <param name="user">The destination.</param>
    /// <param name="e">The embed to send.</param>
    /// <returns>The sent message.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="user"/> or <paramref name="e"/> is <see langword="null"/>.</exception>
    public static async Task<IUserMessage> SendEmbedAsync(this IUser user, Embed e)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(e);
        return await user.SendMessageAsync(string.Empty, false, e);
    }

    /// <summary>Builds an embed and sends it to a direct message channel.</summary>
    /// <param name="channel">The destination.</param>
    /// <param name="eb">The embed builder to build and send.</param>
    /// <returns>The sent message.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="eb"/> or <paramref name="channel"/> is <see langword="null"/>.</exception>
    public static async Task<IUserMessage> SendEmbedAsync(this IDMChannel channel, EmbedBuilder eb)
    {
        ArgumentNullException.ThrowIfNull(eb);
        return await SendEmbedAsync(channel, eb.Build());
    }

    /// <summary>Sends an embed to a direct message channel.</summary>
    /// <param name="channel">The destination.</param>
    /// <param name="e">The embed to send.</param>
    /// <returns>The sent message.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="channel"/> or <paramref name="e"/> is <see langword="null"/>.</exception>
    public static async Task<IUserMessage> SendEmbedAsync(this IDMChannel channel, Embed e) =>
        await SendEmbedAsync((IMessageChannel)channel, e);

    /// <summary>
    /// Downloads an image and uploads it to a channel as <c>image.png</c>. Only HTTPS URLs on
    /// <c>cdn.discordapp.com</c> or <c>media.discordapp.net</c> are accepted; redirects are not
    /// followed, and the download is limited to 8 MiB and 30 seconds.
    /// </summary>
    /// <param name="channel">The channel to send to.</param>
    /// <param name="url">The image URL.</param>
    /// <returns>The sent message.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="channel"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="url"/> is empty, whitespace or not an allowed Discord CDN URL.</exception>
    /// <exception cref="HttpRequestException">The download failed or returned an error status.</exception>
    /// <exception cref="TaskCanceledException">The download took longer than 30 seconds.</exception>
    public static async Task<IUserMessage> SendImageAsync(this IMessageChannel channel, string url)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        return await SendImageAsync(channel, url, ImageDownloader);
    }

    // Takes the downloader as a parameter so tests can send an image without a network call.
    internal static async Task<IUserMessage> SendImageAsync(IMessageChannel channel, string url,
        ImageDownloader imageDownloader)
    {
        var bytes = await imageDownloader.DownloadAsync(url);
        await using var stream = new MemoryStream(bytes, writable: false);

        return await channel.SendFileAsync(stream, "image.png");
    }
}
