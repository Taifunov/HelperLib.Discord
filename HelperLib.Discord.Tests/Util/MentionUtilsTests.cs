using HelperLib.Discord.Util;

namespace HelperLib.Discord.Tests.Util;

public class MentionUtilsTests
{
    [Theory]
    [InlineData(123UL, "<@123>")]
    [InlineData(0UL, "<@0>")]
    public void MentionUser_formats_user_id(ulong userId, string expected)
    {
        var mention = MentionUtils.MentionUser(userId);

        Assert.Equal(expected, mention);
    }

    [Theory]
    [InlineData(456UL, "<#456>")]
    [InlineData(0UL, "<#0>")]
    public void MentionChannel_formats_channel_id(ulong channelId, string expected)
    {
        var mention = MentionUtils.MentionChannel(channelId);

        Assert.Equal(expected, mention);
    }

    [Theory]
    [InlineData(789UL, "<@&789>")]
    [InlineData(0UL, "<@&0>")]
    public void MentionRole_formats_role_id(ulong roleId, string expected)
    {
        var mention = MentionUtils.MentionRole(roleId);

        Assert.Equal(expected, mention);
    }

    [Theory]
    [InlineData("<@123>", 123UL)]
    [InlineData("<@!123>", 123UL)]
    [InlineData("<@0>", 0UL)]
    public void TryParseUser_accepts_user_mentions(string text, ulong expectedUserId)
    {
        var parsed = MentionUtils.TryParseUser(text, out var userId);

        Assert.True(parsed);
        Assert.Equal(expectedUserId, userId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("<#123>")]
    [InlineData("<@abc>")]
    [InlineData("<@-1>")]
    [InlineData("<@123")]
    public void TryParseUser_rejects_invalid_mentions(string text)
    {
        var parsed = MentionUtils.TryParseUser(text, out var userId);

        Assert.False(parsed);
        Assert.Equal(0UL, userId);
    }

    [Fact]
    public void ParseUser_returns_user_id_when_mention_is_valid()
    {
        var userId = MentionUtils.ParseUser("<@42>");

        Assert.Equal(42UL, userId);
    }

    [Fact]
    public void ParseUser_throws_when_mention_is_invalid()
    {
        var exception = Assert.Throws<ArgumentException>(() => MentionUtils.ParseUser("42"));

        Assert.Equal("text", exception.ParamName);
    }

    [Fact]
    public void TryParseChannel_accepts_channel_mention()
    {
        var parsed = MentionUtils.TryParseChannel("<#321>", out var channelId);

        Assert.True(parsed);
        Assert.Equal(321UL, channelId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("321")]
    [InlineData("<@321>")]
    [InlineData("<#abc>")]
    public void TryParseChannel_rejects_invalid_mentions(string text)
    {
        var parsed = MentionUtils.TryParseChannel(text, out var channelId);

        Assert.False(parsed);
        Assert.Equal(0UL, channelId);
    }

    [Fact]
    public void ParseChannel_returns_channel_id_when_mention_is_valid()
    {
        var channelId = MentionUtils.ParseChannel("<#24>");

        Assert.Equal(24UL, channelId);
    }

    [Fact]
    public void ParseChannel_throws_when_mention_is_invalid()
    {
        var exception = Assert.Throws<ArgumentException>(() => MentionUtils.ParseChannel("24"));

        Assert.Equal("text", exception.ParamName);
    }

    [Fact]
    public void TryParseRole_accepts_role_mention()
    {
        var parsed = MentionUtils.TryParseRole("<@&654>", out var roleId);

        Assert.True(parsed);
        Assert.Equal(654UL, roleId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("654")]
    [InlineData("<@654>")]
    [InlineData("<@&abc>")]
    public void TryParseRole_rejects_invalid_mentions(string text)
    {
        var parsed = MentionUtils.TryParseRole(text, out var roleId);

        Assert.False(parsed);
        Assert.Equal(0UL, roleId);
    }

    [Fact]
    public void ParseRole_returns_role_id_when_mention_is_valid()
    {
        var roleId = MentionUtils.ParseRole("<@&84>");

        Assert.Equal(84UL, roleId);
    }

    [Fact]
    public void ParseRole_throws_when_mention_is_invalid()
    {
        var exception = Assert.Throws<ArgumentException>(() => MentionUtils.ParseRole("84"));

        Assert.Equal("text", exception.ParamName);
    }
}
