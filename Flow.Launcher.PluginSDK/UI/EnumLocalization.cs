using System.ComponentModel;
using System.Reflection;

namespace Flow.Launcher.PluginSDK.UI;

/// <summary>
/// Represents an enum value paired with its localized description.
/// <see cref="ToString"/> returns <see cref="Description"/>, so the item can be displayed
/// in a ComboBox without any display member path or item template.
/// </summary>
public sealed record LocalizedEnumItem<T>(T Value, string Description) where T : struct, Enum
{
    public override string ToString() => Description;
}

// Use a static class per enum type to avoid repeated reflection
/// <summary>
/// Provides the <see cref="DescriptionAttribute"/> localized display items for an enum type.
/// </summary>
public static class EnumLocalization<T> where T : struct, Enum
{
    public static IReadOnlyList<LocalizedEnumItem<T>> Items { get; } =
    [
        .. Enum.GetValues<T>().Select(value => new LocalizedEnumItem<T>(value, GetDescription(value)))
    ];

    private static string GetDescription(T value) =>
        typeof(T).GetField(value.ToString())?.GetCustomAttribute<DescriptionAttribute>()?.Description
        ?? value.ToString();
}
