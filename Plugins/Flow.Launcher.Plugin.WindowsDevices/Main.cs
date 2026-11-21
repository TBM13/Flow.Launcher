using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Controls;
using Flow.Launcher.Interop.Hardware;
using Flow.Launcher.Plugin.WindowsDevices.Settings;
using Flow.Launcher.PluginSDK;
using Flow.Launcher.PluginSDK.API;
using Flow.Launcher.PluginSDK.Plugins;
using Flow.Launcher.PluginSDK.Plugins.Interfaces;

namespace Flow.Launcher.Plugin.WindowsDevices;

public static class PluginMetadataDefinition
{
    public static readonly PluginMetadata Metadata = new()
    {
        ID = "8a7c4b9e-2d1f-4e6a-9c3b-5f8e1a2b3c4d",
        ActionKeywords = ["dev"],
        Name = "Windows Devices",
        Description = "List and manage Windows devices.",
        Author = "TBM13",
        Version = "1.0.0",
        IcoPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
            "devmgmt.msc"),

        Plugin = new Main()
    };
}

public sealed class Main : IPlugin, IContextMenu, ISettingProvider
{
    private const string WarningOverlayIcon = "Images/Plugin.WindowsDevices.Warning.png";
    private const string DisabledOverlayIcon = "Images/Plugin.WindowsDevices.Disabled.png";

    private PluginInitContext _context = null!;
    private IStringMatcher _matcher = null!;
    private Settings.Settings _settings = null!;
    private SettingsViewModel _viewModel = null!;

    private readonly Lock _cacheLock = new();
    private IDisposable? _notificationRegistration;
    private DeviceCache? _cache;
    private bool _cacheDirty = true;

    public Control CreateSettingPanel()
    {
        return new SettingsControl(_viewModel);
    }

    public void Init(PluginInitContext context)
    {
        _context = context;
        _matcher = context.API.StringMatcher;
        _settings = context.API.LoadSettingJsonStorage<Settings.Settings>();
        _viewModel = new SettingsViewModel(_settings);

        _notificationRegistration = DeviceHelper.RegisterDeviceNotification(InvalidateCache);
    }

    public List<Result>? Query(Query query)
    {
        if (query.IsHomeQuery)
            return null;

        bool isGlobalQuery = query.ActionKeyword.Length == 0;
        if (isGlobalQuery && string.IsNullOrWhiteSpace(query.Search))
            return null;

        DeviceViewSnapshot snapshot = GetSnapshot(_settings.ShowHiddenDevices);

        // On empty queries with action keyword, show all root categories
        if (!isGlobalQuery && string.IsNullOrWhiteSpace(query.Search))
        {
            return [.. snapshot.Categories.Select(
                c => CreateCategoryResult(c, query.ActionKeyword, score: 0, addSelectedCount: false))];
        }

        ReadOnlySpan<char> search = query.Search.Replace('\\', '/');
        CategoryEntry? selectedCategory = null;

        // If the query has a valid category path prefix, limit the search to that category's devices
        if (!isGlobalQuery)
        {
            int lastSeparatorIndex = search.LastIndexOf('/');
            if (lastSeparatorIndex != -1)
            {
                string path = search[..(lastSeparatorIndex + 1)].ToString();
                if (snapshot.CategoriesByPath.TryGetValue(path, out selectedCategory))
                {
                    search = search[(lastSeparatorIndex + 1)..];
                }
            }
        }

        if (selectedCategory is not null)
        {
            if (search.IsWhiteSpace())
                return [.. selectedCategory.Devices.Select(d => d.Result)];

            List<Result> categoryResults = [];
            foreach (DeviceEntry entry in selectedCategory.Devices)
            {
                MatchResult match = _matcher.FuzzySearch(search, entry.Device.Name);
                if (match.IsThresholdMet)
                    categoryResults.Add(entry.Result with { Score = match.Score, AddSelectedCount = true });
            }

            return categoryResults;
        }

        if (search.IsWhiteSpace())
        {
            return isGlobalQuery
                ? null
                : [.. snapshot.Categories.Select(
                    c => CreateCategoryResult(c, query.ActionKeyword, score: 0, addSelectedCount: false))];
        }

        List<Result> results = [];

        // Include matching categories only when queried with an action keyword
        if (!isGlobalQuery)
        {
            foreach (CategoryEntry category in snapshot.Categories)
            {
                MatchResult match = _matcher.FuzzySearch(search, category.ClassInfo.Name);
                if (match.IsThresholdMet)
                {
                    results.Add(CreateCategoryResult(
                        category,
                        query.ActionKeyword,
                        score: match.Score,
                        addSelectedCount: true));
                }
            }
        }

        foreach (DeviceEntry entry in snapshot.AllDevices)
        {
            MatchResult match = _matcher.FuzzySearch(search, entry.Device.Name);
            if (match.IsThresholdMet)
                results.Add(entry.Result with { Score = match.Score, AddSelectedCount = true });
        }

        return results;
    }

    public List<Result> LoadContextMenus(Result result, ActionContext ctx)
    {
        List<Result> menu = [];

        if (result.ContextData is DeviceInfo device)
        {
            if (device.IsDisabled)
            {
                menu.Add(new Result
                {
                    Title = "Enable device",
                    SubTitle = "Enable this device",
                    IconOrGlyph = "\xEB49",
                    Action = _ => RunPnpUtilDeviceAction("enable", ["/enable-device", device.InstanceId])
                });
            }
            else if (device.CanDisable)
            {
                menu.Add(new Result
                {
                    Title = "Disable device",
                    SubTitle = "Disable this device",
                    IconOrGlyph = "\xEB4A",
                    Action = _ => RunPnpUtilDeviceAction("disable", ["/disable-device", device.InstanceId])
                });
            }

            if (device.IsPresent && !device.IsDisabled)
            {
                menu.Add(new Result
                {
                    Title = "Restart device",
                    SubTitle = "Restart this device",
                    IconOrGlyph = "\xE777",
                    Action = _ => RunPnpUtilDeviceAction("restart", ["/restart-device", device.InstanceId])
                });
            }

            menu.Add(new Result
            {
                Title = "Uninstall device",
                SubTitle = "Uninstall this device from the system",
                IconOrGlyph = "\xE74D",
                Action = _ => RunPnpUtilDeviceAction("uninstall", ["/remove-device", device.InstanceId])
            });

            menu.Add(new Result
            {
                Title = "Scan for hardware changes",
                SubTitle = "Scan for Plug and Play hardware changes",
                IconOrGlyph = "\xE72C",
                Action = _ => RunPnpUtilDeviceAction("scan for hardware changes", ["/scan-devices"])
            });
        }
        else if (result.ContextData is DeviceClassInfo)
        {
            menu.Add(new Result
            {
                Title = "Scan for hardware changes",
                SubTitle = "Scan for Plug and Play hardware changes",
                IconOrGlyph = "\xE72C",
                Action = _ => RunPnpUtilDeviceAction("scan for hardware changes", ["/scan-devices"])
            });
        }

        return menu;
    }

    private void InvalidateCache()
    {
        Volatile.Write(ref _cacheDirty, true);
    }

    private DeviceViewSnapshot GetSnapshot(bool showHidden)
    {
        lock (_cacheLock)
        {
            if (_cache is null || Volatile.Read(ref _cacheDirty))
            {
                Volatile.Write(ref _cacheDirty, false);
                List<DeviceInfo> allDevices = DeviceHelper.GetDevices();
                _cache = BuildCache(allDevices);
            }

            return showHidden ? _cache.WithHidden : _cache.VisibleOnly;
        }
    }

    private DeviceCache BuildCache(List<DeviceInfo> devices)
    {
        List<DeviceEntry> allEntries = [with(devices.Count)];
        List<DeviceEntry> visibleEntries = [with(devices.Count)];

        foreach (DeviceInfo device in devices)
        {
            DeviceEntry entry = new(device, CreateDeviceResult(device));
            allEntries.Add(entry);
            if (!device.IsHidden)
                visibleEntries.Add(entry);
        }

        return new DeviceCache(
            VisibleOnly: BuildViewSnapshot(visibleEntries),
            WithHidden: BuildViewSnapshot(allEntries));
    }

    private static DeviceViewSnapshot BuildViewSnapshot(List<DeviceEntry> entries)
    {
        entries.Sort(static (a, b) => StringComparer.CurrentCultureIgnoreCase.Compare(a.Device.Name, b.Device.Name));

        Dictionary<Guid, (DeviceClassInfo ClassInfo, List<DeviceEntry> Devices)> byClass = [];
        foreach (DeviceEntry entry in entries)
        {
            DeviceClassInfo classInfo = entry.Device.Class;
            if (!byClass.TryGetValue(classInfo.ClassGuid, out var group))
            {
                group = (classInfo, []);
                byClass[classInfo.ClassGuid] = group;
            }

            group.Devices.Add(entry);
        }

        List<CategoryEntry> categories = [with(byClass.Count)];
        Dictionary<string, CategoryEntry> categoriesByPath = new(byClass.Count, StringComparer.OrdinalIgnoreCase);

        foreach ((DeviceClassInfo classInfo, List<DeviceEntry> classDevices) in byClass.Values)
        {
            string localPath = classInfo.Name + "/";
            CategoryEntry category = new(classInfo, localPath, classDevices);
            categories.Add(category);
            categoriesByPath.TryAdd(localPath, category);
        }

        categories.Sort(static (a, b) => StringComparer.CurrentCultureIgnoreCase.Compare(a.ClassInfo.Name, b.ClassInfo.Name));

        return new DeviceViewSnapshot(categories, categoriesByPath, entries);
    }

    private Result CreateCategoryResult(CategoryEntry category, string actionKeyword, int score, bool addSelectedCount)
    {
        string navigateQuery = string.IsNullOrEmpty(actionKeyword)
            ? category.LocalPath
            : $"{actionKeyword} {category.LocalPath}";

        return new Result
        {
            Title = category.ClassInfo.Name,
            AutocompleteText = (true, category.LocalPath),
            IconOrGlyph = category.ClassInfo.IconPath,
            CopyText = category.ClassInfo.Name,
            RecordKey = category.ClassInfo.ClassGuid.ToString(),
            ContextData = category.ClassInfo,
            Score = score,
            AddSelectedCount = addSelectedCount,
            Action = _ =>
            {
                _context.API.ChangeQuery(navigateQuery);
                return false;
            }
        };
    }

    private Result CreateDeviceResult(DeviceInfo device)
    {
        string? overlayIcon = device.HasWarning
            ? WarningOverlayIcon
            : device.IsDisabled
                ? DisabledOverlayIcon
                : null;

        return new Result
        {
            Title = device.Name,
            SubTitle = GetDeviceSubTitle(device),
            AutocompleteText = (true, $"{device.Class.Name}/{device.Name}"),
            IconOrGlyph = device.IconPath,
            OverlayImagePath = overlayIcon,
            CopyText = device.Name,
            ToolTip = GetDeviceToolTip(device),
            RecordKey = device.InstanceId,
            ContextData = device,
            AddSelectedCount = false,
            Action = _ => OpenDeviceProperties(device)
        };
    }

    private static string GetDeviceSubTitle(DeviceInfo device)
    {
        if (device.HasProblem)
        {
            if (!string.IsNullOrWhiteSpace(device.ProblemText))
                return $"{device.Class.Name}  •  {device.ProblemText}";

            return device.IsDisabled
                ? $"{device.Class.Name}  •  Disabled"
                : $"{device.Class.Name}  •  Problem code {device.ProblemCode}";
        }

        if (!device.IsPresent)
            return $"{device.Class.Name}  •  Not connected";

        return device.Class.Name;
    }

    private static string GetDeviceToolTip(DeviceInfo device)
    {
        StringBuilder sb = new();
        sb.AppendLine(device.Name);
        sb.Append($"Category: {device.Class.Name}");

        if (!string.IsNullOrWhiteSpace(device.Manufacturer))
            sb.Append($"\nManufacturer: {device.Manufacturer}");

        if (!string.IsNullOrWhiteSpace(device.LocationInfo))
            sb.Append($"\nLocation: {device.LocationInfo}");

        sb.Append($"\nInstance ID: {device.InstanceId}");

        if (device.HasProblem && !string.IsNullOrWhiteSpace(device.ProblemText))
            sb.Append($"\nStatus: {device.ProblemText}");
        else if (!device.IsPresent)
            sb.Append("\nStatus: Currently, this hardware device is not connected to the computer.");

        return sb.ToString();
    }

    private bool OpenDeviceProperties(DeviceInfo device)
    {
        try
        {
            DeviceHelper.OpenDeviceProperties(device.InstanceId);
            return true;
        }
        catch (Exception ex)
        {
            _context.Logger.LogError(ex, $"Failed to open properties for device '{device.InstanceId}'");
            _context.API.ShowMsgError("Failed to open device properties", ex.Message);
            return false;
        }
    }

    private bool RunPnpUtilDeviceAction(string actionDescription, ReadOnlySpan<string> arguments)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = Path.Combine(Environment.SystemDirectory, "pnputil.exe"),
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden
        };

        foreach (string arg in arguments)
            startInfo.ArgumentList.Add(arg);

        try
        {
            using Process? process = Process.Start(startInfo);
            if (process is null)
            {
                _context.API.ShowMsgError($"Failed to {actionDescription}", "Failed to start pnputil process.");
                return false;
            }

            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                _context.Logger.LogError($"pnputil exited with code {process.ExitCode} while trying to {actionDescription}");
                _context.API.ShowMsgError($"Failed to {actionDescription}", $"pnputil returned exit code {process.ExitCode}.");
                return false;
            }
        }
        catch (Win32Exception ex)
        {
            // User cancelled the UAC prompt (ERROR_CANCELLED = 1223)
            if (ex.NativeErrorCode == 1223)
                return false;

            _context.Logger.LogError(ex, $"Failed to {actionDescription}");
            _context.API.ShowMsgError($"Failed to {actionDescription}", ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            _context.Logger.LogError(ex, $"Failed to {actionDescription}");
            _context.API.ShowMsgError($"Failed to {actionDescription}", ex.Message);
            return false;
        }

        InvalidateCache();
        _context.API.ReQuery();
        return true;
    }

    public ValueTask DisposeAsync()
    {
        _notificationRegistration?.Dispose();
        _notificationRegistration = null;
        return ValueTask.CompletedTask;
    }

    private sealed record DeviceEntry(DeviceInfo Device, Result Result);

    private sealed record CategoryEntry(
        DeviceClassInfo ClassInfo,
        string LocalPath,
        IReadOnlyList<DeviceEntry> Devices);

    private sealed record DeviceViewSnapshot(
        IReadOnlyList<CategoryEntry> Categories,
        Dictionary<string, CategoryEntry> CategoriesByPath,
        IReadOnlyList<DeviceEntry> AllDevices);

    private sealed record DeviceCache(
        DeviceViewSnapshot VisibleOnly,
        DeviceViewSnapshot WithHidden);
}
