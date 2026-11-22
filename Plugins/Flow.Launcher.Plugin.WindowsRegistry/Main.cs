using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security;
using System.Windows.Controls;
using Flow.Launcher.Interop.Programs;
using Flow.Launcher.Plugin.WindowsRegistry.Settings;
using Flow.Launcher.PluginSDK;
using Flow.Launcher.PluginSDK.API;
using Flow.Launcher.PluginSDK.Plugins;
using Flow.Launcher.PluginSDK.Plugins.Interfaces;
using Microsoft.Win32;

namespace Flow.Launcher.Plugin.WindowsRegistry;

public static class PluginMetadataDefinition
{
    public static readonly PluginMetadata Metadata = new()
    {
        ID = "e5b6f372-7c9a-457b-922c-98a82985d87e",
        ActionKeywords = ["*", "reg"],
        Name = "Windows Registry",
        Description = "Navigate and search the Windows Registry.",
        Author = "TBM13",
        Version = "1.0.0",
        IcoPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "regedit.exe"),

        Plugin = new Main()
    };
}

public sealed class Main : IPlugin, ISettingProvider
{
    private const string DefaultValueName = "(Default)";
    private const string KeyGlyph = "\uF12B";
    private const int MaxBinaryPreviewBytes = 32;

    private const string StringValueIcon = "Images\\String.png";
    private const string DWordValueIcon = "Images\\DWord.png";
    private const string QWordValueIcon = "Images\\QWord.png";
    private const string BinaryValueIcon = "Images\\Binary.png";

    private sealed record RegistryHiveInfo(string FullName, string ShortName, RegistryKey Key);

    private static readonly RegistryHiveInfo[] RootHives =
    [
        new("HKEY_CLASSES_ROOT", "HKCR", Registry.ClassesRoot),
        new("HKEY_CURRENT_CONFIG", "HKCC", Registry.CurrentConfig),
        new("HKEY_CURRENT_USER", "HKCU", Registry.CurrentUser),
        new("HKEY_LOCAL_MACHINE", "HKLM", Registry.LocalMachine),
        new("HKEY_USERS", "HKU", Registry.Users),
    ];

    private PluginInitContext _context = null!;
    private IStringMatcher _matcher = null!;
    private Settings.Settings _settings = null!;
    private SettingsViewModel _viewModel = null!;

    public Control CreateSettingPanel()
    {
        return new SettingsControl(_viewModel);
    }

    public ValueTask InitAsync(PluginInitContext context)
    {
        _context = context;
        _matcher = context.API.StringMatcher;
        _settings = context.API.LoadSettingJsonStorage<Settings.Settings>();
        _viewModel = new SettingsViewModel(_settings);

        return ValueTask.CompletedTask;
    }

    public async IAsyncEnumerable<Result> QueryAsync(
        Query query,
        [EnumeratorCancellation] CancellationToken token)
    {
        if (query.IsHomeQuery)
            yield break;

        foreach (Result result in ExecuteQuery(query, token))
            yield return result;
    }

    private IEnumerable<Result> ExecuteQuery(Query query, CancellationToken token)
    {
        string search = query.Search.Replace('/', '\\');

        if (string.IsNullOrWhiteSpace(search))
        {
            if (query.ActionKeyword.Length > 0)
            {
                foreach (RegistryHiveInfo h in RootHives)
                {
                    token.ThrowIfCancellationRequested();
                    yield return CreateKeyResult(h.FullName, h.FullName, isRecursive: false, score: 0, addSelectedCount: false);
                }
            }
            yield break;
        }

        int firstSeparator = search.IndexOf('\\');
        if (firstSeparator == -1)
        {
            if (query.ActionKeyword.Length == 0)
                yield break;

            foreach (Result result in SearchRootHives(search, token))
                yield return result;

            yield break;
        }

        if (firstSeparator == 0)
            yield break;

        RegistryHiveInfo? hive = FindHive(search.AsSpan(0, firstSeparator));
        if (hive is null)
            yield break;

        int lastSeparator = search.LastIndexOf('\\');
        ReadOnlySpan<char> subKeyPath = lastSeparator > firstSeparator
            ? search.AsSpan(firstSeparator + 1, lastSeparator - firstSeparator - 1).Trim('\\')
            : [];

        RegistryKey? openedKey = null;
        RegistryKey targetKey;
        if (subKeyPath.IsEmpty)
        {
            targetKey = hive.Key;
        }
        else
        {
            openedKey = TryOpenSubKey(hive.Key, subKeyPath.ToString());
            if (openedKey is null)
                yield break;

            targetKey = openedKey;
        }

        try
        {
            string searchPart = search[(lastSeparator + 1)..];
            if (string.IsNullOrWhiteSpace(searchPart))
            {
                foreach (Result result in BrowseKey(targetKey, token))
                    yield return result;

                yield break;
            }

            if (searchPart.StartsWith('>'))
            {
                string recursiveSearch = searchPart[1..];
                if (string.IsNullOrWhiteSpace(recursiveSearch))
                    yield break;

                foreach (Result result in SearchRecursive(targetKey, recursiveSearch, token))
                    yield return result;

                yield break;
            }

            foreach (Result result in SearchKey(targetKey, searchPart, token))
                yield return result;
        }
        finally
        {
            openedKey?.Dispose();
        }
    }

    private IEnumerable<Result> SearchRootHives(string search, CancellationToken token)
    {
        foreach (RegistryHiveInfo hive in RootHives)
        {
            token.ThrowIfCancellationRequested();

            MatchResult match = _matcher.FuzzySearchBest(search, hive.FullName, hive.ShortName);
            if (match.IsThresholdMet)
            {
                yield return CreateKeyResult(
                    hive.FullName,
                    hive.FullName,
                    isRecursive: false,
                    score: match.Score,
                    addSelectedCount: true);
            }
        }
    }

    private static RegistryHiveInfo? FindHive(ReadOnlySpan<char> hiveName)
    {
        foreach (RegistryHiveInfo hive in RootHives)
        {
            if (hiveName.Equals(hive.FullName, StringComparison.OrdinalIgnoreCase) ||
                hiveName.Equals(hive.ShortName, StringComparison.OrdinalIgnoreCase))
            {
                return hive;
            }
        }

        return null;
    }

    private IEnumerable<Result> BrowseKey(RegistryKey key, CancellationToken token)
    {
        string[] subKeyNames = GetSortedSubKeyNames(key);
        string[] valueNames = GetSortedValueNames(key);
        string parentPath = key.Name;

        foreach (string subKeyName in subKeyNames)
        {
            token.ThrowIfCancellationRequested();
            yield return CreateKeyResult(
                subKeyName,
                $"{parentPath}\\{subKeyName}",
                isRecursive: false,
                score: 0,
                addSelectedCount: false);
        }

        foreach (string valueName in valueNames)
        {
            token.ThrowIfCancellationRequested();
            yield return CreateValueResult(
                key,
                valueName,
                isRecursive: false,
                score: 0,
                addSelectedCount: false);
        }
    }

    private IEnumerable<Result> SearchKey(RegistryKey key, string searchTerm, CancellationToken token)
    {
        string[] subKeyNames = GetSortedSubKeyNames(key);
        return MatchEntriesInKey(key, subKeyNames, searchTerm, isRecursive: false, token);
    }

    private IEnumerable<Result> SearchRecursive(RegistryKey key, string searchTerm, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        string[] subKeyNames = GetSortedSubKeyNames(key);
        foreach (Result result in MatchEntriesInKey(key, subKeyNames, searchTerm, isRecursive: true, token))
            yield return result;

        foreach (string subKeyName in subKeyNames)
        {
            token.ThrowIfCancellationRequested();

            using RegistryKey? subKey = TryOpenSubKey(key, subKeyName);
            if (subKey is not null)
            {
                foreach (Result childResult in SearchRecursive(subKey, searchTerm, token))
                    yield return childResult;
            }
        }
    }

    private IEnumerable<Result> MatchEntriesInKey(
        RegistryKey key,
        string[] subKeyNames,
        string searchTerm,
        bool isRecursive,
        CancellationToken token)
    {
        string[] valueNames = GetSortedValueNames(key);
        string parentPath = key.Name;

        foreach (string subKeyName in subKeyNames)
        {
            token.ThrowIfCancellationRequested();

            MatchResult match = _matcher.FuzzySearch(searchTerm, subKeyName);
            if (match.IsThresholdMet)
            {
                yield return CreateKeyResult(
                    subKeyName,
                    $"{parentPath}\\{subKeyName}",
                    isRecursive,
                    match.Score,
                    addSelectedCount: true);
            }
        }

        foreach (string valueName in valueNames)
        {
            token.ThrowIfCancellationRequested();

            string displayName = GetDisplayValueName(valueName);
            MatchResult match = _matcher.FuzzySearch(searchTerm, displayName);
            if (match.IsThresholdMet)
            {
                yield return CreateValueResult(
                    key,
                    valueName,
                    isRecursive,
                    match.Score,
                    addSelectedCount: true);
            }
        }
    }

    private Result CreateKeyResult(
        string keyName,
        string fullKeyPath,
        bool isRecursive,
        int score,
        bool addSelectedCount)
    {
        return new Result
        {
            Title = keyName,
            SubTitle = isRecursive ? fullKeyPath : string.Empty,
            AutocompleteText = (true, fullKeyPath + '\\'),
            CopyText = fullKeyPath,
            ToolTip = fullKeyPath,
            IconOrGlyph = KeyGlyph,
            Score = score,
            AddSelectedCount = addSelectedCount,
            Action = _ => OpenInRegJump(fullKeyPath)
        };
    }

    private Result CreateValueResult(
        RegistryKey parentKey,
        string valueName,
        bool isRecursive,
        int score,
        bool addSelectedCount)
    {
        string displayName = GetDisplayValueName(valueName);
        string parentPath = parentKey.Name;
        string fullPath = $"{parentPath}\\{displayName}";
        RegistryValueKind kind = GetValueKind(parentKey, valueName);
        string valueData = FormatValueData(parentKey, valueName, kind);

        return new Result
        {
            Title = displayName,
            SubTitle = isRecursive ? fullPath : valueData,
            AutocompleteText = (true, fullPath),
            CopyText = fullPath,
            ToolTip = string.IsNullOrEmpty(valueData) ? fullPath : $"{fullPath}\n\n{valueData}",
            IconOrGlyph = GetValueIcon(kind),
            Score = score,
            AddSelectedCount = addSelectedCount,
            Action = _ => OpenInRegJump(parentPath)
        };
    }

    private bool OpenInRegJump(string keyPath)
    {
        string regJumpPath = _settings.RegJumpPath.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(regJumpPath) || !File.Exists(regJumpPath))
        {
            _context.API.ShowMsgError(
                "RegJump not found",
                "Please configure the path to regjump.exe in the plugin settings.");
            return false;
        }

        try
        {
            ProcessHelper.StartProcess(
                regJumpPath, arguments: [keyPath],
                useShellExecute: true, verb: "runas");
            return true;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // User cancelled the UAC prompt
            return false;
        }
        catch (Exception ex)
        {
            _context.Logger.LogError(ex, $"Failed to open registry key '{keyPath}' using '{regJumpPath}'");
            _context.API.ShowMsgError("Failed to open registry key", ex.Message);
            return false;
        }
    }

    private static string GetDisplayValueName(string valueName) =>
        string.IsNullOrEmpty(valueName) ? DefaultValueName : valueName;

    private static RegistryKey? TryOpenSubKey(RegistryKey parentKey, string subKeyName)
    {
        try
        {
            return parentKey.OpenSubKey(subKeyName, writable: false);
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException or ArgumentException)
        {
            return null;
        }
    }

    private static string[] GetSortedSubKeyNames(RegistryKey key)
    {
        try
        {
            string[] names = key.GetSubKeyNames();
            Array.Sort(names, StringComparer.CurrentCultureIgnoreCase);
            return names;
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            return [];
        }
    }

    private static string[] GetSortedValueNames(RegistryKey key)
    {
        try
        {
            string[] names = key.GetValueNames();
            Array.Sort(names, StringComparer.CurrentCultureIgnoreCase);
            return names;
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            return [];
        }
    }

    private static RegistryValueKind GetValueKind(RegistryKey key, string valueName)
    {
        try
        {
            return key.GetValueKind(valueName);
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            return RegistryValueKind.Unknown;
        }
    }

    private static string GetValueIcon(RegistryValueKind kind) => kind switch
    {
        RegistryValueKind.String or RegistryValueKind.ExpandString or RegistryValueKind.MultiString => StringValueIcon,
        RegistryValueKind.DWord => DWordValueIcon,
        RegistryValueKind.QWord => QWordValueIcon,
        _ => BinaryValueIcon
    };

    private static string FormatValueData(RegistryKey key, string valueName, RegistryValueKind kind)
    {
        try
        {
            object? value = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (value is null)
                return string.Empty;

            return kind switch
            {
                RegistryValueKind.String or RegistryValueKind.ExpandString => value.ToString() ?? string.Empty,
                RegistryValueKind.MultiString when value is string[] lines => string.Join(" ", lines),
                RegistryValueKind.DWord => FormatDWord(value),
                RegistryValueKind.QWord => FormatQWord(value),
                RegistryValueKind.Binary when value is byte[] bytes => FormatBinary(bytes),
                _ => value.ToString() ?? string.Empty
            };
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException)
        {
            return string.Empty;
        }
    }

    private static string FormatDWord(object value)
    {
        uint raw = unchecked((uint)Convert.ToInt32(value));
        return $"0x{raw:x8} ({raw})";
    }

    private static string FormatQWord(object value)
    {
        ulong raw = unchecked((ulong)Convert.ToInt64(value));
        return $"0x{raw:x16} ({raw})";
    }

    private static string FormatBinary(byte[] bytes)
    {
        if (bytes.Length == 0)
            return "(zero-length binary value)";

        if (bytes.Length <= MaxBinaryPreviewBytes)
            return BitConverter.ToString(bytes).Replace('-', ' ');

        string prefix = BitConverter.ToString(bytes, 0, MaxBinaryPreviewBytes).Replace('-', ' ');
        return $"{prefix} ... ({bytes.Length} bytes)";
    }
}
