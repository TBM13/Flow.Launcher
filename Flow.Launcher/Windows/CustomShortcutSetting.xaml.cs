using System.Windows;
using System.Windows.Input;
using Flow.Launcher.PluginSDK.API;
using Flow.Launcher.Settings.Pages;

namespace Flow.Launcher.Windows;

public partial class CustomShortcutSetting : Window
{
    private readonly SettingsHotkeyViewModel _hotkeyVm;
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    private string originalKey { get; } = null;
    private string originalValue { get; } = null;
    private bool update { get; } = false;

    public CustomShortcutSetting(SettingsHotkeyViewModel vm)
    {
        _hotkeyVm = vm;
        InitializeComponent();
    }

    public CustomShortcutSetting(string key, string value, SettingsHotkeyViewModel vm)
    {
        Key = key;
        Value = value;
        originalKey = key;
        originalValue = value;
        update = true;
        _hotkeyVm = vm;
        InitializeComponent();
    }

    private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        DialogResult = false;
        Close();
    }

    private void BtnAdd_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(Key) || string.IsNullOrEmpty(Value))
        {
            MessageBox.Show("Shortcut and/or its expansion is empty.");
            return;
        }

        // Check if key is modified or adding a new one
        if (((update && originalKey != Key) || !update) && _hotkeyVm.DoesShortcutExist(Key))
        {
            MessageBox.Show("Shortcut already exists, please enter a new Shortcut or edit the existing one.");
            return;
        }

        DialogResult = !update || originalKey != Key || originalValue != Value;
        Close();
    }

    private void cmdEsc_OnPress(object sender, ExecutedRoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BtnTestShortcut_OnClick(object sender, RoutedEventArgs e)
    {
        IPublicAPI.Instance.ChangeQuery(tbExpand.Text);
        IPublicAPI.Instance.ShowMainWindow();
        Application.Current.MainWindow.Focus();
    }
}
