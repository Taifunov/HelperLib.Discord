using global::Discord;
using HelperLib.Discord.Models;
using Moq;

namespace HelperLib.Discord.Tests.Models;

public class GuildUsersResultTests
{
    [Fact]
    public void Constructor_stores_success_state_and_users()
    {
        var users = new List<IGuildUser> { new Mock<IGuildUser>().Object };

        var result = new GuildUsersResult(true, users);

        Assert.True(result.Success);
        Assert.Same(users, result.Users);
    }
}
