namespace HelperLib.Discord.Tests;

/// <summary>
/// Tests in this collection capture Console output or touch static state (<c>IonicHelper.Client</c>,
/// the default guild id in <c>GuildLookup</c>), so they must not run in parallel with each other.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ConsoleAndStaticStateCollection
{
    public const string Name = "ConsoleAndStaticState";
}
