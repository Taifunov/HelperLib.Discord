# HelperLib.Discord

`HelperLib.Discord` is a small utility library for Discord bots built with Discord.NET. It provides convenience helpers for embeds, guild lookups, role checks, command preconditions, and a reconnect watchdog. It works through dependency injection in a generic host.

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

- Breaking: the library works only through dependency injection. Register it with `AddHelperLibDiscord`. `IonicHelper`, the static `IonicHelper.Client`, the static `GuildLookup` members, the `RoleChecks` overloads that take a guild id and a user id, and the console logger are removed. See "Migration from 1.x".
- Breaking: `GuildLookup` is a sealed class that you inject. Its lookups are instance methods. It moved from the `HelperLib.Discord.Util` namespace to the root `HelperLib.Discord` namespace.
- Breaking: removed the unused `Services.ReliabilityService`.
- Breaking: removed `Util.MentionUtils`. Use `Discord.MentionUtils` from Discord.Net, which produces the same output.
- Breaking: the `roleIds` parameters of `RoleChecks` changed from `params ulong[]` to `params ReadOnlySpan<ulong>`. The change is source-compatible but binary-breaking, so consumers must recompile against 2.0.0.
- Breaking: log output goes through the host's `Microsoft.Extensions.Logging.ILogger<T>`. The library no longer writes to the console.
- Breaking: the public `SendEmbedAsync` and `SendImageAsync` methods now throw `ArgumentNullException` and `ArgumentException` for null or blank arguments at the call instead of from the returned task. An `ArgumentException` for an image URL that is not on Discord's CDN still arrives through the returned task.
- Added: `AddHelperLibDiscord` registers `GuildLookup`, `HelperLibDiscordOptions` and the reconnect watchdog as a hosted service.
- Added: `HelperLibDiscordOptions` with `DefaultGuildId`, `ReconnectTimeout`, `ShutdownGracePeriod` and `HeartbeatTimeout`. The host validates the options when it starts.
- Added: heartbeat liveness check. The watchdog ends the process when the connected client sends no `LatencyUpdated` or `Connected` event for `HeartbeatTimeout`.
- Added: all watchdog timing uses `TimeProvider`, so tests can control the clock.
- Added: `GuildLookup.GetGuildUserAsync` and `GetGuildUsersByIdAsync` accept an optional trailing `CancellationToken`.
- Added: `SendImageAsync` accepts an optional trailing `CancellationToken` that cancels the download and the upload. The change is source-compatible but binary-breaking, so consumers must recompile against 2.0.0.
- Added: new package dependencies `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Options`, `Microsoft.Extensions.Hosting.Abstractions` and `Microsoft.Extensions.Logging.Abstractions`.
- Added: NuGet package metadata (authors, description, tags, project URL, repository) and a symbol package (`.snupkg`) with SourceLink, so debuggers can step into the library source.
- Changed: when the watchdog gives up, it ends the process in steps instead of calling `Environment.Exit(1)` at once. It starts the `ShutdownGracePeriod` timer, writes a Critical log entry, sets `Environment.ExitCode` to 1 and calls `IHostApplicationLifetime.StopApplication()`. If the process is still running when the timer ends, it calls `Environment.Exit(1)`. A failure of `StopApplication()` is logged and does not prevent the exit.
- Changed: the library awaits with `ConfigureAwait(false)`, so it no longer resumes on the caller's `SynchronizationContext`. Code that relies on resuming on a UI or request context after a library call must marshal back itself.
- Changed: `GuildLookup.GetEmote` matches names with `OrdinalIgnoreCase` (was `InvariantCultureIgnoreCase`).
- Changed: the `out` parameters of the `GuildLookup` lookups carry `[NotNullWhen(true)]`, so callers need no `!` after a successful lookup.
- Fixed: the reconnect watchdog now terminates the process as soon as the restart task faults while it waits for the client to reconnect, instead of waiting for the timeout.
- Fixed: overlapping disconnect checks no longer end the process while the client is reconnecting; only one check runs at a time. A pending check of a stopped watchdog no longer restarts the client or ends the process. A faulted restart task that is ignored because the client reconnected no longer raises an unobserved task exception.
- Fixed: the watchdog no longer drops a Disconnected event that follows a Connected event while the cancelled check is still unwinding. An exception in the disconnect handler is logged instead of lost.
- Fixed: `SendImageAsync` no longer throws `InvalidCastException` for DM and other non-text channels.
- Fixed: `GuildLookup` methods called without a guild id and without a default guild id now fail with a logged warning instead of looking up guild `0`. `GetGuildUserAsync` no longer swallows cancellation.
- Fixed: `GetGuildOwner` and `GetGuildAdmins` no longer log a second warning when the guild is missing; the guild lookup already logs it.
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

- Guild/channel/user lookup methods live on `GuildLookup` in the root `HelperLib.Discord` namespace.
- Role checks live on `Util.RoleChecks`.
- `SendImageAsync` accepts only HTTPS URLs on `cdn.discordapp.com` and
  `media.discordapp.net`, rejects redirects and unsuccessful responses, and
  limits downloads to 8 MiB and 30 seconds. For other sources, obtain a stream
  using your application's trusted download policy and call Discord.NET's
  `SendFileAsync` directly.

## Migration from 1.x

Version 2.0.0 works only through dependency injection. Run your bot in a generic host.

| 1.x | 2.0.0 |
| --- | --- |
| `new IonicHelper(id).AttachToExistingClient(client, id)` | `services.AddHelperLibDiscord(o => o.DefaultGuildId = id)`. The host registers the `DiscordSocketClient`. |
| `IonicHelper.RunAsync` | The host logs in and starts the client, for example with Discord.Addons.Hosting. |
| `GuildLookup.X(...)` (static) | Inject `GuildLookup` and call `lookup.X(...)`. |
| `IonicHelper.Client` | Inject `DiscordSocketClient`. |
| `IonicHelper.GetCancellationTokenSource()` | Inject `IHostApplicationLifetime`. Call `StopApplication()` or use the `ApplicationStopping` token. |
| `RoleChecks.HasRolesAny(guildId, userId, ...)` | `lookup.GetGuildUserById(guildId, userId, out var user) && RoleChecks.HasRolesAny(user, ...)` |
| `Services.ReliabilityService` | Removed. The reconnect watchdog is part of `AddHelperLibDiscord`. |
| `Util.MentionUtils` | `Discord.MentionUtils` from Discord.Net. |
| `ConsoleLogger` and console output | Removed. Configure logging in the host. |

## Features

- `DependencyInjection.ServiceCollectionExtensions.AddHelperLibDiscord`: Registers the library in a host.
- `HelperLibDiscordOptions`: Settings for the default guild and the watchdog timing.
- `GuildLookup`: Injected guild/channel/role/emote/user lookup helpers.
- `Util.RoleChecks`: Static role-membership check helpers.
- `Util.Extensions`: Extension helpers for sending `Embed`/`EmbedBuilder` and sending images from URLs.
- `Preconditions.RequireRoleAttribute`: Discord.Commands precondition attribute that restricts usage to a role id.
- `Models.GuildUsersResult`: Lightweight result type for guild user enumeration.
- Reconnect watchdog: A hosted service that backstops Discord.Net's reconnect logic.

## Usage

The host registers the `DiscordSocketClient`. Then you add the library. This example uses [Discord.Addons.Hosting](https://github.com/Hawxy/Discord.Addons.Hosting) for the client:

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDiscordHost((config, _) =>
{
    config.Token = builder.Configuration["Discord:Token"]!;
    config.SocketConfig = new DiscordSocketConfig();
});

builder.Services.AddHelperLibDiscord(options =>
{
    options.DefaultGuildId = 123456789012345678UL;
});

await builder.Build().RunAsync();
```

The host must provide `ILogger<T>` and `IHostApplicationLifetime`. The generic host does both.

Inject `GuildLookup` into your services:

```csharp
public sealed class WelcomeService(GuildLookup lookup)
{
    public async Task SendWelcomeAsync(ulong channelId)
    {
        // A null guild id means the default guild from the options.
        if (lookup.GetTextChannel(null, channelId, out var channel))
        {
            await channel.SendEmbedAsync(new EmbedBuilder().WithTitle("Welcome").Build());
        }
    }
}
```

The synchronous lookups read the client's cache. When the target is missing, they return `false` and log a warning.

### Options

| Option | Default | Meaning |
| --- | --- | --- |
| `DefaultGuildId` | none | The guild that lookups use when a call passes no guild id. Without it, such a call finds nothing and logs a warning. `GuildLookup` reads it once, when it is created. |
| `ReconnectTimeout` | 1 minute | How long the client may stay disconnected, and how long a restarted client may take to reconnect, before the watchdog ends the process. |
| `ShutdownGracePeriod` | 30 seconds | How long the host gets to stop gracefully before the process is ended forcibly. |
| `HeartbeatTimeout` | 3 minutes | How long the connected client may stay silent before the watchdog treats the connection as lost. |

All timeouts must be positive and at most 49.7 days (the limit of `Task.Delay`). The host validates the options when it starts and fails to start if one is invalid.

### Reconnect watchdog

The watchdog is a backstop for Discord.Net's own `ConnectionManager`. It acts only when the client is truly stuck.

- After a `Disconnected` event, it waits for `ReconnectTimeout`. A `Connected` event in this time cancels the check.
- It restarts the client with `StartAsync()` only when the state is `Disconnected`. Discord.Net throws for any other state.
- If the state is `Connecting` or `Disconnecting`, it waits up to `ReconnectTimeout` for the state to settle.
- After a restart, it waits up to `ReconnectTimeout` for the state `Connected`.
- It ends the process when the client stays in `Connecting` or `Disconnecting`, when the restarted client does not reach `Connected`, or when the restart task faults.
- Heartbeat liveness: while the client is `Connected`, a `LatencyUpdated` or `Connected` event must arrive within `HeartbeatTimeout`. The check pauses while the client is disconnected.

To end the process, the watchdog does these steps in order:

1. It starts the `ShutdownGracePeriod` timer, so nothing below can prevent the hard exit.
2. It writes a Critical log entry.
3. It sets `Environment.ExitCode` to 1.
4. It calls `IHostApplicationLifetime.StopApplication()`. A failure here is logged and ignored.
5. When the timer ends and the process is still running, it calls `Environment.Exit(1)`.

Only the first trigger ends the process. A process supervisor, such as a Docker restart policy or systemd, must restart the process.

### Other helpers

Parse a user mention with Discord.Net's `MentionUtils`:

```csharp
if (MentionUtils.TryParseUser("<@!123456>", out var userId))
{
    // use userId
}
```

Require a role on a command:

```csharp
[RequireRole(123456789012345678)]
public async Task MyCommand()
{
    // command body
}
```

Check the roles of a user that you found with the injected `GuildLookup`:

```csharp
if (lookup.GetGuildUserById(guildId, userId, out var user)
    && RoleChecks.HasRolesAll(user, moderatorRoleId, trustedRoleId))
{
    // user has both roles
}
```

Fetch a guild user with REST fallback:

```csharp
var user = await lookup.GetGuildUserAsync(guildId, userId);
```

## Notes

- `HelperLib.Discord` is a convenience layer over Discord.NET.
- The host registers one `DiscordSocketClient`. The lookups and the watchdog use that client.
- If the client does not recover, the watchdog ends the process. The host environment must restart it.

## Testing

Run the test suite:

```powershell
dotnet test HelperLib.Discord.Tests\HelperLib.Discord.Tests.csproj --no-restore -v q
```

Run tests with coverage:

```powershell
dotnet test HelperLib.Discord.Tests\HelperLib.Discord.Tests.csproj --no-restore --collect:"XPlat Code Coverage" -v q
```

The integration tests in `HelperLib.Discord.Tests/Integration` run the guild lookups against a real Discord guild. They are skipped unless a bot token is set, so a plain `dotnet test` makes no network call. They are local only, use a dev bot token, and are not run in CI by design. The bot needs the privileged Server Members Intent in the Discord developer portal; without it the member download times out after 60 s and the tests fail with a message that names the intent. To run them:

```powershell
dotnet user-secrets set "HelperLibDiscord:Token" "<token>" --project HelperLib.Discord.Tests
dotnet test HelperLib.Discord.Tests\HelperLib.Discord.Tests.csproj --no-restore --filter "FullyQualifiedName~Integration"
```

You can also set the token in the `HELPERLIB_DISCORD_TOKEN` environment variable, and the guild id in `HELPERLIB_DISCORD_GUILD_ID` or the user secret `HelperLibDiscord:GuildId`.

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
