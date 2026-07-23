using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls.Primitives;

namespace Flow.Launcher.PluginSDK.WPF;

public static class EnumBinding
{
    private record EnumItem(object Value, string Description);

    private static readonly ConcurrentDictionary<Type, EnumItem[]> Cache = new();

    public static readonly DependencyProperty EnumTypeProperty =
        DependencyProperty.RegisterAttached(
            "EnumType",
            typeof(Type),
            typeof(EnumBinding),
            new PropertyMetadata(null, OnEnumTypeChanged));

    public static void SetEnumType(DependencyObject element, Type value) =>
        element.SetValue(EnumTypeProperty, value);

    public static Type GetEnumType(DependencyObject element) =>
        (Type)element.GetValue(EnumTypeProperty);

    private static void OnEnumTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Selector selector)
        {
            if (e.NewValue is Type enumType && enumType.IsEnum)
            {
                selector.DisplayMemberPath = nameof(EnumItem.Description);
                selector.SelectedValuePath = nameof(EnumItem.Value);
                selector.ItemsSource = GetEnumItems(enumType);
            }
            else
            {
                selector.ItemsSource = null;
                selector.DisplayMemberPath = string.Empty;
                selector.SelectedValuePath = string.Empty;
            }
        }
    }

    private static EnumItem[] GetEnumItems(Type enumType)
    {
        return Cache.GetOrAdd(enumType, type =>
        {
            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Static);
            EnumItem[] items = new EnumItem[fields.Length];

            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                object? value = field.GetValue(null);
                if (value is null) continue;

                string description = field.GetCustomAttribute<DescriptionAttribute>()?.Description ?? field.Name;
                items[i] = new EnumItem(value, description);
            }

            return items;
        });
    }
}
