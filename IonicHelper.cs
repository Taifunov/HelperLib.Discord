using global::Discord;
using global::Discord.WebSocket;
using HelperLib.Discord.Util;
using HelperLib.Discord.Watchdog;

namespace HelperLib.Discord
{
    public class IonicHelper
    {
        private static CancellationTokenSource? _tokenSource;
        private readonly string _token;
        private readonly ReconnectWatchdog _watchdog;
        public static DiscordSocketClient? Client { get; private set; }

        public IonicHelper(string token, ulong? defaultGuildId = null)
            : this(
                token,
                TimeSpan.FromMinutes(1),
                Task.Delay,
                Task.Delay,
                () => Environment.Exit(1),
                defaultGuildId)
        {
        }

        /// <summary>
        /// Creates an IonicHelper for <see cref="AttachToExistingClient"/> only - no token is
        /// needed since this path never logs in a client itself, it only attaches to one the
        /// host application already owns.
        /// </summary>
        public IonicHelper(ulong? defaultGuildId = null)
            : this(string.Empty, defaultGuildId)
        {
        }

        internal IonicHelper(
            string token,
            TimeSpan resetTimeout,
            Func<TimeSpan, CancellationToken, Task> delayAsync,
            Func<TimeSpan, Task> delayWithoutCancellationAsync,
            Action failFast,
            ulong? defaultGuildId = null)
        {
            _token = token;
            GuildLookup.SetDefaultGuildId(defaultGuildId);
            _watchdog = new ReconnectWatchdog(resetTimeout, delayAsync, delayWithoutCancellationAsync, failFast);

            if(defaultGuildId != default)
                Console.WriteLine($"Default GuildId was successfully set to: {defaultGuildId}");
        }

        public async Task RunAsync(DiscordSocketConfig config, Func<LogMessage, Task> logger)
        {
            if (_token.Length == 0)
            {
                throw new ArgumentException("Incorrect token! Pass valid one into IonicHelper.ctor");
            }

            var client = new DiscordSocketClient(config);
            client.Log += logger;
            AttachClient(client);

            Console.WriteLine("Starting IonicHelper.");
            await client.LoginAsync(TokenType.Bot, _token);
            await client.StartAsync();
        }

        /// <summary>
        /// Attaches this IonicHelper's reconnect watchdog and static <see cref="Client"/>
        /// reference to an already-running <see cref="DiscordSocketClient"/> instead of
        /// creating and logging in a second gateway connection. Use this when the host
        /// application already owns a client's lifecycle (e.g. via a DI-managed hosting
        /// framework) and only needs GuildLookup/IonicHelper.Client's static access plus
        /// the watchdog's fail-fast reconnect monitoring, not a second connection.
        /// </summary>
        public void AttachToExistingClient(DiscordSocketClient client, ulong? defaultGuildId = null)
        {
            if (defaultGuildId != null)
            {
                GuildLookup.SetDefaultGuildId(defaultGuildId);
            }

            AttachClient(client);
        }

        private void AttachClient(DiscordSocketClient client)
        {
            _tokenSource = new CancellationTokenSource();
            _watchdog.Arm();
            Client = client;
            client.Connected += _watchdog.ClientOnConnected;
            client.Disconnected += exception =>
            {
                _ = _watchdog.ClientOnDisconnected(client);
                return Task.CompletedTask;
            };
        }

        public static CancellationTokenSource? GetCancellationTokenSource() => _tokenSource;
    }
}
