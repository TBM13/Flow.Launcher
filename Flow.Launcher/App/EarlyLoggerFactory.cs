using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Flow.Launcher.App;

public record EarlyLogEntry(string Category, LogLevel LogLevel, EventId EventId, Exception? Exception, string Message);

/// <summary>
/// Used for logging before the real logger is initialized.
/// </summary>
public class EarlyLoggerFactory : ILoggerFactory
{
    private readonly ConcurrentQueue<EarlyLogEntry> _logBuffer = new();
    private readonly ConcurrentDictionary<string, EarlyLogger> _loggers = new();

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new EarlyLogger(categoryName, _logBuffer));
    }

    public void AddProvider(ILoggerProvider provider)
    {

    }

    /// <summary>
    /// Redirects all existing loggers to the actual loggers built using the provided logger factory,
    /// and flushes the buffered log entries to them.
    /// </summary>
    public void FlushAndHandoff(ILoggerFactory realLoggerFactory)
    {
        foreach (EarlyLogger logger in _loggers.Values)
        {
            ILogger realLogger = realLoggerFactory.CreateLogger(logger.Category);
            logger.SetRealLogger(realLogger);
        }

        while (_logBuffer.TryDequeue(out var entry))
        {
            ILogger realLogger = realLoggerFactory.CreateLogger(entry.Category);
            realLogger.Log(entry.LogLevel, entry.EventId, entry.Exception, entry.Message);
        }
    }

    public void Dispose()
    {
        _loggers.Clear();
    }
}
