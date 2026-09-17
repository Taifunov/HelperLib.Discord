using global::Discord;
using global::Discord.WebSocket;
using HelperLib.Discord.Util;

namespace HelperLib.Discord.Services
{
    public class ReliabilityService
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(1.0);
        private readonly Func<ConnectionState> _getConnectionState;
        private readonly Func<Task> _startAsync;
        private readonly Func<TimeSpan, CancellationToken, Task> _delayWithCancellationAsync;
        private readonly Func<TimeSpan, Task> _delayAsync;
        private readonly Action<string> _writeLine;
        private readonly Action<string> _terminate;
        private CancellationTokenSource _cts;

        public ReliabilityService(DiscordSocketClient client)
            : this(
                () => client.ConnectionState,
                client.StartAsync,
                Task.Delay,
                Task.Delay,
                Console.WriteLine,
                _ => Environment.Exit(1),
                Timeout)
        {
            client.Connected += ResetCancellationTokens;
            client.Disconnected += exception =>
            {
                _ = StartTimeoutTask(exception);
                return Task.CompletedTask;
            };
        }

        internal ReliabilityService(
            Func<ConnectionState> getConnectionState,
            Func<Task> startAsync,
            Func<TimeSpan, CancellationToken, Task> delayWithCancellationAsync,
            Func<TimeSpan, Task> delayAsync,
            Action<string> writeLine,
            Action<string> terminate,
            TimeSpan timeout)
        {
            _cts = new CancellationTokenSource();
            _getConnectionState = getConnectionState;
            _startAsync = startAsync;
            _delayWithCancellationAsync = delayWithCancellationAsync;
            _delayAsync = delayAsync;
            _writeLine = writeLine;
            _terminate = terminate;
            ServiceTimeout = timeout;
        }

        private TimeSpan ServiceTimeout { get; }

        internal Task ResetCancellationTokens()
        {
            _writeLine("Client reconnected, resetting cancellation tokens.");
            _cts.Cancel();
            _cts = new CancellationTokenSource();

            return Task.CompletedTask;
        }

        internal async Task StartTimeoutTask(Exception exception)
        {
            _writeLine("Client disconnected, starting timeout task.");
            var token = _cts.Token;

            try
            {
                await _delayWithCancellationAsync(ServiceTimeout, token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (token.IsCancellationRequested)
                return;

            _writeLine("Timeout expired, checking client state..");
            await CheckStateAsync();
        }

        internal Task CheckStateAsync() =>
            ReconnectStateChecker.CheckStateAsync(
                _getConnectionState,
                _startAsync,
                _delayAsync,
                ServiceTimeout,
                log: _writeLine,
                terminate: message =>
                {
                    _writeLine($"{message} Killing process.");
                    _terminate(message);
                });
    }
}
