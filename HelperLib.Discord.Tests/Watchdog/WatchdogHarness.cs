using System.Diagnostics;
using Discord;
using HelperLib.Discord.Watchdog;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;

namespace HelperLib.Discord.Tests.Watchdog;

/// <summary>
/// A watchdog wired to a fake clock, a fake gateway and a fake process. Unless the test passes its own options,
/// it uses the default values (reconnect timeout 1 minute, shutdown grace period 30 seconds,
/// heartbeat timeout 3 minutes).
/// </summary>
internal sealed class WatchdogHarness : IDisposable
{
    private int _startCalls;

    public WatchdogHarness(
        ConnectionState initialState = ConnectionState.Disconnected,
        HelperLibDiscordOptions? options = null)
    {
        State = initialState;
        Client.Setup(client => client.ConnectionState).Returns(() => State);
        Client.Setup(client => client.StartAsync()).Returns(() =>
        {
            Interlocked.Increment(ref _startCalls);
            return OnStartAsync();
        });

        var optionsAccessor = Options.Create(options ?? new HelperLibDiscordOptions());
        var processExit = new ProcessExit(
            exitCode => ExitCodesSet.Add(exitCode),
            exitCode =>
            {
                HardExitCodes.Add(exitCode);
                HardExit.TrySetResult();
            });
        Terminator = new ProcessTerminator(Lifetime.Object, Time, optionsAccessor, processExit, TerminatorLogger);
        Watchdog = new ReconnectWatchdog(Client.Object, Events.Seam, optionsAccessor, Time, Terminator, Logger);
    }

    public SteppingTimeProvider Time { get; } = new();

    public FakeClientEvents Events { get; } = new();

    public Mock<IDiscordClient> Client { get; } = new();

    public Mock<IHostApplicationLifetime> Lifetime { get; } = new();

    public ListLogger<ReconnectWatchdog> Logger { get; } = new();

    public ListLogger<ProcessTerminator> TerminatorLogger { get; } = new();

    public ProcessTerminator Terminator { get; }

    public ReconnectWatchdog Watchdog { get; }

    public ConnectionState State { get; set; }

    /// <summary>What the client's StartAsync returns; it completes at once by default.</summary>
    public Func<Task> OnStartAsync { get; set; } = () => Task.CompletedTask;

    public List<int> ExitCodesSet { get; } = [];

    public List<int> HardExitCodes { get; } = [];

    /// <summary>Completes when the process would have been ended forcibly.</summary>
    public TaskCompletionSource HardExit { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int StartCalls => Volatile.Read(ref _startCalls);

    /// <summary>Makes the client report Connected once the watchdog restarts it.</summary>
    public void ConnectOnStart() =>
        OnStartAsync = () =>
        {
            State = ConnectionState.Connected;
            return Task.CompletedTask;
        };

    public Task StartAsync() => Watchdog.StartAsync(CancellationToken.None);

    /// <summary>Starts the watchdog, then raises the client's Connected event.</summary>
    public async Task StartConnectedAsync()
    {
        await StartAsync();
        await Events.RaiseConnected();
    }

    public void VerifyProcessTerminationRequested(Times times) =>
        Lifetime.Verify(lifetime => lifetime.StopApplication(), times);

    public void AssertNoRestartOrTermination()
    {
        Assert.Equal(0, StartCalls);
        VerifyProcessTerminationRequested(Times.Never());
    }

    public void Dispose() => Watchdog.Dispose();

    /// <summary>Waits for work that runs on another thread, such as a fire-and-forget check, without sleeping.</summary>
    public static async Task WaitUntilAsync(
        Func<bool> condition,
        string failureMessage = "The condition did not become true.")
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"{failureMessage} Waited 5 seconds.");
            await Task.Yield();
        }
    }
}
