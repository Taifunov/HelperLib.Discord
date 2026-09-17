using global::Discord;
using global::Discord.WebSocket;

namespace HelperLib.Discord.Tests;

public class IonicHelperTests
{
    [Fact]
    public async Task RunAsync_throws_when_token_is_empty()
    {
        var helper = new IonicHelper(string.Empty);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            helper.RunAsync(new DiscordSocketConfig(), _ => Task.CompletedTask));

        Assert.Equal("Incorrect token! Pass valid one into IonicHelper.ctor", exception.Message);
    }

    [Fact]
    public void AttachToExistingClient_setsClientAndCancellationTokenSource()
    {
        using var client = new DiscordSocketClient(new DiscordSocketConfig());
        var helper = new IonicHelper();

        helper.AttachToExistingClient(client);

        Assert.Same(client, IonicHelper.Client);
        Assert.Equal(ConnectionState.Disconnected, client.ConnectionState);
        Assert.NotNull(IonicHelper.GetCancellationTokenSource());
    }
}
