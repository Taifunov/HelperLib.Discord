using Discord;
using HelperLib.Discord.Util;
using Moq;

namespace HelperLib.Discord.Tests.Util;

[Collection(ConsoleAndStaticStateCollection.Name)]
public class RoleChecksTests
{
    [Fact]
    public void HasRolesAny_with_user_id_returns_false_when_guild_is_missing()
    {
        var hasRole = RoleChecks.HasRolesAny(123, 456, 789);

        Assert.False(hasRole);
    }

    [Fact]
    public void HasRolesAll_with_user_id_returns_false_when_guild_is_missing()
    {
        var hasRoles = RoleChecks.HasRolesAll(123, 456, 789);

        Assert.False(hasRoles);
    }

    [Fact]
    public void HasRolesAny_returns_true_when_user_has_any_requested_role()
    {
        var roles = CreateRoles(10, 20);

        var hasRole = RoleChecks.HasRolesAny(roles, 20, 30);

        Assert.True(hasRole);
    }

    [Fact]
    public void HasRolesAny_returns_false_when_user_has_none_of_the_requested_roles()
    {
        var roles = CreateRoles(10, 20);

        var hasRole = RoleChecks.HasRolesAny(roles, 30, 40);

        Assert.False(hasRole);
    }

    [Fact]
    public void HasRolesAll_returns_true_when_user_has_every_requested_role()
    {
        var roles = CreateRoles(10, 20, 30);

        var hasRoles = RoleChecks.HasRolesAll(roles, 10, 30);

        Assert.True(hasRoles);
    }

    [Fact]
    public void HasRolesAll_returns_false_when_user_is_missing_a_requested_role()
    {
        var roles = CreateRoles(10);

        var hasRoles = RoleChecks.HasRolesAll(roles, 10, 20);

        Assert.False(hasRoles);
    }

    [Fact]
    public void HasRolesAny_accepts_an_array_of_role_ids()
    {
        var roles = CreateRoles(10, 20);
        ulong[] requestedRoleIds = [30, 20];

        var hasRole = RoleChecks.HasRolesAny(roles, requestedRoleIds);

        Assert.True(hasRole);
    }

    [Fact]
    public void HasRolesAny_accepts_a_single_role_id()
    {
        var roles = CreateRoles(10, 20);

        Assert.True(RoleChecks.HasRolesAny(roles, 10));
        Assert.False(RoleChecks.HasRolesAny(roles, 30));
    }

    [Fact]
    public void HasRolesAny_returns_false_when_no_role_ids_are_requested()
    {
        var roles = CreateRoles(10, 20);

        var hasRole = RoleChecks.HasRolesAny(roles);

        Assert.False(hasRole);
    }

    [Fact]
    public void HasRolesAll_accepts_an_array_of_role_ids()
    {
        var roles = CreateRoles(10, 20, 30);
        ulong[] requestedRoleIds = [30, 10];

        var hasRoles = RoleChecks.HasRolesAll(roles, requestedRoleIds);

        Assert.True(hasRoles);
    }

    [Fact]
    public void HasRolesAll_accepts_a_single_role_id()
    {
        var roles = CreateRoles(10, 20);

        Assert.True(RoleChecks.HasRolesAll(roles, 10));
        Assert.False(RoleChecks.HasRolesAll(roles, 30));
    }

    [Fact]
    public void HasRolesAll_returns_true_when_no_role_ids_are_requested()
    {
        var roles = CreateRoles(10, 20);

        var hasRoles = RoleChecks.HasRolesAll(roles);

        Assert.True(hasRoles);
    }

    [Fact]
    public void HasRolesAll_returns_true_for_duplicate_requested_role_ids_the_user_holds()
    {
        var roles = CreateRoles(10);

        var hasRoles = RoleChecks.HasRolesAll(roles, 10, 10);

        Assert.True(hasRoles);
    }

    private static IReadOnlyCollection<IRole> CreateRoles(params ulong[] roleIds) =>
        roleIds.Select(CreateRole).ToArray();

    private static IRole CreateRole(ulong roleId)
    {
        var role = new Mock<IRole>();
        role.Setup(r => r.Id).Returns(roleId);
        return role.Object;
    }
}
