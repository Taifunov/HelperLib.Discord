namespace HelperLib.Discord.Tests.Integration;

/// <summary>Puts the integration tests in one collection, so they run one at a time against a single logged-in client.</summary>
[CollectionDefinition(Name)]
public sealed class DiscordGuildCollection : ICollectionFixture<DiscordGuildFixture>
{
    public const string Name = "Discord guild";
}
