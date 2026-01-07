using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Flow.Launcher.Infrastructure.UI;

public class LocalizedEnumItem<T> where T : Enum
{
    public required T Value { get; set; }
    public required string Description { get; set; }
}

public static class EnumLocalization
{
    public static List<LocalizedEnumItem<T>> GetLocalizedEnumItems<T>() where T : Enum
    {
        return [.. Enum.GetValues(typeof(T))
            .Cast<T>()
            .Select(e => new LocalizedEnumItem<T>
            {
                Value = e,
                Description = GetDescription(e)
            })
        ];
    }

    private static string GetDescription<T>(T enumValue) where T : Enum
    {
        var type = typeof(T);
        var memberInfo = type.GetMember(enumValue.ToString());
        if (memberInfo.Length > 0)
        {
            var attrs = memberInfo[0].GetCustomAttributes(typeof(DescriptionAttribute), false);
            if (attrs.Length > 0)
            {
                return ((DescriptionAttribute)attrs[0]).Description;
            }
        }

        return enumValue.ToString();
    }
}
