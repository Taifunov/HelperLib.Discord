# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

# HelperLib.Discord

Discord.Net wrapper and reconnect watchdog. Repository: `HelperLib.Discord`;
NuGet package and namespace: `HelperLib.Discord`.

Target framework: `.NET 10`. Current version: see `<Version>` in `HelperLib.Discord.csproj` (also tracked in `README.md`'s changelog).

## Commands

Build:
```powershell
dotnet build HelperLib.Discord.sln
```

Run all tests:
```powershell
dotnet test HelperLib.Discord.Tests\HelperLib.Discord.Tests.csproj --no-restore
```

Run a single test (by fully qualified name or filter):
```powershell
dotnet test HelperLib.Discord.Tests\HelperLib.Discord.Tests.csproj --no-restore --filter "FullyQualifiedName~CheckStateAsync"
```

Run tests with coverage:
```powershell
dotnet test HelperLib.Discord.Tests\HelperLib.Discord.Tests.csproj --no-restore --collect:"XPlat Code Coverage"
```

Check code style before you report a task done (CI fails on any violation; drop `--verify-no-changes` to fix what can be fixed automatically). The root `.editorconfig` defines the rules, and `dotnet build` does not check the code-style ones:
```powershell
dotnet format HelperLib.Discord.sln --verify-no-changes
```

## Key files

- `IonicHelper.cs` — lifecycle only: constructors, `RunAsync` (login/start), static `Client`, `GetCancellationTokenSource`. Wires Discord.Net's `Connected`/`Disconnected` events to a `Watchdog.ReconnectWatchdog` instance. The public constructors take an optional trailing `ILogger`; it goes to the watchdog and to `GuildLookup.SetLogger`.
- `Watchdog/ReconnectWatchdog.cs` — the reconnect watchdog (`ClientOnConnected` / `ClientOnDisconnected` / `CheckStateAsync`), extracted out of `IonicHelper`. `internal`, constructed and owned by `IonicHelper`. `CheckStateAsync` delegates to `Util/ReconnectStateChecker.cs`.
- `Util/GuildLookup.cs` — static guild/channel/role/emote/user lookup helpers (`GetGuild`, `GetTextChannel`, `GetRole`, etc., plus internal `TryGet*` helpers). Reads `IonicHelper.Client` and a default-guild-id set once from `IonicHelper`'s constructor via `GuildLookup.SetDefaultGuildId`.
- `Util/ConsoleLogger.cs` — internal `ILogger` that writes to the console (Error and Critical in red); the default when the host supplies no logger (`ConsoleLogger.Instance`).
- `Util/Log.cs` — all log messages as `[LoggerMessage]` source-generated extension methods on `ILogger`. Add new messages here with named placeholders; the build fails on `LogX(...)` calls (CA1848/CA1873).
- `Util/RoleChecks.cs` — static role-membership checks (`HasRolesAny`/`HasRolesAll`). The `guildId`+`userId` overloads call into `Util/GuildLookup.cs`.
- `Util/ReconnectGuard.cs` — shared `IsSafeToRestart(ConnectionState)` predicate used by `Util/ReconnectStateChecker.cs`.
- `Util/ReconnectStateChecker.cs` — shared backstop logic (state check, restart, poll for Connected, fail-fast on timeout) used by `Watchdog/ReconnectWatchdog.cs`, parameterized over the caller's state getter, start delegate, delay delegate, `ILogger`, and termination action. Fix reconnect-logic bugs here.
- `Util/Extensions.cs` — extension helpers for sending `Embed`/`EmbedBuilder` and images from URLs. `Util/ImageDownloader.cs` owns the shared HTTP client, restricts downloads to Discord CDN HTTPS URLs, disables redirects, and bounds response size and time.
- `Preconditions/RequireRoleAttribute.cs` — Discord.Commands precondition attribute restricting a command to a role id. Calls `Util/RoleChecks.cs`.
- `Models/GuildUsersResult.cs` — lightweight result type for guild user enumeration.

## Architecture notes

- `IonicHelper` holds a **static** `DiscordSocketClient`; `GuildLookup`'s static lookup helpers only work after `RunAsync` has initialized that client. There is exactly one client per process.
- `Watchdog/ReconnectWatchdog.cs`, used by `IonicHelper`, backstops Discord.Net's own `ConnectionManager`. Its `CheckStateAsync` logic lives in `Util/ReconnectStateChecker.cs`.
- On unrecoverable reconnect failure, the watchdog calls `Environment.Exit(1)` in production (an injected `terminate(string)` callback in tests) so an external process host can restart the bot — this is intentional, not an error to be caught.
- Thread-safety: `IonicHelper` instance state (`_disposed`, the attached subscription) is guarded by an instance `Lock`, so `RunAsync`/`AttachToExistingClient`/`Dispose` are serialized; nothing is awaited under a lock. The static `Client` and token source are replaced together under a static `Lock` and read with `Volatile.Read`.
- `GuildLookup` keeps its default guild id and logger in one immutable `State` record, swapped under a lock; every public method reads `GuildLookup.Current` once and uses that snapshot for the whole call (internal `TryGet*` helpers take the logger).
- The watchdog's single-flight slot is the running check's cancellation token, so a Connected event (which cancels it) frees the slot immediately; a finishing check clears the slot only if it still holds its own token.
- Library awaits use `ConfigureAwait(false)` (including `await foreach`); CA2007 is an error for the library and off for `HelperLib.Discord.Tests`. Use a plain `using` for `MemoryStream`, since `await using var` cannot take `ConfigureAwait`.
- `Client` and `GuildLookup` are process-wide statics: create ONE `IonicHelper` per process (several helpers share one client and the last attach wins).

## Watchdog gotcha

Discord.Net's `ConnectionManager` has its own internal reconnect logic (heartbeat-miss / gateway-reconnect handling). The watchdog only exists as a backstop for when that gets truly stuck.

**Never call `client.StartAsync()` without first checking `ConnectionState == Disconnected`.** Discord.Net's `StartAsync()` throws `InvalidOperationException("Cannot start an already running client.")` for any state other than `Disconnected` — including the transient `Connecting` and `Disconnecting` states. Calling it unconditionally caused spurious production bot restarts: a slow-but-healthy reconnect (routine under network backpressure) was being misdiagnosed as a deadlock and killing the process. Fixed in two layers, both in `Util/ReconnectStateChecker.CheckStateAsync`: it only calls `StartAsync()` when `ConnectionState.Disconnected` (see `Util/ReconnectGuard.IsSafeToRestart`), and while `Connecting`/`Disconnecting` it polls (reusing the same timeout budget, raised from 15s to 1 minute) rather than returning immediately — so a reconnect truly stuck in one of those transient states still fails fast instead of being ignored forever.

`StartAsync()`'s returned `Task` completes once the reconnect attempt is *scheduled*, not once Discord.Net actually reconnects — so the watchdog polls `ConnectionState` for `Connected` after calling it, and fails fast if that doesn't happen within the reset timeout or if the task faults while polling.

## Testing

- xUnit + Moq, no real Gateway/network calls.
- `ReconnectWatchdog` takes constructor-injected fakes (delay functions, `failFast`, `IDiscordClient` via `Mock<IDiscordClient>`) — see `HelperLib.Discord.Tests/Watchdog/ReconnectWatchdogTests.cs` for the pattern.

## Versioning

Bump `<Version>` in `HelperLib.Discord.csproj` and add a changelog entry in `README.md` following its semantic versioning convention (breaking / minor / patch) for any release-worthy change.
