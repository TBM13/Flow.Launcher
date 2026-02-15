using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Flow.Launcher.SettingPages.ViewModels;

public partial class DropdownDataGeneric<TValue> : ObservableObject where TValue : Enum
{
    [ObservableProperty]
    public partial string Display { get; set; }
    public TValue Value { get; private init; }
    [ObservableProperty]
    public partial string LocalizationKey { get; set; }

    public static List<TR> GetValues<TR>(string keyPrefix) where TR : DropdownDataGeneric<TValue>, new()
    {
        var data = new List<TR>();
        var enumValues = (TValue[])Enum.GetValues(typeof(TValue));

        foreach (var value in enumValues)
        {
            var key = keyPrefix + value;
            var display = App.API.GetTranslation(key);
            data.Add(new TR { Display = display, Value = value, LocalizationKey = key });
        }

        return data;
    }

    public static void UpdateLabels<TR>(List<TR> options) where TR : DropdownDataGeneric<TValue>
    {
        foreach (var item in options)
        {
            item.Display = App.API.GetTranslation(item.LocalizationKey);
        }
    }
}
