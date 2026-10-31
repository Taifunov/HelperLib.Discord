# HelperLib.Discord

`HelperLib.Discord` is a small utility library for Discord bots built with Discord.NET. It provides convenience helpers for embeds, guild lookups, role checks, command preconditions, and basic reconnect handling.

Current version: `2.0.0`

Target framework: `.NET 10`

## Installation

Install the published package from NuGet:

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

### 2.0.0

Released: in progress

- Breaking: removed the unused `Services.ReliabilityService`. `IonicHelper` keeps its built-in reconnect watchdog.
- Breaking: removed `Util.MentionUtils`. Use `Discord.MentionUtils` from Discord.Net, which produces the same output.
- Breaking: the `roleIds` parameters of `RoleChecks` changed from `params ulong[]` to `params ReadOnlySpan<ulong>`. The change is source-compatible but binary-breaking, so consumers must recompile against 2.0.0.
- Breaking: log output goes through `Microsoft.Extensions.Logging.ILogger` instead of `Console.WriteLine`, and the console text format changed (new wording, no fixed colors except red for errors).
- Breaking: the public `SendEmbedAsync` and `SendImageAsync` methods now throw `ArgumentNullException` and `ArgumentException` for null or blank arguments at the call instead of from the returned task. An `ArgumentException` for an image URL that is not on Discord's CDN still arrives through the returned task.
- Breaking: `GuildLookup.GetGuildUserAsync` and `GetGuildUsersByIdAsync` gained an optional trailing `CancellationToken`. The change is source-compatible but binary-breaking, so consumers must recompile against 2.0.0.
- Breaking: `IonicHelper` is now `sealed`. Code that derives from it must use composition instead.
- Fixed: the reconnect watchdog now terminates the process as soon as the restart task faults while it waits for the client to reconnect, instead of waiting for the timeout.
- Fixed: overlapping disconnect checks no longer end the process while the client is reconnecting; only one check runs at a time. A disposed `IonicHelper`'s pending check no longer restarts the client or ends the process. A faulted restart task that is ignored because the client reconnected no longer raises an unobserved task exception.
- Fixed: `SendImageAsync` no longer throws `InvalidCastException` for DM and other non-text channels. The public `SendEmbedAsync` and `SendImageAsync` methods now throw `ArgumentNullException` or `ArgumentException` for null or blank arguments.
- Fixed: attaching `IonicHelper` to a client again unsubscribes the previous client's watchdog handlers instead of doubling them. The watchdog's reconnect token source is now thread-safe, and an exception in the disconnect handler is logged instead of lost. `RunAsync` and `AttachToExistingClient` now throw `ArgumentNullException` for null `config`, `logger` and `client`.
- Fixed: `GuildLookup` methods called without a guild id and without a default guild id now fail with a logged warning instead of looking up guild `0`. `GetGuildUserAsync` and `GetGuildUsersByIdAsync` accept an optional `CancellationToken`, and `GetGuildUserAsync` no longer swallows cancellation.
- Added: the package depends on `Microsoft.Extensions.Logging.Abstractions` (new transitive dependency). `IonicHelper` constructors accept an optional trailing `ILogger`; the helper, its reconnect watchdog and `GuildLookup` log structured messages with constant templates to it. Without a logger, the output stays on the console. `GuildLookup` is static, so it uses the logger of the last `IonicHelper` constructed with one. The change is source-compatible but binary-breaking, so consumers must recompile against 2.0.0.
- Added: `SendImageAsync` accepts an optional trailing `CancellationToken` that cancels the download and the upload. The change is source-compatible but binary-breaking, so consumers must recompile against 2.0.0.
- Added: `IonicHelper` implements `IDisposable`. `Dispose()` unsubscribes the reconnect watchdog from the attached client, cancels any pending reconnect wait and releases the watchdog's resources; it does not dispose the client. `RunAsync` and `AttachToExistingClient` throw `ObjectDisposedException` after `Dispose()`.
- Added: NuGet package metadata (authors, description, tags, project URL, repository) and a symbol package (`.snupkg`) with SourceLink, so debuggers can step into the library source.
- Fixed: thread-safety. `GuildLookup` reads its default guild id and logger as one snapshot per call, `IonicHelper.RunAsync`, `AttachToExistingClient` and `Dispose` are serialized (`RunAsync` disposes the client it created when `Dispose` ran during login or when login fails), and the watchdog no longer drops a Disconnected event that follows a Connected event while the cancelled check is still unwinding. `IonicHelper.Client` and `GuildLookup` are process-wide, so create one helper per process.
- Changed: the library awaits with `ConfigureAwait(false)`, so it no longer resumes on the caller's `SynchronizationContext`. Code that relies on resuming on a UI or request context after a library call must marshal back itself.
- Changed: `GuildLookup.GetEmote` matches names with `OrdinalIgnoreCase` (was `InvariantCultureIgnoreCase`).
- Build: warnings are now treated as errors, and the package ships its XML documentation.

### 1.0.0

Released: 2026-09-17

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

Parse a user mention with Discord.Net's `MentionUtils`:

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

## Code style

The root `.editorconfig` sets the formatting, naming and analyzer rules for all C# code. Visual Studio, Rider and VS Code (C# Dev Kit) apply it while you edit.

- Rules with severity `error` that the compiler or the .NET analyzers report (for example CS4014, CS8603, CA2016, CA2254) fail `dotnet build`.
- Code-style rules (file-scoped namespaces, naming, `readonly` fields, unused and unordered usings, whitespace) are not checked by the build. Check them with `dotnet format`.

Before you open a pull request, run:

```bash
dotnet format HelperLib.Discord.sln --verify-no-changes   # check only; fails on any rule violation
dotnet format HelperLib.Discord.sln                        # fix what can be fixed automatically
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
   and dated changelog, builds, tests, packs, publishes to NuGet, and tags that
   commit. It then creates a GitHub Release with automatically generated notes
   and the `.nupkg` attached under Assets.

Publishing uses GitHub OIDC to obtain a short-lived NuGet credential. No
long-lived NuGet API key is stored in GitHub. Publishing is manually dispatched;
merging a PR does not publish a package. A failed tag push can be retried on the
same commit; an existing version tag pointing at a different commit is rejected.
GitHub Release creation can also be retried: missing assets are uploaded without
overwriting existing ones, and an interrupted draft is published after upload.
Packages are hosted on NuGet.org, not GitHub Packages. Check the `Release package`
workflow, the repository's Releases page, and NuGet.org to verify publication.

The repository, NuGet package, assembly, and root namespace are all named
`HelperLib.Discord`.

## Contributing

Keep changes focused and add tests for behavior changes.

## License

Licensed under the [MIT License](LICENSE).
