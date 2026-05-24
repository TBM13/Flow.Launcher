using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows.Input;
using Flow.Launcher.Infrastructure.API;
using Flow.Launcher.Infrastructure.Helpers;
using Flow.Launcher.Infrastructure.Results;
using Flow.Launcher.Infrastructure.WPF;
using Flow.Launcher.Interop.Programs;
using Flow.Launcher.Plugin.Program.Views.Models;
using MemoryPack;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using ZLogger;

namespace Flow.Launcher.Plugin.Program.Programs
{
    [MemoryPackable]
    public partial class Win32 : IProgram, IEquatable<Win32>
    {
        public string Name { get; set; }

        public string UniqueIdentifier
        {
            get => _uid;
            set => _uid = value == null ? string.Empty : value.ToLowerInvariant();
        } // For path comparison

        public string IcoPath { get; set; }

        /// <summary>
        /// Path of the file. It's the path of .lnk and .url for .lnk and .url files.
        /// </summary>
        public string FullPath { get; set; }

        /// <summary>
        /// Path of the executable for .lnk, or the URL for .url
        /// </summary>
        public string LnkResolvedPath { get; set; }

        /// <summary>
        /// Path of the actual executable file
        /// </summary>
        public string ExecutablePath => LnkResolvedPath ?? FullPath;

        /// <summary>
        /// Arguments for the executable.
        /// </summary>
        public string Args { get; set; }

        public string ParentDirectory { get; set; }

        /// <summary>
        /// Name of the executable for .lnk files
        /// </summary>
        public string ExecutableName { get; set; }

        public string Description { get; set; }
        public bool Valid { get; set; }
        public bool Enabled { get; set; }
        public string Location => ParentDirectory;

        // Localized name based on windows display language
        public string LocalizedName { get; set; } = string.Empty;

        private const string ShortcutExtension = "lnk";
        private const string UrlExtension = "url";
        private const string ExeExtension = "exe";
        private string _uid = string.Empty;

        private static readonly Win32 Default = new()
        {
            Name = string.Empty,
            Description = string.Empty,
            IcoPath = string.Empty,
            FullPath = string.Empty,
            LnkResolvedPath = null,
            ParentDirectory = string.Empty,
            ExecutableName = null,
            UniqueIdentifier = string.Empty,
            Valid = false,
            Enabled = false
        };

        private static MatchResult Match(string query, IReadOnlyCollection<string> candidates)
        {
            if (candidates.Count == 0)
                return null;

            var match = candidates.Select(candidate => Main.Context.API.FuzzySearch(query, candidate))
                .MaxBy(match => match.Score);

            return match?.IsSearchPrecisionScoreMet() ?? false ? match : null;
        }

        public Result Result(string query, IPublicAPI api)
        {
            string title;
            MatchResult matchResult;

            // Name of the result
            // Check equality to avoid matching again in candidates
            bool useLocalizedName = !string.IsNullOrEmpty(LocalizedName) && !Name.Equals(LocalizedName);
            string resultName = useLocalizedName ? LocalizedName : Name;

            if (!Main._settings.EnableDescription || string.IsNullOrWhiteSpace(Description) ||
                resultName.Equals(Description))
            {
                title = resultName;
                matchResult = Main.Context.API.FuzzySearch(query, resultName);
            }
            else
            {
                // Search in both
                title = $"{resultName}: {Description}";
                var nameMatch = Main.Context.API.FuzzySearch(query, resultName);
                var descriptionMatch = Main.Context.API.FuzzySearch(query, Description);
                if (descriptionMatch.Score > nameMatch.Score)
                {
                    matchResult = descriptionMatch;
                }
                else
                {
                    matchResult = nameMatch;
                }
            }

            List<string> candidates = [];

            if (!matchResult.IsSearchPrecisionScoreMet() && !string.IsNullOrEmpty(query))
            {
                if (ExecutableName != null) // only lnk program will need this one
                {
                    candidates.Add(ExecutableName);
                }

                if (useLocalizedName)
                {
                    candidates.Add(Name);
                }

                matchResult = Match(query, candidates);
                if (matchResult == null)
                {
                    return null;
                }
            }

            string subtitle = string.Empty;
            if (!Main._settings.HideAppsPath)
            {
                if (Extension(FullPath) == UrlExtension)
                {
                    subtitle = LnkResolvedPath;
                }
                else
                {
                    subtitle = FullPath;
                }
            }

            string autocompleteText = Extension(FullPath) == ShortcutExtension && !string.IsNullOrEmpty(LnkResolvedPath)
                ? LnkResolvedPath : FullPath;
            if (!Path.EndsInDirectorySeparator(autocompleteText) && Directory.Exists(autocompleteText))
            {
                autocompleteText += Path.DirectorySeparatorChar;
            }

            var result = new Result
            {
                Title = title,
                AutoCompleteText = autocompleteText,
                SubTitle = subtitle,
                IcoPath = IcoPath,
                Score = matchResult.Score,
                ContextData = this,
                TitleToolTip = $"{title}\n{ExecutablePath}",
                Action = c =>
                {
                    // Ctrl + Enter to open containing folder
                    bool openFolder = c.PressedKeys == ModifierKeys.Control;
                    if (openFolder)
                    {
                        Main.Context.API.OpenDirectory(ParentDirectory, FullPath);
                        return true;
                    }

                    // Ctrl + Shift + Enter to run as admin
                    bool runAsAdmin = c.PressedKeys == (ModifierKeys.Control | ModifierKeys.Shift);
                    Launch(runAsAdmin);

                    return true;
                }
            };

            return result;
        }

        private void Launch(bool runAsAdmin = false)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    ProcessHelper.StartProcess(
                        FullPath,
                        workingDirectory: ParentDirectory,
                        useShellExecute: true,
                        verb: runAsAdmin ? "runas" : "");
                }
                catch (Exception e)
                {
                    Main.Context.API.ShowMsgError(
                        Localize.Error_Title,
                        Localize.Error_UnableToRun(FullPath, e.Message));
                }
            });
        }

        public List<Result> ContextMenus(IPublicAPI api)
        {
            var contextMenus = new List<Result>
            {
                new()
                {
                    Title = Localize.Action_RunAsDifferentUser,
                    Action = c =>
                    {
                        _ = Task.Run(() =>
                        {
                            try
                            {
                                ProcessHelper.StartProcess(FullPath, workingDirectory: ParentDirectory,
                                    useShellExecute: true, verb: "RunAsUser");
                            }
                            catch (Exception e)
                            {
                                Main.Context.API.ShowMsgError(
                                    Localize.Error_Title, Localize.Error_UnableToRun(FullPath, e.Message));
                            }
                        });

                        return true;
                    },
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe7ee"),
                },
                new()
                {
                    Title = Localize.Action_RunAsAdministrator,
                    Action = c =>
                    {
                        Launch(true);
                        return true;
                    },
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe7ef"),
                },
                new()
                {
                    Title = Localize.Action_OpenContainingFolder,
                    Action = _ =>
                    {
                        Main.Context.API.OpenDirectory(ParentDirectory, FullPath);

                        return true;
                    },
                    Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe838"),
                },
            };
            if (Extension(FullPath) == ShortcutExtension)
            {
                contextMenus.Add(OpenTargetFolderContextMenuResult(api));
            }
            return contextMenus;
        }

        private Result OpenTargetFolderContextMenuResult(IPublicAPI api)
        {
            return new Result
            {
                Title = Localize.Action_OpenTargetFolder,
                Action = _ =>
                {
                    api.OpenDirectory(Path.GetDirectoryName(ExecutablePath), ExecutablePath);
                    return true;
                },
                Glyph = new GlyphInfo(FontFamily: "/Resources/#Segoe Fluent Icons", Glyph: "\xe8de"),
            };
        }

        public override string ToString()
        {
            return Name;
        }

        private static readonly List<FileSystemWatcher> Watchers = [];

        private static Win32 Win32Program(string path)
        {
            try
            {
                var p = new Win32
                {
                    Name = Path.GetFileNameWithoutExtension(path),
                    IcoPath = path,
                    FullPath = path,
                    UniqueIdentifier = path,
                    ParentDirectory = Directory.GetParent(path).FullName,
                    Description = string.Empty,
                    Valid = true,
                    Enabled = true
                };
                return p;
            }
            catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
            {
                Main.Context.Logger.LogError(e, $"Permission denied when trying to load the program from {path}");
                return Default;
            }
#if !DEBUG
            catch (Exception e)
            {
                Main.Context.Logger.LogError(e, $"An unexpected error occurred in the calling method Win32Program for path {path}");
                return Default;
            }
#endif
        }

        private static Win32 LnkProgram(string path)
        {
            var program = Win32Program(path);
            try
            {
                const int MAX_PATH = 260;
                StringBuilder buffer = new StringBuilder(MAX_PATH);
                string target = ShortcutHelper.RetrieveTargetPath(path);
                (string description, string args) = ShortcutHelper.RetrieveDescriptionAndArgs(path);

                program.LnkResolvedPath = Path.GetFullPath(target);
                if (!string.IsNullOrEmpty(target) && File.Exists(target))
                {
                    program.ExecutableName = Path.GetFileNameWithoutExtension(target);

                    if (!string.IsNullOrEmpty(args))
                    {
                        program.Args = args;
                    }

                    if (!string.IsNullOrEmpty(description))
                    {
                        program.Description = description;
                    }
                    else
                    {
                        var info = FileVersionInfo.GetVersionInfo(target);
                        if (!string.IsNullOrEmpty(info.FileDescription))
                        {
                            program.Description = info.FileDescription;
                        }
                    }
                }

                program.LocalizedName = ShellLocalization.GetLocalizedName(path);

                return program;
            }
            catch (FileNotFoundException e)
            {
                Main.Context.Logger.LogError(e, $"An unexpected error occurred in the calling method LnkProgram for path {path}");
                return Default;
            }
            catch (Exception e)
            {
                Main.Context.Logger.LogError(e, $"An unexpected error occurred in the calling method LnkProgram for path {path}");
                return Default;
            }
        }

        private static Win32 UrlProgram(string path, string[] protocols)
        {
            var program = Win32Program(path);
            program.Valid = false;

            try
            {
                var shortcutInfo = InternetShortcutHelper.Parse(path);
                if (string.IsNullOrEmpty(shortcutInfo.Url))
                    return program;

                foreach (var protocol in protocols)
                {
                    if (shortcutInfo.Url.StartsWith(protocol))
                    {
                        program.LnkResolvedPath = shortcutInfo.Url;
                        program.Valid = true;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(shortcutInfo.IconFile))
                {
                    program.IcoPath = shortcutInfo.IconFile;
                }
            }
            catch (Exception)
            {
                // Many files do not have the required fields, so no logging is done.
            }

            return program;
        }

        private static Win32 ExeProgram(string path)
        {
            try
            {
                var program = Win32Program(path);
                var info = FileVersionInfo.GetVersionInfo(path);
                if (!string.IsNullOrEmpty(info.FileDescription))
                    program.Description = info.FileDescription;
                return program;
            }
            catch (FileNotFoundException e)
            {
                Main.Context.Logger.LogError(e, $"File not found when trying to load the program from {path}");
                return Default;
            }
            catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
            {
                Main.Context.Logger.LogError(e, $"Permission denied when trying to load the program from {path}", e);
                return Default;
            }
        }

        private static IEnumerable<string> EnumerateProgramsInDir(string directory, string[] suffixes,
            bool recursive = true)
        {
            if (!Directory.Exists(directory))
                return [];

            return Directory.EnumerateFiles(
                    directory, "*",
                    new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = recursive })
                .Where(x => suffixes.Contains(Extension(x)));
        }

        private static string Extension(string path)
        {
            var extension = Path.GetExtension(path)?.ToLowerInvariant();
            if (!string.IsNullOrEmpty(extension))
            {
                return extension[1..]; // remove dot
            }
            else
            {
                return string.Empty;
            }
        }

        private static IEnumerable<Win32> UnregisteredPrograms(List<string> directories, string[] suffixes,
            string[] protocols)
        {
            // Disabled custom sources are not in DisabledProgramSources
            var paths = directories.AsParallel()
                .SelectMany(s => EnumerateProgramsInDir(s, suffixes));

            // Remove disabled programs in DisabledProgramSources
            var programs = ExceptDisabledSource(paths).Select(x => GetProgramFromPath(x, protocols));
            return programs;
        }

        private static IEnumerable<Win32> StartMenuPrograms(string[] suffixes, string[] protocols)
        {
            var allPrograms = GetStartMenuPaths()
                .SelectMany(p => EnumerateProgramsInDir(p, suffixes))
                .Distinct();

            var startupPaths = GetStartupPaths();

            var programs = ExceptDisabledSource(allPrograms)
                .Where(x => !startupPaths.Any(startup => PathContains(startup, x)))
                .Select(x => GetProgramFromPath(x, protocols));
            return programs;
        }

        private static IEnumerable<Win32> PATHPrograms(string[] suffixes, string[] protocols,
            List<string> commonParents)
        {
            var pathEnv = Environment.GetEnvironmentVariable("Path");
            if (string.IsNullOrEmpty(pathEnv))
            {
                return [];
            }

            var paths = pathEnv.Split(";", StringSplitOptions.RemoveEmptyEntries).DistinctBy(p => p.ToLowerInvariant());

            var toFilter = paths.Where(x => commonParents.All(parent => !PathContains(parent, x)))
                .AsParallel()
                .SelectMany(p => EnumerateProgramsInDir(p, suffixes, recursive: false));

            var programs = ExceptDisabledSource(toFilter.Distinct())
                .Select(x => GetProgramFromPath(x, protocols));
            return programs;
        }

        private static List<Win32> AppPathsPrograms(string[] suffixes, string[] protocols)
        {
            // https://msdn.microsoft.com/en-us/library/windows/desktop/ee872121
            const string appPaths = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";

            IEnumerable<string> toFilter = [];

            using var rootMachine = Registry.LocalMachine.OpenSubKey(appPaths);
            using var rootUser = Registry.CurrentUser.OpenSubKey(appPaths);

            if (rootMachine != null)
            {
                toFilter = toFilter.Concat(GetPathFromRegistry(rootMachine));
            }

            if (rootUser != null)
            {
                toFilter = toFilter.Concat(GetPathFromRegistry(rootUser));
            }

            toFilter = toFilter.Distinct().Where(p => suffixes.Contains(Extension(p)));

            var programs = ExceptDisabledSource(toFilter)
                .Select(x => GetProgramFromPath(x, protocols)).Where(x => x.Valid)
                .ToList(); // ToList due to disposing issue
            return programs;
        }

        private static IEnumerable<string> GetPathFromRegistry(RegistryKey root)
        {
            return root
                .GetSubKeyNames()
                .Select(x => GetProgramPathFromRegistrySubKeys(root, x))
                .Distinct();
        }

        private static string GetProgramPathFromRegistrySubKeys(RegistryKey root, string subKey)
        {
            var path = string.Empty;
            try
            {
                using (var key = root.OpenSubKey(subKey))
                {
                    if (key == null)
                        return string.Empty;

                    var defaultValue = string.Empty;
                    path = key.GetValue(defaultValue) as string;
                }

                if (string.IsNullOrEmpty(path))
                    return string.Empty;

                // fix path like this: ""\"C:\\folder\\executable.exe\""
                return path = path.Trim('"', ' ');
            }
            catch (Exception e) when (e is SecurityException || e is UnauthorizedAccessException)
            {
                Main.Context.Logger.LogError(e, $"Permission denied when trying to load the program from {path}");
                return string.Empty;
            }
        }

        private static Win32 GetProgramFromPath(string path, string[] protocols)
        {
            if (string.IsNullOrEmpty(path))
                return Default;

            path = Environment.ExpandEnvironmentVariables(path);

            return Extension(path) switch
            {
                ShortcutExtension => LnkProgram(path),
                ExeExtension => ExeProgram(path),
                UrlExtension => UrlProgram(path, protocols),
                _ => Win32Program(path)
            };
        }

        public static IEnumerable<string> ExceptDisabledSource(IEnumerable<string> paths)
        {
            return ExceptDisabledSource(paths, x => x.ToLowerInvariant());
        }

        public static IEnumerable<TSource> ExceptDisabledSource<TSource>(IEnumerable<TSource> sources,
            Func<TSource, string> keySelector)
        {
            return Main._settings.DisabledProgramSources.Count == 0
                ? sources
                : ExceptDisabledSourceEnumerable(sources, keySelector);

            static IEnumerable<TSource> ExceptDisabledSourceEnumerable(IEnumerable<TSource> elements,
                Func<TSource, string> selector)
            {
                var set = Main._settings.DisabledProgramSources.Select(x => x.UniqueIdentifier).ToHashSet();

                foreach (var element in elements)
                {
                    if (!set.Contains(selector(element)))
                        yield return element;
                }
            }
        }

        public static IEnumerable<T> DistinctBy<T, R>(IEnumerable<T> source, Func<T, R> selector)
        {
            var set = new HashSet<R>();
            foreach (var item in source)
            {
                if (set.Add(selector(item)))
                    yield return item;
            }
        }

        private static ParallelQuery<Win32> ProgramsHasher(IEnumerable<Win32> programs)
        {
            var startMenuPaths = GetStartMenuPaths();
            return programs.GroupBy(p => (p.ExecutablePath + p.Args).ToLowerInvariant())
                .AsParallel()
                .SelectMany(g =>
                {
                    // is shortcut and in start menu
                    var startMenu = g.Where(g =>
                            g.LnkResolvedPath != null &&
                            startMenuPaths.Any(x => PathContains(x, g.FullPath)))
                        .ToList();
                    if (startMenu.Count != 0)
                        return startMenu.Take(1);

                    // distinct by description
                    var temp = g.Where(g => !string.IsNullOrEmpty(g.Description)).ToList();
                    if (temp.Count != 0)
                        return temp.Take(1);
                    return g.Take(1);
                });
        }


        public static Win32[] All(Settings settings)
        {
            try
            {
                var programs = Enumerable.Empty<Win32>();
                var suffixes = settings.GetSuffixes();
                var protocols = settings.GetProtocols();

                // Disabled custom sources are not in DisabledProgramSources
                var sources = settings.ProgramSources.Where(s => Directory.Exists(s.Location) && s.Enabled).Distinct();
                var commonParents = GetCommonParents(sources);

                var unregistered = UnregisteredPrograms(commonParents, suffixes, protocols);

                programs = programs.Concat(unregistered);

                var autoIndexPrograms = Enumerable.Empty<Win32>(); // for single programs, not folders

                if (settings.EnableRegistrySource)
                {
                    var appPaths = AppPathsPrograms(suffixes, protocols);
                    autoIndexPrograms = autoIndexPrograms.Concat(appPaths);
                }

                if (settings.EnableStartMenuSource)
                {
                    var startMenu = StartMenuPrograms(suffixes, protocols);
                    autoIndexPrograms = autoIndexPrograms.Concat(startMenu);
                }

                if (settings.EnablePathSource)
                {
                    var path = PATHPrograms(settings.GetSuffixes(), protocols, commonParents);
                    programs = programs.Concat(path);
                }

                autoIndexPrograms = ProgramsHasher(autoIndexPrograms).ToArray();

                return [.. programs.Concat(autoIndexPrograms).Where(x => x.Valid).Distinct()];
            }
#if DEBUG //This is to make developer aware of any unhandled exception and add in handling.
            catch (Exception)
            {
                throw;
            }
#endif

#if !DEBUG //Only do a catch all in production.
            catch (Exception e)
            {
                Main.Context.Logger.LogError(e, $"An unexpected error occurred in the calling method All");
                return [];
            }
#endif
        }

        public override int GetHashCode()
        {
            return UniqueIdentifier.GetHashCode();
        }

        public bool Equals([AllowNull] Win32 other)
        {
            if (other == null)
                return false;

            return UniqueIdentifier == other.UniqueIdentifier;
        }

        public override bool Equals(object obj)
        {
            if (obj is Win32 other)
            {
                return UniqueIdentifier == other.UniqueIdentifier;
            }
            else
            {
                return false;
            }
        }

        private static string[] GetStartMenuPaths()
        {
            var userStartMenu = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
            var commonStartMenu = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);

            return [userStartMenu, commonStartMenu];
        }

        private static string[] GetStartupPaths()
        {
            var userStartup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            var commonStartup = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);

            return [userStartup, commonStartup];
        }

        public static void WatchProgramUpdate(Settings settings)
        {
            var paths = new List<string>();
            if (settings.EnableStartMenuSource)
                paths.AddRange(GetStartMenuPaths());

            var customSources = GetCommonParents(settings.ProgramSources);
            paths.AddRange(customSources);

            var fileExtensionToWatch = settings.GetSuffixes();
            foreach (var directory in from path in paths where Directory.Exists(path) select path)
            {
                WatchDirectory(directory, fileExtensionToWatch);
            }

            _ = Task.Run(MonitorDirectoryChangeAsync);
        }

        private static readonly Channel<byte> indexQueue = Channel.CreateBounded<byte>(1);

        public static async Task MonitorDirectoryChangeAsync()
        {
            var reader = indexQueue.Reader;
            while (await reader.WaitToReadAsync())
            {
                await Task.Delay(500);
                while (reader.TryRead(out _))
                {
                }

                await Main.IndexWin32ProgramsAsync(resetCache: true).ConfigureAwait(false);
            }
        }

        public static void WatchDirectory(string directory, string[] extensions)
        {
            if (!Directory.Exists(directory))
            {
                throw new ArgumentException("Path Not Exist");
            }

            var watcher = new FileSystemWatcher(directory);

            watcher.Created += static (_, _) => indexQueue.Writer.TryWrite(default);
            watcher.Deleted += static (_, _) => indexQueue.Writer.TryWrite(default);
            watcher.EnableRaisingEvents = true;
            watcher.IncludeSubdirectories = true;
            foreach (var extension in extensions)
            {
                watcher.Filters.Add($"*.{extension}");
            }

            Watchers.Add(watcher);
        }

        public static void Dispose()
        {
            foreach (var fileSystemWatcher in Watchers)
            {
                fileSystemWatcher.Dispose();
            }
        }

        private static List<string> GetCommonParents(IEnumerable<ProgramSource> programSources)
        {
            // To avoid unnecessary io
            // like c:\windows and c:\windows\system32
            var grouped = programSources.GroupBy(p => p.Location.ToLowerInvariant()[0]); // group by disk
            List<string> result = [];
            foreach (var group in grouped)
            {
                HashSet<ProgramSource> parents = [.. group];
                foreach (var source in group)
                {
                    if (parents.Any(p => PathContains(p.Location, source.Location)))
                    {
                        parents.Remove(source);
                    }
                }

                result.AddRange(parents.Select(x => x.Location));
            }

            return [.. result.DistinctBy(x => x.ToLowerInvariant())];
        }

        /// <summary>
        /// Returns if <paramref name="parentPath"/> contains <paramref name="subPath"/>. Equal paths are not considered to be contained by default.
        /// From https://stackoverflow.com/a/66877016
        /// </summary>
        private static bool PathContains(string parentPath, string subPath)
        {
            if (!parentPath.EndsWith('\\'))
                parentPath += '\\';

            var rel = Path.GetRelativePath(parentPath, subPath);
            return rel != "."
                   && rel != ".."
                   && !rel.StartsWith("../")
                   && !rel.StartsWith(@"..\")
                   && !Path.IsPathRooted(rel);
        }
    }
}
