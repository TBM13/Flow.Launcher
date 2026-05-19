using Microsoft.Extensions.Logging;

namespace Flow.Launcher.Infrastructure.Logging;

public static class LogManager
{
    private static ILoggerFactory _loggerFactory = null!;

    public static void Init(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public static ILogger<T> GetLogger<T>() where T : class => _loggerFactory.CreateLogger<T>();
    public static ILogger GetLogger(string categoryName) => _loggerFactory.CreateLogger(categoryName);
}
