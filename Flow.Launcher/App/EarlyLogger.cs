using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Flow.Launcher.App;

/// <summary>
/// Used for logging before the real logger is initialized.
/// </summary>
public class EarlyLogger(string category, ConcurrentQueue<EarlyLogEntry> buffer) : ILogger
{
    private readonly ConcurrentQueue<EarlyLogEntry> _buffer = buffer;
    private ILogger? _realLogger;

    public string Category { get; } = category;

    public void SetRealLogger(ILogger realLogger)
    {
        _realLogger = realLogger;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _realLogger?.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => _realLogger?.IsEnabled(logLevel) ?? true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (_realLogger is not null)
            _realLogger.Log(logLevel, eventId, state, exception, formatter);
        else
        {
            string message = formatter(state, exception);
            _buffer.Enqueue(new EarlyLogEntry(Category, logLevel, eventId, exception, message));
        }
    }
}
