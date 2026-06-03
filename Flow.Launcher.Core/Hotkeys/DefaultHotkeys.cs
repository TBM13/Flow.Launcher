using System.Windows.Input;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure.API;
using Flow.Launcher.Infrastructure.Hotkeys;

namespace Flow.Launcher.Core;

public static class DefaultHotkeys
{
    // Global Hotkeys
    public static readonly GlobalHotkeyInfo
        ToggleFlowLauncher = new()
        {
            Id = "ToggleFlowLauncher",
            Name = "Toggle Flow Launcher",
            DefaultHotkey = new(modifiers: ModifierKeys.Windows),
            CanBeDisabled = false,
            OnHotkeyTriggered = () =>
            {
                if (IPublicAPI.Instance.IsMainWindowVisible())
                    IPublicAPI.Instance.HideMainWindow();
                else
                    IPublicAPI.Instance.ShowMainWindow();
            }
        },
        MagicQuery = new()
        {
            Id = "MagicQuery",
            Name = "Magic Query",
            DefaultHotkey = new(modifiers: ModifierKeys.Windows, longPress: true),
            OnHotkeyTriggered = () =>
            {
                // Generate magic query before showing the main window since
                // some plugins may need to know which window is focused right now
                string? query = Ioc.Default.GetRequiredService<PluginManager>().GenerateMagicQuery();

                if (!IPublicAPI.Instance.IsMainWindowVisible())
                    IPublicAPI.Instance.ShowMainWindow();

                if (query is not null)
                    IPublicAPI.Instance.ChangeQuery(query);
            }
        };

    // App hotkeys
    public static readonly AppHotkeyInfo
        TogglePreview = new()
        {
            Id = "TogglePreview",
            Name = "Toggle preview pane",
            DefaultHotkey = new(mainKey: Key.F1),
        },
        Autocomplete = new()
        {
            Id = "Autocomplete",
            Name = "Autocomplete",
            DefaultHotkey = new(mainKey: Key.Tab)
        },
        SelectNextResult = new()
        {
            Id = "SelectNextResult",
            Name = "Select next result",
            DefaultHotkey = new(mainKey: Key.Down)
        },
        SelectPreviousResult = new()
        {
            Id = "SelectPreviousResult",
            Name = "Select previous result",
            DefaultHotkey = new(mainKey: Key.Up)
        },
        SelectNextPage = new()
        {
            Id = "SelectNextPage",
            Name = "Select next page",
            DefaultHotkey = new(mainKey: Key.PageDown)
        },
        SelectPreviousPage = new()
        {
            Id = "SelectPreviousPage",
            Name = "Select previous page",
            DefaultHotkey = new(mainKey: Key.PageUp)
        },
        ShowContextMenu = new()
        {
            Id = "ShowContextMenu",
            Name = "Show result's context menu",
            DefaultHotkey = new(mainKey: Key.Right)
        },
        HideContextMenu = new()
        {
            Id = "HideContextMenu",
            Name = "Hide result's context menu",
            DefaultHotkey = new(mainKey: Key.Left)
        };

    // TODO: Add more hotkeys
    /*      new ("Escape", "HotkeyESCDesc"),
            new ("F5", "ReloadPluginHotkey"),
            new ("Alt+Home", "HotkeySelectFirstResult"),
            new ("Alt+End", "HotkeySelectLastResult"),
            new ("Ctrl+R", "HotkeyRequery"),
            new ("Ctrl+OemCloseBrackets", "QuickWidthHotkey"),
            new ("Ctrl+OemOpenBrackets", "QuickWidthHotkey"),
            new ("Ctrl+OemPlus", "QuickHeightHotkey"),
            new ("Ctrl+OemMinus", "QuickHeightHotkey"),
            new ("Ctrl+Shift+Enter", "HotkeyCtrlShiftEnterDesc"),
            new ("Shift+Enter", "OpenContextMenuHotkey"),
            new ("Enter", "HotkeyRunDesc"),
            new ("Ctrl+Enter", "OpenContainFolderHotkey"),
            new ("Alt+Enter", "HotkeyOpenResult"),
            new ("Ctrl+F12", "ToggleGameModeHotkey"),
            new ("Ctrl+Shift+C", "CopyFilePathHotkey")
    */

    public static readonly GlobalHotkeyInfo[] GlobalHotkeys = [
        ToggleFlowLauncher,
        MagicQuery
    ];

    public static readonly AppHotkeyInfo[] AppHotkeys = [
        TogglePreview,
        Autocomplete,
        SelectNextResult,
        SelectPreviousResult,
        SelectNextPage,
        SelectPreviousPage,
        ShowContextMenu,
        HideContextMenu
    ];
}
