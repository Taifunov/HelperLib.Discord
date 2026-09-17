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

## Key files

- `IonicHelper.cs` — lifecycle only: constructors, `RunAsync` (login/start), static `Client`, `GetCancellationTokenSource`. Wires Discord.Net's `Connected`/`Disconnected` events to a `Watchdog.ReconnectWatchdog` instance.
- `Watchdog/ReconnectWatchdog.cs` — the reconnect watchdog (`ClientOnConnected` / `ClientOnDisconnected` / `CheckStateAsync`), extracted out of `IonicHelper`. `internal`, constructed and owned by `IonicHelper`. `CheckStateAsync` delegates to `Util/ReconnectStateChecker.cs`.
- `Util/GuildLookup.cs` — static guild/channel/role/emote/user lookup helpers (`GetGuild`, `GetTextChannel`, `GetRole`, etc., plus internal `TryGet*` helpers). Reads `IonicHelper.Client` and a default-guild-id set once from `IonicHelper`'s constructor via `GuildLookup.SetDefaultGuildId`.
- `Util/RoleChecks.cs` — static role-membership checks (`HasRolesAny`/`HasRolesAll`). The `guildId`+`userId` overloads call into `Util/GuildLookup.cs`.
- `Util/ReconnectGuard.cs` — shared `IsSafeToRestart(ConnectionState)` predicate used by `Util/ReconnectStateChecker.cs`.
- `Util/ReconnectStateChecker.cs` — shared backstop logic (state check, restart, poll for Connected, fail-fast on timeout) used by both `Watchdog/ReconnectWatchdog.cs` and `Services/ReliabilityService.cs`, parameterized over each caller's state getter, start delegate, delay delegate, logging, and termination action. Fix reconnect-logic bugs here once; both callers pick it up automatically.
- `Services/ReliabilityService.cs` — a parallel, unused rewrite of the same watchdog concept (not wired up anywhere). Its `CheckStateAsync` shares implementation with `Watchdog/ReconnectWatchdog.cs` via `Util/ReconnectStateChecker.cs`; only the surrounding CTS/event-wiring code still differs between the two.
- `Util/MentionUtils.cs` — create/parse Discord mentions for users, channels, and roles.
- `Util/Extensions.cs` — extension helpers for sending `Embed`/`EmbedBuilder` and images from URLs. `Util/ImageDownloader.cs` owns the shared HTTP client, restricts downloads to Discord CDN HTTPS URLs, disables redirects, and bounds response size and time.
- `Preconditions/RequireRoleAttribute.cs` — Discord.Commands precondition attribute restricting a command to a role id. Calls `Util/RoleChecks.cs`.
- `Models/GuildUsersResult.cs` — lightweight result type for guild user enumeration.

## Architecture notes

- `IonicHelper` holds a **static** `DiscordSocketClient`; `GuildLookup`'s static lookup helpers only work after `RunAsync` has initialized that client. There is exactly one client per process.
- Two reconnect watchdog classes exist side by side (`Watchdog/ReconnectWatchdog.cs`, used by `IonicHelper`, and `Services/ReliabilityService`), both backstopping Discord.Net's own `ConnectionManager`. `ReliabilityService` is not currently wired into any consumer — it exists for future use. Their `CheckStateAsync` logic is shared via `Util/ReconnectStateChecker.cs`, so a bug fix there applies to both automatically; only the CTS/event-wiring code around it still differs (and would need fixing in both places if changed).
- On unrecoverable reconnect failure, the watchdog calls `Environment.Exit(1)` in production (an injected `terminate(string)` callback in tests) so an external process host can restart the bot — this is intentional, not an error to be caught.

## Watchdog gotcha

Discord.Net's `ConnectionManager` has its own internal reconnect logic (heartbeat-miss / gateway-reconnect handling). The watchdog only exists as a backstop for when that gets truly stuck.

**Never call `client.StartAsync()` without first checking `ConnectionState == Disconnected`.** Discord.Net's `StartAsync()` throws `InvalidOperationException("Cannot start an already running client.")` for any state other than `Disconnected` — including the transient `Connecting` and `Disconnecting` states. Calling it unconditionally caused spurious production bot restarts: a slow-but-healthy reconnect (routine under network backpressure) was being misdiagnosed as a deadlock and killing the process. Fixed in two layers, both in `Util/ReconnectStateChecker.CheckStateAsync`: it only calls `StartAsync()` when `ConnectionState.Disconnected` (see `Util/ReconnectGuard.IsSafeToRestart`), and while `Connecting`/`Disconnecting` it polls (reusing the same timeout budget, raised from 15s to 1 minute) rather than returning immediately — so a reconnect truly stuck in one of those transient states still fails fast instead of being ignored forever.

`StartAsync()`'s returned `Task` completes once the reconnect attempt is *scheduled*, not once Discord.Net actually reconnects — so both watchdogs poll `ConnectionState` for `Connected` after calling it, and fail-fast if that doesn't happen within the reset timeout.

## Testing

- xUnit + Moq, no real Gateway/network calls.
- `ReconnectWatchdog` and `ReliabilityService` take constructor-injected fakes (delay functions, `startAsync`, `failFast`/`terminate`, `IDiscordClient` via `Mock<IDiscordClient>`) — see `HelperLib.Discord.Tests/Watchdog/ReconnectWatchdogTests.cs` and `HelperLib.Discord.Tests/Services/ReliabilityServiceTests.cs` for the pattern.

## Versioning

Bump `<Version>` in `HelperLib.Discord.csproj` and add a changelog entry in `README.md` following its semantic versioning convention (breaking / minor / patch) for any release-worthy change.
