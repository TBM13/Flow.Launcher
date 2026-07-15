using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core.Hotkeys;
using Flow.Launcher.PluginSDK.Hotkeys;

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

        public HotkeyInfo? HotkeyInformation;
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
                    // TODO: Check if there is any better alternative
                    ? null : Ioc.Default.GetRequiredService<HotkeyManager>().GetHotkeyInformation(id);

                control.UpdateUI();
            }
        }

        public void GetNewHotkey(object sender, RoutedEventArgs e)
        {
            _ = OpenHotkeyDialogAsync();
        }

        private async Task OpenHotkeyDialogAsync()
        {
            if (HotkeyInformation is null)
                return;

            var dialog = new HotkeyControlDialog(HotkeyInformation, WindowTitle)
            {
                Owner = Window.GetWindow(this)
            };

            await dialog.ShowAsync();
            UpdateUI();
        }

        private void UpdateUI()
        {
            KeysToDisplay.Clear();

            if (HotkeyInformation is null || !HotkeyInformation.Hotkey.IsValid)
            {
                KeysToDisplay.Add("None");
                return;
            }

            foreach (var key in HotkeyInformation.Hotkey.ToString(includeLongPress: false).Split('+'))
            {
                KeysToDisplay.Add(key);
            }
        }
    }
}
