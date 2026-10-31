using System.Linq.Expressions;
using Discord;
using Moq;

namespace HelperLib.Discord.Tests.Mocks;

public static class DiscordMockBuilder
{
    public static Mock<ITextChannel> CreateTextChannel() => CreateMessageChannel<ITextChannel>();

    public static Mock<IMessageChannel> CreateMessageChannel() => CreateMessageChannel<IMessageChannel>();

    public static Mock<IDMChannel> CreateDMChannel() => CreateMessageChannel<IDMChannel>();

    public static IReadOnlyCollection<IRole> CreateRoles(params ulong[] roleIds) =>
        roleIds.Select(CreateRole).ToArray();

    /// <summary>Verifies that exactly one message with empty text and a matching embed was sent.</summary>
    public static void VerifyEmbedSentOnce<TChannel>(this Mock<TChannel> channel, Expression<Func<Embed, bool>> embed)
        where TChannel : class, IMessageChannel =>
        channel.Verify(c => c.SendMessageAsync(
            string.Empty,
            false,
            It.Is(embed),
            It.IsAny<RequestOptions>(),
            It.IsAny<AllowedMentions>(),
            It.IsAny<MessageReference>(),
            It.IsAny<MessageComponent>(),
            It.IsAny<ISticker[]>(),
            It.IsAny<Embed[]>(),
            It.IsAny<MessageFlags>()), Times.Once);

    /// <summary>Makes <c>SendFileAsync</c> succeed and hands the sent bytes to <paramref name="onFileSent"/>.</summary>
    public static void SetupSendFile<TChannel>(this Mock<TChannel> channel, Action<byte[]> onFileSent)
        where TChannel : class, IMessageChannel =>
        channel.Setup(c => c.SendFileAsync(
                It.IsAny<Stream>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<Embed>(),
                It.IsAny<RequestOptions>(),
                It.IsAny<bool>(),
                It.IsAny<AllowedMentions>(),
                It.IsAny<MessageReference>(),
                It.IsAny<MessageComponent>(),
                It.IsAny<ISticker[]>(),
                It.IsAny<Embed[]>(),
                It.IsAny<MessageFlags>()))
            .Callback(new InvocationAction(invocation =>
            {
                using var copy = new MemoryStream();
                ((Stream)invocation.Arguments[0]!).CopyTo(copy);
                onFileSent(copy.ToArray());
            }))
            .ReturnsAsync(new Mock<IUserMessage>().Object);

    public static void VerifyNoFileSent<TChannel>(this Mock<TChannel> channel)
        where TChannel : class, IMessageChannel =>
        VerifyFileSent(channel, Times.Never(), _ => true, _ => true);

    /// <summary>Verifies that exactly one file with the given name was sent, cancellable by <paramref name="cancelToken"/>.</summary>
    public static void VerifyFileSentOnce<TChannel>(
        this Mock<TChannel> channel,
        string fileName,
        CancellationToken cancelToken)
        where TChannel : class, IMessageChannel =>
        VerifyFileSent(channel, Times.Once(), name => name == fileName, options => options.CancelToken == cancelToken);

    private static void VerifyFileSent<TChannel>(
        Mock<TChannel> channel,
        Times times,
        Expression<Func<string, bool>> fileName,
        Expression<Func<RequestOptions, bool>> options)
        where TChannel : class, IMessageChannel =>
        channel.Verify(c => c.SendFileAsync(
            It.IsAny<Stream>(),
            It.Is(fileName),
            It.IsAny<string>(),
            It.IsAny<bool>(),
            It.IsAny<Embed>(),
            It.Is(options),
            It.IsAny<bool>(),
            It.IsAny<AllowedMentions>(),
            It.IsAny<MessageReference>(),
            It.IsAny<MessageComponent>(),
            It.IsAny<ISticker[]>(),
            It.IsAny<Embed[]>(),
            It.IsAny<MessageFlags>()), times);

    private static Mock<TChannel> CreateMessageChannel<TChannel>()
        where TChannel : class, IMessageChannel
    {
        var mock = new Mock<TChannel>();
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
            .ReturnsAsync(new Mock<IUserMessage>().Object);

        return mock;
    }

    private static IRole CreateRole(ulong roleId)
    {
        var role = new Mock<IRole>();
        role.Setup(r => r.Id).Returns(roleId);
        return role.Object;
    }
}
