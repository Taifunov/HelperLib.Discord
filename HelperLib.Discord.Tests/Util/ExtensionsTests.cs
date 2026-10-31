using Discord;
using HelperLib.Discord.Tests.Mocks;
using HelperLib.Discord.Util;
using Moq;

namespace HelperLib.Discord.Tests.Util;

public class ExtensionsTests
{
    [Fact]
    public async Task SendEmbedAsync_with_text_channel_sends_empty_text_and_embed()
    {
        var channel = DiscordMockBuilder.CreateTextChannel();
        var embed = new EmbedBuilder().WithTitle("status").Build();

        var message = await channel.Object.SendEmbedAsync(embed);

        Assert.NotNull(message);
        channel.VerifyEmbedSentOnce(sent => Equals(sent, embed));
    }

    [Fact]
    public async Task SendEmbedAsync_with_text_channel_builder_builds_and_sends_embed()
    {
        var channel = DiscordMockBuilder.CreateTextChannel();
        var builder = new EmbedBuilder().WithTitle("status");

        await channel.Object.SendEmbedAsync(builder);

        channel.VerifyEmbedSentOnce(sent => sent.Title == "status");
    }

    [Fact]
    public async Task SendEmbedAsync_with_message_channel_sends_empty_text_and_embed()
    {
        var channel = DiscordMockBuilder.CreateMessageChannel();
        var embed = new EmbedBuilder().WithDescription("ready").Build();

        await channel.Object.SendEmbedAsync(embed);

        channel.VerifyEmbedSentOnce(sent => Equals(sent, embed));
    }

    [Fact]
    public async Task SendEmbedAsync_with_message_channel_builder_builds_and_sends_embed()
    {
        var channel = DiscordMockBuilder.CreateMessageChannel();
        var builder = new EmbedBuilder().WithDescription("ready");

        await channel.Object.SendEmbedAsync(builder);

        channel.VerifyEmbedSentOnce(sent => sent.Description == "ready");
    }

    [Fact]
    public async Task SendEmbedAsync_with_dm_channel_sends_empty_text_and_embed()
    {
        var channel = DiscordMockBuilder.CreateDMChannel();
        var embed = new EmbedBuilder().WithTitle("dm").Build();

        await channel.Object.SendEmbedAsync(embed);

        channel.VerifyEmbedSentOnce(sent => Equals(sent, embed));
    }

    [Fact]
    public async Task SendEmbedAsync_with_dm_channel_builder_builds_and_sends_embed()
    {
        var channel = DiscordMockBuilder.CreateDMChannel();
        var builder = new EmbedBuilder().WithTitle("dm");

        await channel.Object.SendEmbedAsync(builder);

        channel.VerifyEmbedSentOnce(sent => sent.Title == "dm");
    }

    [Fact]
    public async Task SendEmbedAsync_with_user_sends_empty_text_and_embed()
    {
        var user = new Mock<IUser>();
        var dmChannel = DiscordMockBuilder.CreateDMChannel();
        var embed = new EmbedBuilder().WithTitle("user").Build();
        user.Setup(u => u.CreateDMChannelAsync(It.IsAny<RequestOptions>()))
            .ReturnsAsync(dmChannel.Object);

        await user.Object.SendEmbedAsync(embed);

        dmChannel.VerifyEmbedSentOnce(sent => Equals(sent, embed));
    }

    [Fact]
    public async Task SendEmbedAsync_with_user_builder_builds_and_sends_embed()
    {
        var user = new Mock<IUser>();
        var dmChannel = DiscordMockBuilder.CreateDMChannel();
        var builder = new EmbedBuilder().WithDescription("user");
        user.Setup(u => u.CreateDMChannelAsync(It.IsAny<RequestOptions>()))
            .ReturnsAsync(dmChannel.Object);

        await user.Object.SendEmbedAsync(builder);

        dmChannel.VerifyEmbedSentOnce(sent => sent.Description == "user");
    }

    [Fact]
    public async Task SendImageAsync_rejects_loopback_urls()
    {
        var channel = DiscordMockBuilder.CreateTextChannel();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            channel.Object.SendImageAsync("http://127.0.0.1:0/image.png"));
    }

    [Fact]
    public async Task SendImageAsync_with_dm_channel_validates_url_instead_of_casting_to_text_channel()
    {
        var channel = DiscordMockBuilder.CreateDMChannel();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            ((IMessageChannel)channel.Object).SendImageAsync("https://example.invalid/image.png"));
    }

    [Fact]
    public async Task SendImageAsync_with_dm_channel_sends_downloaded_image_as_file()
    {
        var channel = DiscordMockBuilder.CreateDMChannel();
        using var handler = FakeHttpMessageHandler.Returning([1, 2, 3]);
        using var downloader = new ImageDownloader(handler);
        using var cancellation = new CancellationTokenSource();
        byte[]? sentBytes = null;
        channel.SetupSendFile(bytes => sentBytes = bytes);

        await Extensions.SendImageAsync(channel.Object, "https://cdn.discordapp.com/image.png", downloader,
            cancellation.Token);

        Assert.Equal(new byte[] { 1, 2, 3 }, sentBytes);
        channel.VerifyFileSentOnce("image.png", cancellation.Token);
    }

    [Fact]
    public async Task SendImageAsync_with_cancelled_token_stops_the_download_and_sends_nothing()
    {
        var channel = DiscordMockBuilder.CreateDMChannel();
        using var handler = FakeHttpMessageHandler.Returning([1, 2, 3]);
        using var downloader = new ImageDownloader(handler);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Extensions.SendImageAsync(channel.Object, "https://cdn.discordapp.com/image.png", downloader,
                cancellation.Token));

        channel.VerifyNoFileSent();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SendImageAsync_rejects_missing_url_at_the_call(string? url)
    {
        var channel = DiscordMockBuilder.CreateMessageChannel();

        Assert.ThrowsAny<ArgumentException>(() => { _ = channel.Object.SendImageAsync(url!); });
    }

    [Fact]
    public void SendImageAsync_rejects_null_channel_at_the_call()
    {
        IMessageChannel channel = null!;

        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = channel.SendImageAsync("https://cdn.discordapp.com/image.png");
        });
    }

    [Fact]
    public void SendEmbedAsync_rejects_null_embed_at_the_call()
    {
        var channel = DiscordMockBuilder.CreateMessageChannel();

        Assert.Throws<ArgumentNullException>(() => { _ = channel.Object.SendEmbedAsync((Embed)null!); });
    }

    [Fact]
    public void SendEmbedAsync_rejects_null_embed_builder_at_the_call()
    {
        var channel = DiscordMockBuilder.CreateDMChannel();

        Assert.Throws<ArgumentNullException>(() => { _ = channel.Object.SendEmbedAsync((EmbedBuilder)null!); });
    }

    [Fact]
    public void SendEmbedAsync_rejects_null_user_at_the_call()
    {
        IUser user = null!;

        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = user.SendEmbedAsync(new EmbedBuilder().WithTitle("user").Build());
        });
    }
}
