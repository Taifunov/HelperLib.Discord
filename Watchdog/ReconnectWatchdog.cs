using global::Discord;
using HelperLib.Discord.Util;

namespace HelperLib.Discord.Watchdog;

internal class ReconnectWatchdog(
    TimeSpan resetTimeout,
    Func<TimeSpan, CancellationToken, Task> delayAsync,
    Func<TimeSpan, Task> delayWithoutCancellationAsync,
    Action failFast)
{
    private CancellationTokenSource? _reconnectCts;

    internal CancellationToken GetReconnectCancellationToken() => (_reconnectCts ??= new CancellationTokenSource()).Token;

    internal void Arm() => _reconnectCts = new CancellationTokenSource();

    internal Task ClientOnConnected()
    {
        Console.WriteLine("Client reconnected, resetting cancel tokens...");

        _reconnectCts?.Cancel();
        _reconnectCts = new CancellationTokenSource();

        Console.WriteLine("Client reconnected, cancel tokens reset");
        return Task.CompletedTask;
    }

    internal async Task ClientOnDisconnected(IDiscordClient client)
    {
        Console.WriteLine("Client disconnected, starting timeout task...");
        if (_reconnectCts != null)
        {
            var token = _reconnectCts.Token;
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

            await Task.Run(async () =>
            {
                Console.WriteLine("Timeout expired, continuing to check client state...");
                await CheckStateAsync(client);
                Console.WriteLine("State came back okay");
            });
        }

        return;
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
