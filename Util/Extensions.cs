using global::Discord;

namespace HelperLib.Discord.Util;

public static class Extensions
{
    private static readonly ImageDownloader ImageDownloader = new();

    public static async Task<IUserMessage> SendEmbedAsync(this ITextChannel channel, Embed e) =>
        await SendEmbedAsync((IMessageChannel)channel, e);

    public static async Task<IUserMessage> SendEmbedAsync(this ITextChannel channel, EmbedBuilder eb) =>
        await SendEmbedAsync(channel, eb.Build());

    public static async Task<IUserMessage> SendEmbedAsync(this IMessageChannel channel, Embed e) =>
        await channel.SendMessageAsync(string.Empty, false, e);

    public static async Task<IUserMessage> SendEmbedAsync(this IMessageChannel channel, EmbedBuilder eb) =>
        await SendEmbedAsync(channel, eb.Build());

    public static async Task<IUserMessage> SendEmbedAsync(this IUser user, EmbedBuilder eb) =>
        await SendEmbedAsync(user, eb.Build());

    public static async Task<IUserMessage> SendEmbedAsync(this IUser user, Embed e) =>
        await user.SendMessageAsync(string.Empty, false, e);

    public static async Task<IUserMessage> SendEmbedAsync(this IDMChannel channel, EmbedBuilder eb) =>
        await SendEmbedAsync(channel, eb.Build());

    public static async Task<IUserMessage> SendEmbedAsync(this IDMChannel channel, Embed e) =>
        await SendEmbedAsync((IMessageChannel)channel, e);

    private static async Task<IUserMessage> SendImageAsync(this ITextChannel channel, string url,
        string fileName = "image.png")
    {
        var bytes = await ImageDownloader.DownloadAsync(url);
        await using var stream = new MemoryStream(bytes, writable: false);

        return await channel.SendFileAsync(stream, fileName);
    }

    public static async Task<IUserMessage> SendImageAsync(this IMessageChannel channel, string url) =>
        await ((ITextChannel) channel).SendImageAsync(url);
}
