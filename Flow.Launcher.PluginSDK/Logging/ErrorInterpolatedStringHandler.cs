using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using ZLogger;

namespace Flow.Launcher.PluginSDK.Logging;

[InterpolatedStringHandler]
public ref struct ErrorInterpolatedStringHandler(int literalLength, int formattedCount, Logger logger, out bool isEnabled)
{
    public ZLoggerErrorInterpolatedStringHandler InnerHandler = new(literalLength, formattedCount, logger.InternalLogger, out isEnabled);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendLiteral([ConstantExpected] string s)
    {
        InnerHandler.AppendLiteral(s);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendFormatted<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
        T value, int alignment = 0, string? format = null,
        [CallerArgumentExpression(nameof(value))] string? argumentName = null)
    {
        InnerHandler.AppendFormatted(value, alignment, format, argumentName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendFormatted<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
        T? value, int alignment = 0, string? format = null,
        [CallerArgumentExpression(nameof(value))] string? argumentName = null) where T : struct
    {
        InnerHandler.AppendFormatted(value, alignment, format, argumentName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AppendFormatted<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] T>(
        (string, T) namedValue, int alignment = 0, string? format = null,
        [CallerArgumentExpression(nameof(namedValue))] string? argumentName = null)
    {
        InnerHandler.AppendFormatted(namedValue, alignment, format, argumentName);
    }
}
