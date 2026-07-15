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

    private volatile ILoggerFactory? _realFactory;
    private readonly Lock _syncLock = new();

    public ILogger CreateLogger(string categoryName)
    {
        ILoggerFactory? realFactory = _realFactory;
        if (realFactory is not null)
            return realFactory.CreateLogger(categoryName);

        lock (_syncLock)
        {
            if (_realFactory is not null)
                return _realFactory.CreateLogger(categoryName);

            return _loggers.GetOrAdd(categoryName, name => new EarlyLogger(categoryName, _logBuffer));
        }
    }

    public void AddProvider(ILoggerProvider provider)
    {
        throw new NotSupportedException("Early loggers do not support providers");
    }

    /// <summary>
    /// Redirects all existing loggers to the actual loggers built using the provided logger factory,
    /// and flushes the buffered log entries to them.
    /// </summary>
    /// <exception cref="InvalidOperationException"/>
    public void FlushAndHandoff(ILoggerFactory realLoggerFactory)
    {
        lock (_syncLock)
        {
            if (_realFactory is not null)
                throw new InvalidOperationException($"{nameof(FlushAndHandoff)} can only be called once.");

            _realFactory = realLoggerFactory;
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
    }

    public void Dispose()
    {
        _loggers.Clear();
        _realFactory = null;
    }
}
