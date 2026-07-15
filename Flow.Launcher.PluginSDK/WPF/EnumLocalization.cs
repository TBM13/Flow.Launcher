using System.ComponentModel;
using System.Reflection;

namespace Flow.Launcher.PluginSDK.WPF;

public class LocalizedEnumItem<T> where T : Enum
{
    public required T Value { get; set; }
    public required string Description { get; set; }
}

// Use a static class per enum type to avoid repeated reflection
public static class EnumLocalization<T> where T : struct, Enum
{
    public static readonly List<LocalizedEnumItem<T>> Items = [..
            Enum.GetValues<T>().Select(e => new LocalizedEnumItem<T>
            {
                Value = e,
                Description = FetchDescription(e)
            })
    ];

    private static string FetchDescription(T enumValue)
    {
        return typeof(T)
            .GetField(enumValue.ToString())
            ?.GetCustomAttribute<DescriptionAttribute>()
            ?.Description
            ?? enumValue.ToString();
    }
}
