using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core;
using Flow.Launcher.Infrastructure.Hotkeys;

namespace Flow.Launcher
{
    public partial class HotkeyControl
    {
        public string WindowTitle
        {
            get { return (string)GetValue(WindowTitleProperty); }
            set { SetValue(WindowTitleProperty, value); }
        }

        public static readonly DependencyProperty WindowTitleProperty = DependencyProperty.Register(
            nameof(WindowTitle),
            typeof(string),
            typeof(HotkeyControl),
            new PropertyMetadata(string.Empty)
        );

        public static readonly DependencyProperty IdProperty = DependencyProperty.Register(
            nameof(IdProperty),
            typeof(string),
            typeof(HotkeyControl),
            new PropertyMetadata(null, OnIdChanged)
        );

        public string? Id
        {
            get { return (string?)GetValue(IdProperty); }
            set { SetValue(IdProperty, value); }
        }

        public HotkeyInformation? HotkeyInformation;
        public ObservableCollection<string> KeysToDisplay { get; set; } = [];

        public HotkeyControl()
        {
            InitializeComponent();

            HotkeyList.ItemsSource = KeysToDisplay;
        }

        private static void OnIdChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is HotkeyControl control && e.NewValue is string id)
            {
                control.HotkeyInformation = string.IsNullOrEmpty(id)
                    ? null : HotkeyManager.GetHotkeyInformationById(id);

                control.SetKeysToDisplay(control.HotkeyInformation?.Hotkey);
            }
        }

        public void GetNewHotkey(object sender, RoutedEventArgs e)
        {
            _ = OpenHotkeyDialogAsync();
        }

        private async Task OpenHotkeyDialogAsync()
        {
            // TODO
            /*var dialog = new HotkeyControlDialog(HotkeyInformation.Hotkey, HotkeyInformation.DefaultHotkey, WindowTitle)
            {
                Owner = Window.GetWindow(this)
            };

            await dialog.ShowAsync();
            switch (dialog.ResultType)
            {
                case HotkeyControlDialog.EResultType.Cancel:
                    SetHotkey(Hotkey);
                    return;
                case HotkeyControlDialog.EResultType.Save:
                    SetHotkey(dialog.ResultValue);
                    break;
                case HotkeyControlDialog.EResultType.Delete:
                    Delete();
                    break;
            }*/
        }

        private void SetKeysToDisplay(Hotkey? hotkey)
        {
            KeysToDisplay.Clear();

            if (!hotkey.HasValue || !hotkey.Value.IsValid)
            {
                KeysToDisplay.Add("None");
                return;
            }

            foreach (var key in hotkey.Value.ToString().Split('+'))
            {
                KeysToDisplay.Add(key);
            }
        }
    }
}
