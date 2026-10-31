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

Run the integration tests against a real guild (they are skipped without a token; see Testing):
```powershell
dotnet user-secrets set "HelperLibDiscord:Token" "<token>" --project HelperLib.Discord.Tests
dotnet test HelperLib.Discord.Tests\HelperLib.Discord.Tests.csproj --no-restore --filter "FullyQualifiedName~Integration"
```

Check code style before you report a task done (CI fails on any violation; drop `--verify-no-changes` to fix what can be fixed automatically). The root `.editorconfig` defines the rules, and `dotnet build` does not check the code-style ones:
```powershell
dotnet format HelperLib.Discord.sln --verify-no-changes
```

## Key files

- `DependencyInjection/ServiceCollectionExtensions.cs` — `AddHelperLibDiscord(configure)`, the only entry point. Registers the validated `HelperLibDiscordOptions` (`ValidateOnStart`), `TimeProvider.System`, `GuildLookup`, `ProcessExit.Default`, `ProcessTerminator` and `ReconnectWatchdog` as an `IHostedService`. Everything uses `TryAdd*`, so a host or test can replace a service first. The host must register `DiscordSocketClient`, `ILogger<T>` and `IHostApplicationLifetime`.
- `HelperLibDiscordOptions.cs` — `DefaultGuildId`, `ReconnectTimeout` (1 min), `ShutdownGracePeriod` (30 s), `HeartbeatTimeout` (3 min). The three timeouts must be positive and at most 49.7 days (`Task.Delay` limit); `DependencyInjection/HelperLibDiscordOptionsValidator.cs` checks them.
- `Watchdog/ReconnectWatchdog.cs` — `internal` hosted service. Subscribes to `Connected`, `Disconnected` and `LatencyUpdated` through `ClientEvents`, runs one reconnect check at a time, and unsubscribes in `StopAsync`/`Dispose`. The reconnect check calls `Util/ReconnectStateChecker.cs`.
- `Watchdog/HeartbeatMonitor.cs` — timer behind the heartbeat liveness check. `Arm` on connect, `Beat` on `LatencyUpdated`, `Pause` on disconnect. It calls back when the timeout passes without a beat and the injected `isConnected` check is true. The expiry decision (armed, elapsed, connected) and the disarm happen under its lock; the callback runs outside it.
- `Watchdog/ProcessTerminator.cs` — `Terminate(reason)`, first call only: arms the `ShutdownGracePeriod` hard exit first, then Critical log, `ProcessExit.SetExitCode(1)`, `IHostApplicationLifetime.StopApplication()`; the hard exit calls `ProcessExit.Exit(1)`. A failure of the stop is logged and does not prevent the exit.
- `Watchdog/ClientEvents.cs` — test seam: the add/remove actions for the client's three events. `ClientEvents.For(client)` is the production wiring.
- `Watchdog/ProcessExit.cs` — test seam: the two process-level effects (`Environment.ExitCode` and `Environment.Exit`).
- `Util/GuildLookup.cs` — injected lookup of guilds, channels, roles, emotes and users (`GetGuild`, `GetTextChannel`, `GetRole`, and so on, plus internal `TryGet*` helpers). It reads `DefaultGuildId` once in its constructor and keeps no other state.
- `Util/Log.cs` — all log messages as `[LoggerMessage]` source-generated extension methods on `ILogger`. Add new messages here with named placeholders; the build fails on `LogX(...)` calls (CA1848/CA1873).
- `Util/RoleChecks.cs` — static role-membership checks (`HasRolesAny`/`HasRolesAll`) over a `SocketGuildUser`. They do no lookups.
- `Util/ReconnectGuard.cs` — shared `IsSafeToRestart(ConnectionState)` predicate used by `Util/ReconnectStateChecker.cs`.
- `Util/ReconnectStateChecker.cs` — shared backstop logic (state check, restart, poll for Connected, terminate on timeout) used by `Watchdog/ReconnectWatchdog.cs`. It takes the caller's state getter, start delegate, `TimeProvider`, timeout, `ILogger` and terminate action. Fix reconnect-logic bugs here.
- `Util/Extensions.cs` — extension helpers for sending `Embed`/`EmbedBuilder` and images from URLs. `Util/ImageDownloader.cs` owns the shared HTTP client, restricts downloads to Discord CDN HTTPS URLs, disables redirects, and bounds response size and time.
- `Preconditions/RequireRoleAttribute.cs` — Discord.Commands precondition attribute restricting a command to a role id. Calls `Util/RoleChecks.cs`.
- `Models/GuildUsersResult.cs` — lightweight result type for guild user enumeration.

## Architecture notes

- The library is DI-only. It has no static client and no static state except the shared `ImageDownloader`. The host owns the `DiscordSocketClient`, its login and its lifetime.
- `ReconnectWatchdog` backstops Discord.Net's own `ConnectionManager`. Its check logic lives in `Util/ReconnectStateChecker.cs`.
- Termination goes through `ProcessTerminator` only. The order is: arm the `ShutdownGracePeriod` timer, Critical log, `Environment.ExitCode = 1`, `StopApplication()` (failures are logged), then `Environment.Exit(1)` when the timer ends. The hard exit is a fallback for a host that does not stop in time. A process supervisor then restarts the bot. This is intentional, not an error to be caught.
- Heartbeat liveness: `ReconnectWatchdog` arms `HeartbeatMonitor` on `Connected` (and on start, if the client is already connected), restarts it on `LatencyUpdated`, and pauses it on `Disconnected`. A timeout terminates only while the client reports `Connected`; `HeartbeatMonitor` checks that atomically with `Arm`/`Pause`, so a stale callback cannot end a freshly re-armed connection.
- All timing goes through `TimeProvider`. Nothing in the watchdog calls `Task.Delay` without it.
- The watchdog's single-flight slot is the running check's cancellation token, so a Connected event (which cancels it) frees the slot immediately. A finishing check clears the slot only if it still holds its own token.
- Thread-safety: `ReconnectWatchdog` state is guarded by one `Lock`, and nothing is awaited under it. `HeartbeatMonitor` has its own `Lock`. `ProcessTerminator` uses `Interlocked.Exchange` so only the first call acts. `GuildLookup` is immutable after construction.
- Library awaits use `ConfigureAwait(false)` (including `await foreach`); CA2007 is an error for the library and off for `HelperLib.Discord.Tests`. Use a plain `using` for `MemoryStream`, since `await using var` cannot take `ConfigureAwait`.

## Watchdog gotcha

Discord.Net's `ConnectionManager` has its own internal reconnect logic (heartbeat-miss / gateway-reconnect handling). The watchdog only exists as a backstop for when that gets truly stuck.

**Never call `client.StartAsync()` without first checking `ConnectionState == Disconnected`.** Discord.Net's `StartAsync()` throws `InvalidOperationException("Cannot start an already running client.")` for any state other than `Disconnected` — including the transient `Connecting` and `Disconnecting` states. Calling it unconditionally caused spurious production bot restarts: a slow-but-healthy reconnect (routine under network backpressure) was being misdiagnosed as a deadlock and killing the process. Fixed in two layers, both in `Util/ReconnectStateChecker.CheckStateAsync`: it only calls `StartAsync()` when `ConnectionState.Disconnected` (see `Util/ReconnectGuard.IsSafeToRestart`), and while `Connecting`/`Disconnecting` it polls (reusing the same timeout budget, `HelperLibDiscordOptions.ReconnectTimeout`, 1 minute by default; it was 15 s before) rather than returning immediately — so a reconnect truly stuck in one of those transient states still terminates the process instead of being ignored forever.

`StartAsync()`'s returned `Task` completes once the reconnect attempt is *scheduled*, not once Discord.Net actually reconnects — so the watchdog polls `ConnectionState` for `Connected` after calling it, and terminates if that doesn't happen within `ReconnectTimeout` or if the task faults while polling.

## Testing

- xUnit + Moq, no real Gateway/network calls, except the opt-in integration tests below.
- `HelperLib.Discord.Tests/Integration/` runs `GuildLookup` against a real guild, for the Discord.Net lookups that unit tests cannot reach (`SocketGuild` is sealed with an internal constructor) and for each lookup's missing-target warning. `IntegrationFactAttribute` skips a test when no bot token is configured, so plain `dotnet test` and CI make no network call. The integration tests are local only, use a dev bot token, and are not run in CI by design. `DiscordGuildFixture` logs in once for the whole `Discord guild` collection and never starts the reconnect watchdog, which could end the test process.
- Integration settings (`IntegrationSettings`): token from the environment variable `HELPERLIB_DISCORD_TOKEN` or the user secret `HelperLibDiscord:Token`; guild id from `HELPERLIB_DISCORD_GUILD_ID` or `HelperLibDiscord:GuildId`, default `872032510962446346`. The bot must be a member of that guild, and the privileged **Server Members Intent** (`GuildMembers`) must be enabled for it in the Discord developer portal. Without that intent the member download does not finish and the fixture fails after 60 s with a message that names it. An invalid guild id setting throws an `InvalidOperationException` that names the setting keys. A malformed user-secrets file does not break test discovery: the integration tests are skipped and the error text is the skip reason.
- Integration tests take channel, role and emote ids from the guild itself. When the guild has no channel or emote of a kind, that test returns early and writes a note to the test output.
- `Microsoft.Extensions.Time.Testing.FakeTimeProvider` replaces the clock. `WatchdogHarness` wires a `ReconnectWatchdog` and a `ProcessTerminator` to a fake clock, a `Mock<IDiscordClient>`, a mocked `IHostApplicationLifetime` and a fake `ProcessExit`, with the default option values. The harness clock is a `SteppingTimeProvider`, which counts the timers it creates and fires. `Task.Delay` resumes its caller asynchronously, so use `Time.AdvanceAsync(duration, watchedTask)` / `AdvanceUntilCompletedAsync` (`FakeTimeProviderExtensions`) when code polls the clock: they move time one second at a time and, after a timer fires, wait until the watched code registers its next timer or completes. Use plain `Time.Advance` when nothing awaits a delay.
- `FakeClientEvents` implements the `ClientEvents` seam, so tests raise `Connected`, `Disconnected` and `LatencyUpdated` without a gateway.
- The `ProcessExit` seam records the exit code and the hard exit, so no test ends the test process.
- See `HelperLib.Discord.Tests/Watchdog/ReconnectWatchdogTests.cs` and `ReconnectWatchdogHeartbeatTests.cs` for the pattern.

## Versioning

Bump `<Version>` in `HelperLib.Discord.csproj` and add a changelog entry in `README.md` following its semantic versioning convention (breaking / minor / patch) for any release-worthy change.
