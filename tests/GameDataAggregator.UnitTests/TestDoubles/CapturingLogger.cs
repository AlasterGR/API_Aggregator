using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace GameDataAggregator.UnitTests.TestDoubles;

/// <summary>A logger that keeps every message, so tests can check what was logged and at which level.</summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly ConcurrentQueue<(LogLevel Level, string Message)> _entries = new();

    /// <summary>Every message logged so far, oldest first.</summary>
    public IReadOnlyList<(LogLevel Level, string Message)> Entries => [.. _entries];

    /// <summary>The messages logged at one level.</summary>
    public IReadOnlyList<string> At(LogLevel level) => [.. _entries.Where(e => e.Level == level).Select(e => e.Message)];

    /// <summary>Scopes are not needed by the tests.</summary>
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <summary>Every level is recorded.</summary>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <summary>Keeps the formatted message and its level.</summary>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        _entries.Enqueue((logLevel, formatter(state, exception)));
}
