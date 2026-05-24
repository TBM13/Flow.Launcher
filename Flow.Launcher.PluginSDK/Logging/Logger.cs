using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace Flow.Launcher.PluginSDK.Logging;

public class Logger<T>(ILoggerFactory factory) : Logger(factory.CreateLogger<T>());

public class Logger(ILogger logger)
{
    // We can't allow plugins to use the ILogger directly,
    // since we want to force them to use the ZLogger methods
    internal ILogger InternalLogger { get; } = logger;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogTrace([InterpolatedStringHandlerArgument("")] ref TraceInterpolatedStringHandler message, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        InternalLogger.ZLog(LogLevel.Trace, default, null, ref message.InnerHandler.InnerHandler, context, memberName, filePath, lineNumber);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogTrace(EventId eventId, [InterpolatedStringHandlerArgument("")] ref TraceInterpolatedStringHandler message, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        InternalLogger.ZLog(LogLevel.Trace, eventId, null, ref message.InnerHandler.InnerHandler, context, memberName, filePath, lineNumber);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogTrace(Exception? exception, [InterpolatedStringHandlerArgument("")] ref TraceInterpolatedStringHandler message, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        InternalLogger.ZLog(LogLevel.Trace, default, exception, ref message.InnerHandler.InnerHandler, context, memberName, filePath, lineNumber);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogTrace(EventId eventId, Exception? exception, [InterpolatedStringHandlerArgument("")] ref TraceInterpolatedStringHandler message, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        InternalLogger.ZLog(LogLevel.Trace, eventId, exception, ref message.InnerHandler.InnerHandler, context, memberName, filePath, lineNumber);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogDebug([InterpolatedStringHandlerArgument("")] ref DebugInterpolatedStringHandler message, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        InternalLogger.ZLog(LogLevel.Debug, default, null, ref message.InnerHandler.InnerHandler, context, memberName, filePath, lineNumber);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogDebug(EventId eventId, [InterpolatedStringHandlerArgument("")] ref DebugInterpolatedStringHandler message, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        InternalLogger.ZLog(LogLevel.Debug, eventId, null, ref message.InnerHandler.InnerHandler, context, memberName, filePath, lineNumber);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogDebug(Exception? exception, [InterpolatedStringHandlerArgument("")] ref DebugInterpolatedStringHandler message, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        InternalLogger.ZLog(LogLevel.Debug, default, exception, ref message.InnerHandler.InnerHandler, context, memberName, filePath, lineNumber);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogDebug(EventId eventId, Exception? exception, [InterpolatedStringHandlerArgument("")] ref DebugInterpolatedStringHandler message, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        InternalLogger.ZLog(LogLevel.Debug, eventId, exception, ref message.InnerHandler.InnerHandler, context, memberName, filePath, lineNumber);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogInfo([InterpolatedStringHandlerArgument("")] ref InfoInterpolatedStringHandler message, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        InternalLogger.ZLog(LogLevel.Information, default, null, ref message.InnerHandler.InnerHandler, context, memberName, filePath, lineNumber);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogInfo(EventId eventId, [InterpolatedStringHandlerArgument("")] ref InfoInterpolatedStringHandler message, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        InternalLogger.ZLog(LogLevel.Information, eventId, null, ref message.InnerHandler.InnerHandler, context, memberName, filePath, lineNumber);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogInfo(Exception? exception, [InterpolatedStringHandlerArgument("")] ref InfoInterpolatedStringHandler message, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        InternalLogger.ZLog(LogLevel.Information, default, exception, ref message.InnerHandler.InnerHandler, context, memberName, filePath, lineNumber);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogInfo(EventId eventId, Exception? exception, [InterpolatedStringHandlerArgument("")] ref InfoInterpolatedStringHandler message, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        InternalLogger.ZLog(LogLevel.Information, eventId, exception, ref message.InnerHandler.InnerHandler, context, memberName, filePath, lineNumber);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogWarn([InterpolatedStringHandlerArgument("")] ref WarnInterpolatedStringHandler message, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        InternalLogger.ZLog(LogLevel.Warning, default, null, ref message.InnerHandler.InnerHandler, context, memberName, filePath, lineNumber);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogWarn(EventId eventId, [InterpolatedStringHandlerArgument("")] ref WarnInterpolatedStringHandler message, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        InternalLogger.ZLog(LogLevel.Warning, eventId, null, ref message.InnerHandler.InnerHandler, context, memberName, filePath, lineNumber);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogWarn(Exception? exception, [InterpolatedStringHandlerArgument("")] ref WarnInterpolatedStringHandler message, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        InternalLogger.ZLog(LogLevel.Warning, default, exception, ref message.InnerHandler.InnerHandler, context, memberName, filePath, lineNumber);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogWarn(EventId eventId, Exception? exception, [InterpolatedStringHandlerArgument("")] ref WarnInterpolatedStringHandler message, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        InternalLogger.ZLog(LogLevel.Warning, eventId, exception, ref message.InnerHandler.InnerHandler, context, memberName, filePath, lineNumber);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogError([InterpolatedStringHandlerArgument("")] ref ErrorInterpolatedStringHandler message, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        InternalLogger.ZLog(LogLevel.Error, default, null, ref message.InnerHandler.InnerHandler, context, memberName, filePath, lineNumber);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogError(EventId eventId, [InterpolatedStringHandlerArgument("")] ref ErrorInterpolatedStringHandler message, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        InternalLogger.ZLog(LogLevel.Error, eventId, null, ref message.InnerHandler.InnerHandler, context, memberName, filePath, lineNumber);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogError(Exception? exception, [InterpolatedStringHandlerArgument("")] ref ErrorInterpolatedStringHandler message, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        InternalLogger.ZLog(LogLevel.Error, default, exception, ref message.InnerHandler.InnerHandler, context, memberName, filePath, lineNumber);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogError(EventId eventId, Exception? exception, [InterpolatedStringHandlerArgument("")] ref ErrorInterpolatedStringHandler message, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        InternalLogger.ZLog(LogLevel.Error, eventId, exception, ref message.InnerHandler.InnerHandler, context, memberName, filePath, lineNumber);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogCritical([InterpolatedStringHandlerArgument("")] ref CriticalInterpolatedStringHandler message, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        InternalLogger.ZLog(LogLevel.Critical, default, null, ref message.InnerHandler.InnerHandler, context, memberName, filePath, lineNumber);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogCritical(EventId eventId, [InterpolatedStringHandlerArgument("")] ref CriticalInterpolatedStringHandler message, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        InternalLogger.ZLog(LogLevel.Critical, eventId, null, ref message.InnerHandler.InnerHandler, context, memberName, filePath, lineNumber);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogCritical(Exception? exception, [InterpolatedStringHandlerArgument("")] ref CriticalInterpolatedStringHandler message, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        InternalLogger.ZLog(LogLevel.Critical, default, exception, ref message.InnerHandler.InnerHandler, context, memberName, filePath, lineNumber);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void LogCritical(EventId eventId, Exception? exception, [InterpolatedStringHandlerArgument("")] ref CriticalInterpolatedStringHandler message, object? context = null, [CallerMemberName] string? memberName = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = 0)
    {
        InternalLogger.ZLog(LogLevel.Critical, eventId, exception, ref message.InnerHandler.InnerHandler, context, memberName, filePath, lineNumber);
    }
}
