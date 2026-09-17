using global::Discord;
using Moq;

namespace HelperLib.Discord.Tests.Mocks;

public static class DiscordMockBuilder
{
    public static Mock<ITextChannel> CreateTextChannel(ulong? channelId = null) =>
        CreateMessageChannel<ITextChannel>(channelId);

    public static Mock<IMessageChannel> CreateMessageChannel(ulong? channelId = null) =>
        CreateMessageChannel<IMessageChannel>(channelId);

    public static Mock<IDMChannel> CreateDMChannel(ulong? channelId = null) =>
        CreateMessageChannel<IDMChannel>(channelId);

    private static Mock<TChannel> CreateMessageChannel<TChannel>(ulong? channelId)
        where TChannel : class, IMessageChannel
    {
        var mock = new Mock<TChannel>();
        if (channelId.HasValue)
        {
            mock.Setup(c => c.Id).Returns(channelId.Value);
        }

        mock.Setup(c => c.SendMessageAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<Embed>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<AllowedMentions>(),
                It.IsAny<MessageReference>(),
                It.IsAny<MessageComponent>(),
                It.IsAny<ISticker[]>(),
                It.IsAny<Embed[]>(),
                It.IsAny<MessageFlags>()))
            .ReturnsAsync(CreateUserMessage().Object);

        return mock;
    }

    private static Mock<IUserMessage> CreateUserMessage() => new();
}
