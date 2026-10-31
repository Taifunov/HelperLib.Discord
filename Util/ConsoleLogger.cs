using Microsoft.Extensions.Logging;

namespace HelperLib.Discord.Util;

/// <summary>The logger used when the host application supplies none: writes to the console.</summary>
internal sealed class ConsoleLogger : ILogger
{
    private static readonly Lock ConsoleLock = new();

    internal static ConsoleLogger Instance { get; } = new();

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        // The console color is process-wide state, so one lock keeps concurrent logs from mixing colors or interleaving a message with its exception.
        lock (ConsoleLock)
        {
            var previousColor = Console.ForegroundColor;
            if (logLevel >= LogLevel.Error)
            {
                Console.ForegroundColor = ConsoleColor.Red;
            }

            try
            {
                Console.WriteLine(formatter(state, exception));
                if (exception is not null)
                {
                    Console.WriteLine(exception);
                }
            }
            finally
            {
                Console.ForegroundColor = previousColor;
            }
        }
    }
}
