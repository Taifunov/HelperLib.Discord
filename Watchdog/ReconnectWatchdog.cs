using Discord;
using HelperLib.Discord.Util;

namespace HelperLib.Discord.Watchdog;

internal class ReconnectWatchdog(
    TimeSpan resetTimeout,
    Func<TimeSpan, CancellationToken, Task> delayAsync,
    Func<TimeSpan, Task> delayWithoutCancellationAsync,
    Action failFast) : IDisposable
{
    private readonly Lock _reconnectCtsGate = new();
    private CancellationTokenSource _reconnectCts = new();
    private volatile bool _disposed;

    internal CancellationToken GetReconnectCancellationToken()
    {
        lock (_reconnectCtsGate)
        {
            return _disposed ? new CancellationToken(canceled: true) : _reconnectCts.Token;
        }
    }

    internal void Arm()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        TryReplaceReconnectCts();
    }

    internal Task ClientOnConnected()
    {
        Console.WriteLine("Client reconnected, resetting cancel tokens...");

        if (TryReplaceReconnectCts())
        {
            Console.WriteLine("Client reconnected, cancel tokens reset");
        }

        return Task.CompletedTask;
    }

    internal async Task ClientOnDisconnected(IDiscordClient client)
    {
        Console.WriteLine("Client disconnected, starting timeout task...");

        var token = GetReconnectCancellationToken();
        try
        {
            await delayAsync(resetTimeout, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (token.IsCancellationRequested)
        {
            return;
        }

        Console.WriteLine("Timeout expired, continuing to check client state...");
        await CheckStateAsync(client);
        Console.WriteLine("State came back okay");
    }

    internal Task CheckStateAsync(IDiscordClient client) =>
        ReconnectStateChecker.CheckStateAsync(
            () => client.ConnectionState,
            client.StartAsync,
            delayWithoutCancellationAsync,
            resetTimeout,
            log: message => WriteColored(ConsoleColor.Green, message),
            terminate: message =>
            {
                WriteColored(ConsoleColor.Red, $"{message} Killing process.");
                failFast();
            });

    public void Dispose()
    {
        CancellationTokenSource lastCts;
        lock (_reconnectCtsGate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            lastCts = _reconnectCts;
        }

        CancelAndDispose(lastCts);
    }

    private bool TryReplaceReconnectCts()
    {
        CancellationTokenSource previousCts;
        lock (_reconnectCtsGate)
        {
            if (_disposed)
            {
                return false;
            }

            previousCts = _reconnectCts;
            _reconnectCts = new CancellationTokenSource();
        }

        CancelAndDispose(previousCts);
        return true;
    }

    private static void CancelAndDispose(CancellationTokenSource cts)
    {
        cts.Cancel();
        cts.Dispose();
    }

    private static void WriteColored(ConsoleColor color, string message)
    {
        Console.ForegroundColor = color;
        try
        {
            Console.WriteLine(message);
        }
        finally
        {
            Console.ForegroundColor = ConsoleColor.White;
        }
    }
}
