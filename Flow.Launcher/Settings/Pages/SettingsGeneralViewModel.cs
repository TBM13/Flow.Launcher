using Flow.Launcher.Core.Hotkeys;
using Flow.Launcher.Core.Settings;
using Flow.Launcher.Interop.Hardware;
using Flow.Launcher.PluginSDK.API;
using Flow.Launcher.PluginSDK.Logging;
using Flow.Launcher.PluginSDK.UI;

namespace Flow.Launcher.Settings.Pages;

public partial class SettingsGeneralViewModel(
    Logger<SettingsGeneralViewModel> logger, ISettingsAPI settings) : BaseSettingsPageViewModel
{
    private readonly Logger<SettingsGeneralViewModel> _logger = logger;

    public override string Title => "General";
    public override string IconPath => "pack://application:,,,/Images/settings.png";

    public ISettingsAPI Settings { get; } = settings;

    public List<int> ScreenNumbers
    {
        get
        {
            try
            {
                return [.. MonitorHelper.GetDisplayMonitors().Select((_, index) => index + 1)];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to get screen numbers");
                return [];
            }
        }
    }

    public IReadOnlyList<LocalizedEnumItem<DisplayType>> DisplayItems => EnumLocalization<DisplayType>.Items;

    public IReadOnlyList<LocalizedEnumItem<DisplayPosition>> DisplayPositionItems => EnumLocalization<DisplayPosition>.Items;

    public IReadOnlyList<LocalizedEnumItem<SearchPrecision>> SearchPrecisionItems => EnumLocalization<SearchPrecision>.Items;

    public IReadOnlyList<LocalizedEnumItem<LastQueryMode>> LastQueryModeItems => EnumLocalization<LastQueryMode>.Items;

    public static string AlwaysPreviewToolTip
        => $"Always open preview panel when Flow activates. Press {DefaultHotkeys.TogglePreview.Hotkey} to toggle preview.";
}
