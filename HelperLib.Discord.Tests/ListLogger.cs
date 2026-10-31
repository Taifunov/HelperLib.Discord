using Microsoft.Extensions.Logging;

namespace HelperLib.Discord.Tests;

/// <summary>Records every log call so tests can assert on the level, the formatted message and the exception.</summary>
internal class ListLogger : ILogger
{
    private readonly Lock _gate = new();
    private readonly List<LogEntry> _entries = [];

    /// <summary>A snapshot of the entries recorded so far; safe to read while other threads log.</summary>
    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (_gate)
            {
                return [.. _entries];
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        AddEntry(new LogEntry(logLevel, formatter(state, exception), exception));

    private void AddEntry(LogEntry entry)
    {
        lock (_gate)
        {
            _entries.Add(entry);
        }
    }
}

/// <summary>A <see cref="ListLogger"/> that can stand in for an <c>ILogger&lt;T&gt;</c>.</summary>
internal sealed class ListLogger<T> : ListLogger, ILogger<T>;

internal sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

internal static class LogEntryAssertions
{
    /// <summary>Asserts that exactly one entry was logged, with this level and message.</summary>
    public static void AssertSingleEntry(this IReadOnlyList<LogEntry> entries, LogLevel level, string message)
    {
        var entry = Assert.Single(entries);
        Assert.Equal(level, entry.Level);
        Assert.Equal(message, entry.Message);
    }

    public static bool HasEntry(this IEnumerable<LogEntry> entries, LogLevel level, string message) =>
        entries.Any(entry => entry.Level == level && entry.Message == message);
}
