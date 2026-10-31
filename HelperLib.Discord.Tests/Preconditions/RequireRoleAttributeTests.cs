using Discord;
using HelperLib.Discord.Preconditions;
using Moq;
using static HelperLib.Discord.Tests.Mocks.DiscordMockBuilder;
using ICommandContext = Discord.Commands.ICommandContext;

namespace HelperLib.Discord.Tests.Preconditions;

public class RequireRoleAttributeTests
{
    [Fact]
    public async Task CheckPermissionsAsync_returns_error_when_user_is_not_a_guild_user()
    {
        var attribute = new RequireRoleAttribute(20);
        var context = new Mock<ICommandContext>();
        context.Setup(c => c.User).Returns(new Mock<IUser>().Object);

        var result = await attribute.CheckPermissionsAsync(context.Object, null!, null!);

        Assert.False(result.IsSuccess);
        Assert.Equal("This command can only be used in a server.", result.ErrorReason);
    }

    [Fact]
    public async Task CheckRolesAsync_returns_success_when_user_has_required_role()
    {
        var attribute = new RequireRoleAttribute(20);

        var result = await attribute.CheckRolesAsync(CreateRoles(10, 20));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CheckRolesAsync_returns_configured_error_when_user_lacks_required_role()
    {
        var attribute = new RequireRoleAttribute(20, "blocked");

        var result = await attribute.CheckRolesAsync(CreateRoles(10));

        Assert.False(result.IsSuccess);
        Assert.Equal("blocked", result.ErrorReason);
    }
}
