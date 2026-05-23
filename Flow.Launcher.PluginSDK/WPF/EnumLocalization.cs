using System.ComponentModel;
using System.Reflection;

namespace Flow.Launcher.Infrastructure.WPF;

public class LocalizedEnumItem<T> where T : Enum
{
    public required T Value { get; set; }
    public required string Description { get; set; }
}

public static class EnumLocalization
{
    // Use a cache per enum type to avoid repeated reflection
    private static class Cache<T> where T : struct, Enum
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

    public static IReadOnlyList<LocalizedEnumItem<T>> GetLocalizedEnumItems<T>() where T : struct, Enum
    {
        // Return shallow copy to prevent external modification of the cached list
        return [.. Cache<T>.Items];
    }
}
