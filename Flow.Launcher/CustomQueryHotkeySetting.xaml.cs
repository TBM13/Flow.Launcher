using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Flow.Launcher.Core.UserSettings;
using Flow.Launcher.PluginSDK.API;

namespace Flow.Launcher
{
    public partial class CustomQueryHotkeySetting : Window
    {
        public string Hotkey { get; set; } = string.Empty;
        public string ActionKeyword { get; set; } = string.Empty;

        private readonly bool update;
        private readonly CustomPluginHotkey originalCustomHotkey;

        public CustomQueryHotkeySetting()
        {
            InitializeComponent();
            tbAdd.Visibility = Visibility.Visible;
        }

        public CustomQueryHotkeySetting(CustomPluginHotkey hotkey)
        {
            originalCustomHotkey = hotkey;
            update = true;
            ActionKeyword = originalCustomHotkey.ActionKeyword;
            InitializeComponent();
            tbUpdate.Visibility = Visibility.Visible;

            // TODO
            //HotkeyControl.SetHotkey(originalCustomHotkey.Hotkey, false);
        }

        private void BtnCancel_OnClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            DialogResult = false;
            Close();
        }

        private void btnAdd_OnClick(object sender, RoutedEventArgs e)
        {
            // TODO
            //Hotkey = HotkeyControl.CurrentHotkey.ToString();

            if (string.IsNullOrEmpty(Hotkey) && string.IsNullOrEmpty(ActionKeyword))
            {
                IPublicAPI.Instance.ShowMsgBox("Hotkey and action keyword are empty");
                return;
            }

            DialogResult = !update || originalCustomHotkey.Hotkey != Hotkey || originalCustomHotkey.ActionKeyword != ActionKeyword;
            Close();
        }

        private void BtnTestActionKeyword_OnClick(object sender, RoutedEventArgs e)
        {
            IPublicAPI.Instance.ChangeQuery(tbAction.Text);
            IPublicAPI.Instance.ShowMainWindow();
            Application.Current.MainWindow.Focus();
        }

        private void cmdEsc_OnPress(object sender, ExecutedRoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void window_MouseDown(object sender, MouseButtonEventArgs e) /* for close hotkey popup */
        {
            if (Keyboard.FocusedElement is not TextBox textBox) return;

            TraversalRequest tRequest = new TraversalRequest(FocusNavigationDirection.Next);
            textBox.MoveFocus(tRequest);
        }
    }
}
