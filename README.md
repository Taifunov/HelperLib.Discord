# HelperLib.Discord

`HelperLib.Discord` is a small utility library for Discord bots built with Discord.NET. It provides convenience helpers for mentions, embeds, guild lookups, role checks, command preconditions, and basic reconnect handling.

Current version: `1.0.0`

Target framework: `.NET 10`

## Installation

The first release is being prepared. Once published, install it from NuGet:

```powershell
dotnet add package HelperLib.Discord
```

## Versioning

This package follows semantic versioning from its first release, `1.0.0`:

- `MAJOR` changes for breaking public API or runtime requirements.
- `MINOR` changes for backward-compatible features, behavior fixes, and new helpers.
- `PATCH` changes for backward-compatible bug fixes, documentation, or test-only changes.

Breaking changes must be listed in the changelog below.

## Changelog

### 1.0.0

Released: in progress

Initial release of the `HelperLib.Discord` package:

- Discord client startup and attachment to an existing client.
- Guild lookups, role checks, mentions, embeds, and command preconditions.
- Reconnect monitoring with bounded retries and fail-fast recovery.
- Restricted Discord CDN image downloads with size and time limits.

## Migration from HelperLib

This is a new package with its own version history. Replace the former
`HelperLib` package reference and update namespaces to `HelperLib.Discord`
and its subnamespaces.

- Static guild/channel/user lookup methods live on `Util.GuildLookup`.
- Role checks live on `Util.RoleChecks`.
- `IonicHelper` handles client startup or `AttachToExistingClient`.
- `SendImageAsync` accepts only HTTPS URLs on `cdn.discordapp.com` and
  `media.discordapp.net`, rejects redirects and unsuccessful responses, and
  limits downloads to 8 MiB and 30 seconds. For other sources, obtain a stream
  using your application's trusted download policy and call Discord.NET's
  `SendFileAsync` directly.

## Features

- `IonicHelper`: Lifecycle for a `DiscordSocketClient` — construction, login/start, cancellation token access, and reconnect watchdog wiring.
- `Util.GuildLookup`: Static guild/channel/role/emote/user lookup helpers.
- `Util.RoleChecks`: Static role-membership check helpers.
- `Services.ReliabilityService`: Reconnect/reset strategy for `DiscordSocketClient`.
- `Util.MentionUtils`: Create and parse Discord mentions for users, channels, and roles.
- `Util.Extensions`: Extension helpers for sending `Embed`/`EmbedBuilder` and sending images from URLs.
- `Preconditions.RequireRoleAttribute`: Discord.Commands precondition attribute that restricts usage to a role id.
- `Models.GuildUsersResult`: Lightweight result type for guild user enumeration.

## Quick Start

Install the NuGet package, or reference the project directly during development.

Simple start using `IonicHelper`:

```csharp
var token = "YOUR_BOT_TOKEN";
var defaultGuildId = 123456789012345678UL;

var helper = new IonicHelper(token, defaultGuildId);

await helper.RunAsync(
    new DiscordSocketConfig(),
    message =>
    {
        Console.WriteLine(message);
        return Task.CompletedTask;
    });
```

`Util.GuildLookup` and `Util.RoleChecks`'s static helpers expect `RunAsync` to have initialized `IonicHelper.Client`.

## Examples

Parse a user mention:

```csharp
if (MentionUtils.TryParseUser("<@!123456>", out var userId))
{
    // use userId
}
```

Send an embed:

```csharp
await textChannel.SendEmbedAsync(
    new EmbedBuilder()
        .WithTitle("Hello")
        .Build());
```

Require a role on a command:

```csharp
[RequireRole(123456789012345678)]
public async Task MyCommand()
{
    // command body
}
```

Check roles:

```csharp
if (RoleChecks.HasRolesAll(guildUser, moderatorRoleId, trustedRoleId))
{
    // user has both roles
}
```

Fetch a guild user with REST fallback:

```csharp
var user = await GuildLookup.GetGuildUserAsync(defaultGuildId, userId);
```

## Notes

- `HelperLib.Discord` is a convenience layer over Discord.NET.
- `IonicHelper` stores a static `DiscordSocketClient`, so `GuildLookup`/`RoleChecks`'s static helpers require the client to be initialized.
- Reconnect handling is intentionally simple. If reset attempts fail or time out, the process can exit so an external host can restart the bot.

## Testing

Run the test suite:

```powershell
dotnet test HelperLib.Discord.Tests\HelperLib.Discord.Tests.csproj --no-restore -v q
```

Run tests with coverage:

```powershell
dotnet test HelperLib.Discord.Tests\HelperLib.Discord.Tests.csproj --no-restore --collect:"XPlat Code Coverage" -v q
```

## Releasing

Releases use a checked pull request for version changes and a separate publish
workflow. Neither workflow pushes a release commit directly to the protected
default branch.

1. Configure a nuget.org Trusted Publishing policy for owner `Taifunov`,
   repository `HelperLib.Discord`, workflow `release.yml`, and package
   `HelperLib.Discord`, with no environment. Enable **Push new packages and
   package versions** for the first publication of this new package ID.
   The old repository's policy does not match this new repository.
2. In repository Actions settings, allow GitHub Actions to create pull requests.
3. Add and merge a changelog section for the intended version with
   `Released: in progress`.
4. Run `Prepare release` on the default branch. Choose `current` to finalize
   the initial `1.0.0`, or `major`, `minor`, or `patch` to increment the project
   version. It opens a version PR and explicitly dispatches CI on that branch.
5. Merge the version PR after `build-test-pack` passes. If the base has changed,
   update the release branch and rerun CI before merging.
6. Run `Release package` on the default branch. It validates the merged version
   and dated changelog, builds, tests, packs, publishes, and tags that commit.

Publishing uses GitHub OIDC to obtain a short-lived NuGet credential. No
long-lived NuGet API key is stored in GitHub. Publishing is manually dispatched;
merging a PR does not publish a package. A failed tag push can be retried on the
same commit; an existing version tag pointing at a different commit is rejected.

The repository, NuGet package, assembly, and root namespace are all named
`HelperLib.Discord`.

## Contributing

Keep changes focused and add tests for behavior changes.

## License

Licensed under the [MIT License](LICENSE).
