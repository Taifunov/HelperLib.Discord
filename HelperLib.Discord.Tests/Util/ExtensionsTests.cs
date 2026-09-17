using global::Discord;
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
        channel.Verify(c => c.SendMessageAsync(
            string.Empty,
            false,
            embed,
            It.IsAny<RequestOptions>(),
            It.IsAny<AllowedMentions>(),
            It.IsAny<MessageReference>(),
            It.IsAny<MessageComponent>(),
            It.IsAny<ISticker[]>(),
            It.IsAny<Embed[]>(),
            It.IsAny<MessageFlags>()), Times.Once);
    }

    [Fact]
    public async Task SendEmbedAsync_with_text_channel_builder_builds_and_sends_embed()
    {
        var channel = DiscordMockBuilder.CreateTextChannel();
        var builder = new EmbedBuilder().WithTitle("status");

        await channel.Object.SendEmbedAsync(builder);

        channel.Verify(c => c.SendMessageAsync(
            string.Empty,
            false,
            It.Is<Embed>(e => e.Title == "status"),
            It.IsAny<RequestOptions>(),
            It.IsAny<AllowedMentions>(),
            It.IsAny<MessageReference>(),
            It.IsAny<MessageComponent>(),
            It.IsAny<ISticker[]>(),
            It.IsAny<Embed[]>(),
            It.IsAny<MessageFlags>()), Times.Once);
    }

    [Fact]
    public async Task SendEmbedAsync_with_message_channel_sends_empty_text_and_embed()
    {
        var channel = DiscordMockBuilder.CreateMessageChannel();
        var embed = new EmbedBuilder().WithDescription("ready").Build();

        await channel.Object.SendEmbedAsync(embed);

        channel.Verify(c => c.SendMessageAsync(
            string.Empty,
            false,
            embed,
            It.IsAny<RequestOptions>(),
            It.IsAny<AllowedMentions>(),
            It.IsAny<MessageReference>(),
            It.IsAny<MessageComponent>(),
            It.IsAny<ISticker[]>(),
            It.IsAny<Embed[]>(),
            It.IsAny<MessageFlags>()), Times.Once);
    }

    [Fact]
    public async Task SendEmbedAsync_with_message_channel_builder_builds_and_sends_embed()
    {
        var channel = DiscordMockBuilder.CreateMessageChannel();
        var builder = new EmbedBuilder().WithDescription("ready");

        await channel.Object.SendEmbedAsync(builder);

        channel.Verify(c => c.SendMessageAsync(
            string.Empty,
            false,
            It.Is<Embed>(e => e.Description == "ready"),
            It.IsAny<RequestOptions>(),
            It.IsAny<AllowedMentions>(),
            It.IsAny<MessageReference>(),
            It.IsAny<MessageComponent>(),
            It.IsAny<ISticker[]>(),
            It.IsAny<Embed[]>(),
            It.IsAny<MessageFlags>()), Times.Once);
    }

    [Fact]
    public async Task SendEmbedAsync_with_dm_channel_sends_empty_text_and_embed()
    {
        var channel = DiscordMockBuilder.CreateDMChannel();
        var embed = new EmbedBuilder().WithTitle("dm").Build();

        await channel.Object.SendEmbedAsync(embed);

        channel.Verify(c => c.SendMessageAsync(
            string.Empty,
            false,
            embed,
            It.IsAny<RequestOptions>(),
            It.IsAny<AllowedMentions>(),
            It.IsAny<MessageReference>(),
            It.IsAny<MessageComponent>(),
            It.IsAny<ISticker[]>(),
            It.IsAny<Embed[]>(),
            It.IsAny<MessageFlags>()), Times.Once);
    }

    [Fact]
    public async Task SendEmbedAsync_with_dm_channel_builder_builds_and_sends_embed()
    {
        var channel = DiscordMockBuilder.CreateDMChannel();
        var builder = new EmbedBuilder().WithTitle("dm");

        await channel.Object.SendEmbedAsync(builder);

        channel.Verify(c => c.SendMessageAsync(
            string.Empty,
            false,
            It.Is<Embed>(e => e.Title == "dm"),
            It.IsAny<RequestOptions>(),
            It.IsAny<AllowedMentions>(),
            It.IsAny<MessageReference>(),
            It.IsAny<MessageComponent>(),
            It.IsAny<ISticker[]>(),
            It.IsAny<Embed[]>(),
            It.IsAny<MessageFlags>()), Times.Once);
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

        dmChannel.Verify(c => c.SendMessageAsync(
            string.Empty,
            false,
            embed,
            It.IsAny<RequestOptions>(),
            It.IsAny<AllowedMentions>(),
            It.IsAny<MessageReference>(),
            It.IsAny<MessageComponent>(),
            It.IsAny<ISticker[]>(),
            It.IsAny<Embed[]>(),
            It.IsAny<MessageFlags>()), Times.Once);
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

        dmChannel.Verify(c => c.SendMessageAsync(
            string.Empty,
            false,
            It.Is<Embed>(e => e.Description == "user"),
            It.IsAny<RequestOptions>(),
            It.IsAny<AllowedMentions>(),
            It.IsAny<MessageReference>(),
            It.IsAny<MessageComponent>(),
            It.IsAny<ISticker[]>(),
            It.IsAny<Embed[]>(),
            It.IsAny<MessageFlags>()), Times.Once);
    }

    [Fact]
    public async Task SendImageAsync_rejects_loopback_urls()
    {
        var channel = DiscordMockBuilder.CreateTextChannel();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            channel.Object.SendImageAsync("http://127.0.0.1:0/image.png"));
    }

    [Fact]
    public async Task SendImageAsync_throws_when_message_channel_is_not_text_channel()
    {
        var channel = DiscordMockBuilder.CreateMessageChannel();

        await Assert.ThrowsAsync<InvalidCastException>(() =>
            channel.Object.SendImageAsync("https://example.invalid/image.png"));
    }
}
