using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using Flow.Launcher.Interop.Programs;
using Flow.Launcher.Interop.Shell;
using Flow.Launcher.PluginSDK;
using Flow.Launcher.PluginSDK.API;
using Flow.Launcher.PluginSDK.Hotkeys;
using Flow.Launcher.PluginSDK.Plugins;
using MemoryPack;

namespace Flow.Launcher.Plugin.Program.Programs;

public enum Win32ProgramType : byte
{
    Exe,
    Shortcut,
    InternetShortcut,
    Other
}

[MemoryPackable]
public sealed partial class Win32Program : IProgram, IEquatable<Win32Program>
{
    private static readonly HashSet<string> CommonUninstallerFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "uninst.exe",
        "unins000.exe",
        "unins001.exe",
        "uninst000.exe",
        "uninstall.exe"
    };

    private static readonly string[] CommonUninstallerPrefixes =
    [
        "uninstall",       // en
        "卸载",             // zh-cn
        "卸載",             // zh-tw
        "видалити",        // uk-UA
        "удалить",         // ru
        "désinstaller",    // fr
        "アンインストール", // ja
        "deïnstalleren",   // nl
        "odinstaluj",      // pl
        "afinstallere",    // da
        "deinstallieren",  // de
        "삭제",             // ko
        "деинсталирај",    // sr
        "desinstalar",     // pt-pt / pt-br / es / es-419
        "disinstallare",   // it
        "avinstallere",    // nb-NO
        "odinštalovať",    // sk
        "kaldır",          // tr
        "odinstalovat",    // cs
        "إلغاء التثبيت",   // ar
        "gỡ bỏ",           // vi-vn
        "הסרה"             // he
    ];

    public string Name { get; init; } = string.Empty;
    public string LocalizedName { get; init; } = string.Empty;
    public string UniqueIdentifier { get; init; } = string.Empty;
    public string IcoPath { get; init; } = string.Empty;
    public string FullPath { get; init; } = string.Empty;
    public string? LnkResolvedPath { get; init; }
    public string? Args { get; init; }
    public string ParentDirectory { get; init; } = string.Empty;
    public string? ExecutableName { get; init; }
    public string Description { get; init; } = string.Empty;
    public Win32ProgramType ProgramType { get; init; }
    public bool IsUninstaller { get; init; }

    [MemoryPackIgnore]
    public string ExecutablePath => LnkResolvedPath ?? FullPath;

    [MemoryPackIgnore]
    public string Location => ParentDirectory;

    [MemoryPackIgnore]
    public string DisplayName => !string.IsNullOrEmpty(LocalizedName)
        && !Name.Equals(LocalizedName, StringComparison.OrdinalIgnoreCase)
        ? LocalizedName
        : Name;

    public Win32Program()
    {
    }

    public Result? CreateResult(ReadOnlySpan<char> query, Settings.Settings settings, PluginInitContext context)
    {
        if (settings.HideUninstallers && IsUninstaller)
            return null;

        bool useLocalized = !string.IsNullOrEmpty(LocalizedName)
            && !Name.Equals(LocalizedName, StringComparison.OrdinalIgnoreCase);
        string displayName = useLocalized ? LocalizedName : Name;

        bool showDescription = settings.EnableDescription
            && !string.IsNullOrWhiteSpace(Description)
            && !displayName.Equals(Description, StringComparison.OrdinalIgnoreCase);

        int score = 0;
        if (!query.IsEmpty)
        {
            IStringMatcher matcher = context.API.StringMatcher;
            MatchResult match = matcher.FuzzySearch(query, displayName);

            if (showDescription)
            {
                MatchResult descMatch = matcher.FuzzySearch(query, Description);
                if (descMatch.Score > match.Score || (!match.IsThresholdMet && descMatch.IsThresholdMet))
                    match = descMatch;
            }

            if (useLocalized)
            {
                MatchResult nameMatch = matcher.FuzzySearch(query, Name);
                if (nameMatch.Score > match.Score || (!match.IsThresholdMet && nameMatch.IsThresholdMet))
                    match = nameMatch;
            }

            if (!string.IsNullOrEmpty(ExecutableName)
                && !ExecutableName.Equals(displayName, StringComparison.OrdinalIgnoreCase))
            {
                MatchResult exeMatch = matcher.FuzzySearch(query, ExecutableName);
                if (exeMatch.Score > match.Score || (!match.IsThresholdMet && exeMatch.IsThresholdMet))
                    match = exeMatch;
            }

            if (!match.IsThresholdMet || match.Score <= 0)
                return null;

            score = match.Score;
        }

        string title = showDescription ? $"{displayName}: {Description}" : displayName;

        string subtitle = settings.HideAppsPath
            ? string.Empty
            : ProgramType == Win32ProgramType.InternetShortcut
                ? (LnkResolvedPath ?? FullPath)
                : FullPath;

        string autoPath = ProgramType == Win32ProgramType.Shortcut && !string.IsNullOrEmpty(LnkResolvedPath)
            ? LnkResolvedPath
            : FullPath;

        return new Result
        {
            Title = title,
            SubTitle = subtitle,
            AutocompleteText = (false, autoPath),
            CopyText = FullPath,
            RecordKey = UniqueIdentifier,
            IconOrGlyph = IcoPath,
            Score = score,
            ContextData = this,
            ToolTip = string.IsNullOrEmpty(subtitle)
                ? $"{title}\n{ExecutablePath}"
                : $"{title}\n{ExecutablePath}\n\n{subtitle}",
            Action = c =>
            {
                if (c.PressedKeys.OnlyModifiersPressed(ModifierKeys.Control))
                {
                    FileExplorerHelper.OpenFolderAndSelectFile(FullPath);
                    return true;
                }

                bool runAsAdmin = c.PressedKeys.OnlyModifiersPressed(ModifierKeys.Control | ModifierKeys.Shift);
                Launch(context, runAsAdmin ? "runas" : string.Empty);
                return true;
            }
        };
    }

    public List<Result> CreateContextMenus(PluginInitContext context)
    {
        List<Result> menus =
        [
            new Result
            {
                Title = "Run as administrator",
                IconOrGlyph = "\xe7ef",
                Action = _ =>
                {
                    Launch(context, "runas");
                    return true;
                }
            },
            new Result
            {
                Title = "Run as different user",
                IconOrGlyph = "\xe7ee",
                Action = _ =>
                {
                    Launch(context, "RunAsUser");
                    return true;
                }
            },
            new Result
            {
                Title = "Open containing folder",
                IconOrGlyph = "\xe838",
                Action = _ =>
                {
                    FileExplorerHelper.OpenFolderAndSelectFile(FullPath);
                    return true;
                }
            }
        ];

        if (ProgramType == Win32ProgramType.Shortcut && !string.IsNullOrEmpty(LnkResolvedPath))
        {
            menus.Add(new Result
            {
                Title = "Open target folder",
                IconOrGlyph = "\xe8de",
                Action = _ =>
                {
                    FileExplorerHelper.OpenFolderAndSelectFile(LnkResolvedPath);
                    return true;
                }
            });
        }

        return menus;
    }

    private void Launch(PluginInitContext context, string verb)
    {
        _ = Task.Run(() =>
        {
            try
            {
                ProcessHelper.StartProcess(
                    FullPath,
                    workingDirectory: ParentDirectory,
                    useShellExecute: true,
                    verb: verb);
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                // User cancelled the UAC prompt
            }
            catch (Exception ex)
            {
                context.Logger.LogError(ex, $"Unable to run program '{FullPath}'");
                context.API.ShowMsgError("Error", $"Unable to run {FullPath}: {ex.Message}");
            }
        });
    }

    internal static bool CheckIsUninstaller(string fullPath, string? lnkResolvedPath, string name, string localizedName)
    {
        string fullFileName = Path.GetFileName(fullPath);
        if (CommonUninstallerFileNames.Contains(fullFileName))
            return true;

        if (!string.IsNullOrEmpty(lnkResolvedPath))
        {
            string targetFileName = Path.GetFileName(lnkResolvedPath);
            if (CommonUninstallerFileNames.Contains(targetFileName))
                return true;

            string targetNameWithoutExt = Path.GetFileNameWithoutExtension(lnkResolvedPath);
            if (StartsWithUninstallerPrefix(targetNameWithoutExt))
                return true;
        }

        string fileNameWithoutExt = Path.GetFileNameWithoutExtension(fullPath);
        return StartsWithUninstallerPrefix(fileNameWithoutExt)
            || StartsWithUninstallerPrefix(name)
            || StartsWithUninstallerPrefix(localizedName);
    }

    private static bool StartsWithUninstallerPrefix(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        foreach (string prefix in CommonUninstallerPrefixes)
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public bool Equals(Win32Program? other) =>
        other is not null && string.Equals(UniqueIdentifier, other.UniqueIdentifier, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => Equals(obj as Win32Program);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(UniqueIdentifier);

    public override string ToString() => DisplayName;
}
