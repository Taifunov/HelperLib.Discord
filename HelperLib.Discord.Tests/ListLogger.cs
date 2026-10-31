using Microsoft.Extensions.Logging;

namespace HelperLib.Discord.Tests;

/// <summary>Records every log call so tests can assert on the level, the formatted message and the exception.</summary>
internal sealed class ListLogger : ILogger
{
    public List<LogEntry> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
}

internal sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
