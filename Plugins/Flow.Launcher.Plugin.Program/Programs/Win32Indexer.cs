using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using Flow.Launcher.Interop.Files;
using Flow.Launcher.Interop.Shell;
using Flow.Launcher.Plugin.Program.Settings;
using Flow.Launcher.PluginSDK.Plugins;
using Microsoft.Win32;

namespace Flow.Launcher.Plugin.Program.Programs;

internal static class Win32Indexer
{
    internal enum SourceKind : byte
    {
        CustomDirectory = 0,
        StartMenu = 1,
        Registry = 2,
        Path = 3
    }

    private sealed class ExecutableKeyComparer : IEqualityComparer<(string ExecutablePath, string Args)>
    {
        public static readonly ExecutableKeyComparer Instance = new();

        public bool Equals((string ExecutablePath, string Args) x, (string ExecutablePath, string Args) y) =>
            string.Equals(x.ExecutablePath, y.ExecutablePath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Args, y.Args, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string ExecutablePath, string Args) obj) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.ExecutablePath),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Args));
    }

    private const string AppPathsRegistrySubKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";

    private static readonly string WindowsAppsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        "WindowsApps");

    private static readonly string LocalWindowsAppsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Microsoft",
        "WindowsApps");

    internal static Win32Program[] IndexAll(
        Settings.Settings settings,
        IReadOnlyList<UwpProgram> uwpPrograms,
        PluginInitContext? context = null)
    {
        HashSet<string> extensions = settings.GetAllExtensions();
        Dictionary<string, (string FullPath, SourceKind Kind)> candidates = new(StringComparer.OrdinalIgnoreCase);

        List<string> customDirs = GetCommonParents(
            settings.ProgramSources
                .Where(s => s.Enabled && !string.IsNullOrWhiteSpace(s.Location) && Directory.Exists(s.Location))
                .Select(s => s.Location));

        foreach (string dir in customDirs)
        {
            foreach (string filePath in EnumerateFilesInDirectory(dir, extensions, recursive: true))
            {
                AddCandidate(candidates, filePath, SourceKind.CustomDirectory);
            }
        }

        if (settings.EnableStartMenuSource)
        {
            string[] startupPaths = GetStartupPaths();
            foreach (string dir in GetStartMenuPaths())
            {
                foreach (string filePath in EnumerateFilesInDirectory(dir, extensions, recursive: true))
                {
                    if (!startupPaths.Any(startup => PathContainsOrEquals(startup, filePath)))
                    {
                        AddCandidate(candidates, filePath, SourceKind.StartMenu);
                    }
                }
            }
        }

        if (settings.EnableRegistrySource)
        {
            foreach (string filePath in EnumerateAppPathsFromRegistry(extensions, context))
            {
                AddCandidate(candidates, filePath, SourceKind.Registry);
            }
        }

        if (settings.EnablePathSource)
        {
            foreach (string filePath in EnumeratePathDirectories(extensions, customDirs))
            {
                AddCandidate(candidates, filePath, SourceKind.Path);
            }
        }

        if (candidates.Count == 0)
            return [];

        string[] uwpLocations = uwpPrograms
            .Select(u => u.Location)
            .Where(loc => !string.IsNullOrWhiteSpace(loc) && PathContainsOrEquals(WindowsAppsPath, loc))
            .Select(Path.TrimEndingDirectorySeparator)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        HashSet<string> uwpNames = uwpPrograms
            .Select(u => u.DisplayName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        (Win32Program Program, SourceKind Kind)[] parsed = candidates.Values
            .AsParallel()
            .Select(entry => (Program: CreateFromPath(entry.FullPath, context), entry.Kind))
            .Where(x => x.Program is not null && !IsDuplicatedUwpProgram(x.Program, uwpLocations, uwpNames))
            .Select(x => (x.Program!, x.Kind))
            .ToArray();

        return DeduplicatePrograms(parsed);
    }

    internal static Win32Program[] DeduplicatePrograms(IReadOnlyList<(Win32Program Program, SourceKind Kind)> entries)
    {
        if (entries.Count == 0)
            return [];

        return
        [
            .. entries
                .GroupBy(
                    x => (x.Program.ExecutablePath, x.Program.Args ?? string.Empty),
                    ExecutableKeyComparer.Instance)
                .Select(group => group.MaxBy(GetCandidatePriority).Program)
                .DistinctBy(p => p.UniqueIdentifier, StringComparer.OrdinalIgnoreCase)
        ];
    }

    internal static Win32Program? CreateFromPath(string path, PluginInitContext? context = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            string expandedPath = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
            if (string.IsNullOrWhiteSpace(expandedPath))
                return null;

            string ext = Path.GetExtension(expandedPath);
            string name = Path.GetFileNameWithoutExtension(expandedPath);
            string parentDir = Path.GetDirectoryName(expandedPath) ?? string.Empty;
            string uid = ProgramSource.NormalizeId(expandedPath);

            if (ext.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                return CreateShortcutProgram(expandedPath, name, parentDir, uid, context);
            }

            if (ext.Equals(".exe", StringComparison.OrdinalIgnoreCase))
            {
                return CreateExeProgram(expandedPath, name, parentDir, uid);
            }

            if (ext.Equals(".url", StringComparison.OrdinalIgnoreCase))
            {
                return CreateInternetShortcutProgram(expandedPath, name, parentDir, uid);
            }

            return new Win32Program
            {
                Name = name,
                IcoPath = expandedPath,
                FullPath = expandedPath,
                UniqueIdentifier = uid,
                ParentDirectory = parentDir,
                ProgramType = Win32ProgramType.Other,
                IsUninstaller = Win32Program.CheckIsUninstaller(expandedPath, null, name, string.Empty)
            };
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException or IOException or COMException or ArgumentException or PathTooLongException)
        {
            context?.Logger.LogError(ex, $"Failed to index program '{path}'");
            return null;
        }
    }

    private static Win32Program? CreateShortcutProgram(
        string expandedPath,
        string name,
        string parentDir,
        string uid,
        PluginInitContext? context)
    {
        ShortcutInfo info;
        try
        {
            info = ShortcutHelper.RetrieveShortcutInfo(expandedPath);
        }
        catch (Exception ex) when (ex is FileNotFoundException or COMException or PathTooLongException or IOException)
        {
            context?.Logger.LogDebug($"Failed to read shortcut '{expandedPath}': {ex.Message}");
            return null;
        }

        string? resolvedTarget = null;
        string? executableName = null;
        string description = info.Description;
        string? args = string.IsNullOrWhiteSpace(info.Arguments) ? null : info.Arguments;

        if (!string.IsNullOrWhiteSpace(info.TargetPath))
        {
            string expandedTarget = Environment.ExpandEnvironmentVariables(info.TargetPath);
            try
            {
                resolvedTarget = Path.GetFullPath(expandedTarget);
            }
            catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
            {
                resolvedTarget = expandedTarget;
            }

            if (!File.Exists(resolvedTarget) && !Directory.Exists(resolvedTarget))
                return null;

            if (File.Exists(resolvedTarget))
            {
                executableName = Path.GetFileNameWithoutExtension(resolvedTarget);
                if (string.IsNullOrWhiteSpace(description))
                {
                    description = TryGetFileDescription(resolvedTarget);
                }
            }
        }

        string localizedName = string.Empty;
        try
        {
            localizedName = ShellHelper.GetDisplayName(expandedPath);
            if (localizedName.Equals(name, StringComparison.OrdinalIgnoreCase))
                localizedName = string.Empty;
        }
        catch
        {
            // Ignore shell display name failures for shortcuts without localized metadata
        }

        return new Win32Program
        {
            Name = name,
            LocalizedName = localizedName,
            IcoPath = expandedPath,
            FullPath = expandedPath,
            UniqueIdentifier = uid,
            LnkResolvedPath = resolvedTarget,
            Args = args,
            ParentDirectory = parentDir,
            ExecutableName = executableName,
            Description = description,
            ProgramType = Win32ProgramType.Shortcut,
            IsUninstaller = Win32Program.CheckIsUninstaller(expandedPath, resolvedTarget, name, localizedName)
        };
    }

    private static Win32Program CreateExeProgram(string expandedPath, string name, string parentDir, string uid)
    {
        string description = TryGetFileDescription(expandedPath);
        return new Win32Program
        {
            Name = name,
            IcoPath = expandedPath,
            FullPath = expandedPath,
            UniqueIdentifier = uid,
            ParentDirectory = parentDir,
            Description = description,
            ProgramType = Win32ProgramType.Exe,
            IsUninstaller = Win32Program.CheckIsUninstaller(expandedPath, null, name, string.Empty)
        };
    }

    private static Win32Program? CreateInternetShortcutProgram(string expandedPath, string name, string parentDir, string uid)
    {
        InternetShortcutInfo shortcutInfo;
        try
        {
            shortcutInfo = InternetShortcutHelper.Parse(expandedPath);
        }
        catch
        {
            return null;
        }

        if (!IsNonWebAppUrl(shortcutInfo.Url))
            return null;

        string icoPath = expandedPath;
        if (!string.IsNullOrWhiteSpace(shortcutInfo.IconFile))
        {
            string expandedIcon = Environment.ExpandEnvironmentVariables(shortcutInfo.IconFile.Trim());
            string iconExt = Path.GetExtension(expandedIcon);
            if (ImageHelper.IsImageExtension(iconExt)
                && !iconExt.Equals(".ico", StringComparison.OrdinalIgnoreCase)
                && File.Exists(expandedIcon))
            {
                icoPath = expandedIcon;
            }
        }

        return new Win32Program
        {
            Name = name,
            IcoPath = icoPath,
            FullPath = expandedPath,
            LnkResolvedPath = shortcutInfo.Url,
            UniqueIdentifier = uid,
            ParentDirectory = parentDir,
            ProgramType = Win32ProgramType.InternetShortcut,
            IsUninstaller = Win32Program.CheckIsUninstaller(expandedPath, null, name, string.Empty)
        };
    }

    private static string TryGetFileDescription(string filePath)
    {
        try
        {
            return FileVersionInfo.GetVersionInfo(filePath).FileDescription ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    internal static bool IsNonWebAppUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        ReadOnlySpan<char> trimmed = url.AsSpan().Trim();
        int schemeSeparator = trimmed.IndexOf("://", StringComparison.Ordinal);
        if (schemeSeparator <= 0)
            return false;

        return !trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDuplicatedUwpProgram(
        Win32Program program,
        string[] uwpLocations,
        HashSet<string> uwpNames)
    {
        if (uwpLocations.Length > 0)
        {
            if (IsUnderAnyLocation(program.ParentDirectory, uwpLocations)
                || IsUnderAnyLocation(program.ExecutablePath, uwpLocations))
            {
                return true;
            }
        }

        if (uwpNames.Count > 0
            && !string.IsNullOrEmpty(program.ParentDirectory)
            && PathContainsOrEquals(LocalWindowsAppsPath, program.ParentDirectory))
        {
            if (uwpNames.Contains(program.Name)
                || (!string.IsNullOrEmpty(program.LocalizedName) && uwpNames.Contains(program.LocalizedName)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsUnderAnyLocation(string path, string[] locations)
    {
        if (string.IsNullOrEmpty(path) || !PathContainsOrEquals(WindowsAppsPath, path))
            return false;

        foreach (string location in locations)
        {
            if (PathContainsOrEquals(location, path))
                return true;
        }

        return false;
    }

    internal static int GetCandidatePriority((Win32Program Program, SourceKind Kind) item)
    {
        int score = item.Kind switch
        {
            SourceKind.CustomDirectory => 400,
            SourceKind.StartMenu => 300,
            SourceKind.Registry => 200,
            SourceKind.Path => 100,
            _ => 0
        };

        if (item.Program.ProgramType == Win32ProgramType.Shortcut)
            score += 20;

        if (!string.IsNullOrWhiteSpace(item.Program.Description))
            score += 10;

        return score;
    }

    private static void AddCandidate(
        Dictionary<string, (string FullPath, SourceKind Kind)> candidates,
        string filePath,
        SourceKind kind)
    {
        string key = ProgramSource.NormalizeId(filePath);
        if (string.IsNullOrEmpty(key))
            return;

        if (!candidates.TryGetValue(key, out (string FullPath, SourceKind Kind) existing) || kind < existing.Kind)
        {
            candidates[key] = (filePath, kind);
        }
    }

    private static IEnumerable<string> EnumerateFilesInDirectory(
        string directory,
        HashSet<string> extensions,
        bool recursive)
    {
        if (!Directory.Exists(directory))
            yield break;

        EnumerationOptions options = new()
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = recursive,
            AttributesToSkip = FileAttributes.System
        };

        HashSet<string>.AlternateLookup<ReadOnlySpan<char>> extLookup = extensions.GetAlternateLookup<ReadOnlySpan<char>>();
        foreach (string file in Directory.EnumerateFiles(directory, "*", options))
        {
            ReadOnlySpan<char> ext = Path.GetExtension(file);
            if (ext.Length > 1 && extLookup.Contains(ext[1..]))
            {
                yield return file;
            }
        }
    }

    private static IEnumerable<string> EnumerateAppPathsFromRegistry(
        HashSet<string> extensions,
        PluginInitContext? context)
    {
        HashSet<string>.AlternateLookup<ReadOnlySpan<char>> extLookup = extensions.GetAlternateLookup<ReadOnlySpan<char>>();
        List<string> paths = [];
        CollectFromRootKey(Registry.LocalMachine);
        CollectFromRootKey(Registry.CurrentUser);
        return paths;

        void CollectFromRootKey(RegistryKey root)
        {
            try
            {
                using RegistryKey? appPathsKey = root.OpenSubKey(AppPathsRegistrySubKey);
                if (appPathsKey is null)
                    return;

                foreach (string subKeyName in appPathsKey.GetSubKeyNames())
                {
                    try
                    {
                        using RegistryKey? subKey = appPathsKey.OpenSubKey(subKeyName);
                        if (subKey?.GetValue(string.Empty) is not string rawPath || string.IsNullOrWhiteSpace(rawPath))
                            continue;

                        string cleaned = Environment.ExpandEnvironmentVariables(rawPath.Trim('"', ' '));
                        ReadOnlySpan<char> ext = Path.GetExtension(cleaned);
                        if (ext.Length > 1 && extLookup.Contains(ext[1..]) && File.Exists(cleaned))
                        {
                            paths.Add(cleaned);
                        }
                    }
                    catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException)
                    {
                        context?.Logger.LogDebug($"Permission denied reading App Paths subkey '{subKeyName}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException)
            {
                context?.Logger.LogDebug($"Permission denied reading App Paths registry key: {ex.Message}");
            }
        }
    }

    private static IEnumerable<string> EnumeratePathDirectories(
        HashSet<string> extensions,
        List<string> customDirs)
    {
        string? pathEnv = Environment.GetEnvironmentVariable("Path");
        if (string.IsNullOrWhiteSpace(pathEnv))
            yield break;

        HashSet<string> visitedDirs = new(StringComparer.OrdinalIgnoreCase);
        foreach (string rawDir in pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string expandedDir = Environment.ExpandEnvironmentVariables(rawDir.Trim('"'));
            if (string.IsNullOrWhiteSpace(expandedDir))
                continue;

            string normalized = Path.TrimEndingDirectorySeparator(expandedDir);
            if (!visitedDirs.Add(normalized))
                continue;

            if (customDirs.Any(parent => PathContainsOrEquals(parent, normalized)))
                continue;

            foreach (string file in EnumerateFilesInDirectory(normalized, extensions, recursive: false))
            {
                yield return file;
            }
        }
    }

    internal static string[] GetStartMenuPaths()
    {
        return
        [
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
        ];
    }

    internal static string[] GetStartupPaths()
    {
        return
        [
            Environment.GetFolderPath(Environment.SpecialFolder.Startup),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup)
        ];
    }

    internal static List<string> GetCommonParents(IEnumerable<string> locations)
    {
        List<string> normalized = [];
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);

        foreach (string location in locations)
        {
            if (string.IsNullOrWhiteSpace(location))
                continue;

            string candidate;
            try
            {
                candidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(location.Trim()));
            }
            catch
            {
                candidate = Path.TrimEndingDirectorySeparator(location.Trim());
            }

            if (!string.IsNullOrEmpty(candidate) && seen.Add(candidate))
                normalized.Add(candidate);
        }

        normalized.Sort(static (a, b) => a.Length.CompareTo(b.Length));

        List<string> parents = [];
        foreach (string candidate in normalized)
        {
            if (!parents.Any(parent => PathContains(parent, candidate)))
            {
                parents.Add(candidate);
            }
        }

        return parents;
    }

    /// <summary>
    /// Returns true if <paramref name="subPath"/> is strictly inside <paramref name="parentPath"/>.
    /// </summary>
    internal static bool PathContains(string parentPath, string subPath)
    {
        if (string.IsNullOrWhiteSpace(parentPath) || string.IsNullOrWhiteSpace(subPath))
            return false;

        string parentWithSeparator = Path.EndsInDirectorySeparator(parentPath)
            ? parentPath
            : parentPath + Path.DirectorySeparatorChar;

        string rel = Path.GetRelativePath(parentWithSeparator, subPath);
        return rel != "."
            && rel != ".."
            && !rel.StartsWith("../", StringComparison.Ordinal)
            && !rel.StartsWith("..\\", StringComparison.Ordinal)
            && !Path.IsPathRooted(rel);
    }

    internal static bool PathContainsOrEquals(string parentPath, string subPath)
    {
        if (string.IsNullOrWhiteSpace(parentPath) || string.IsNullOrWhiteSpace(subPath))
            return false;

        return string.Equals(
            Path.TrimEndingDirectorySeparator(parentPath),
            Path.TrimEndingDirectorySeparator(subPath),
            StringComparison.OrdinalIgnoreCase)
            || PathContains(parentPath, subPath);
    }
}
