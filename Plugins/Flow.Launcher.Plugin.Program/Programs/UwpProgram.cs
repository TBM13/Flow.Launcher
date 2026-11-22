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

[MemoryPackable]
public sealed partial class UwpProgram : IProgram, IEquatable<UwpProgram>
{
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string UserModelId { get; init; } = string.Empty;
    public string UniqueIdentifier { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public bool CanRunElevated { get; init; }
    public string LogoPath { get; init; } = string.Empty;
    public string PreviewImagePath { get; init; } = string.Empty;

    [MemoryPackIgnore]
    public string Name => DisplayName;

    public UwpProgram()
    {
    }

    public Result? CreateResult(ReadOnlySpan<char> query, Settings.Settings settings, PluginInitContext context)
    {
        bool showDescription = settings.EnableDescription
            && !string.IsNullOrWhiteSpace(Description)
            && !DisplayName.Equals(Description, StringComparison.OrdinalIgnoreCase);

        int score = 0;
        if (!query.IsEmpty)
        {
            IStringMatcher matcher = context.API.StringMatcher;
            MatchResult match = matcher.FuzzySearch(query, DisplayName);

            if (showDescription)
            {
                MatchResult descMatch = matcher.FuzzySearch(query, Description);
                if (descMatch.Score > match.Score || (!match.IsThresholdMet && descMatch.IsThresholdMet))
                    match = descMatch;
            }

            if (!match.IsThresholdMet || match.Score <= 0)
                return null;

            score = match.Score;
        }

        string title = showDescription ? $"{DisplayName}: {Description}" : DisplayName;

        string subtitle = settings.HideAppsPath ? string.Empty : Location;
        string autocomplete = string.IsNullOrEmpty(Location)
            ? string.Empty
            : Path.EndsInDirectorySeparator(Location)
                ? Location
                : Location + Path.DirectorySeparatorChar;

        return new Result
        {
            Title = title,
            SubTitle = subtitle,
            AutocompleteText = (false, autocomplete),
            CopyText = Location,
            RecordKey = UniqueIdentifier,
            IconOrGlyph = string.IsNullOrEmpty(LogoPath) ? null : LogoPath,
            Preview = new Result.PreviewInfo
            {
                PreviewImagePath = string.IsNullOrEmpty(PreviewImagePath) ? null : PreviewImagePath,
                Description = Description
            },
            Score = score,
            ContextData = this,
            ToolTip = string.IsNullOrEmpty(subtitle) ? title : $"{title}\n\n{subtitle}",
            Action = c =>
            {
                if (c.PressedKeys.OnlyModifiersPressed(ModifierKeys.Control))
                {
                    if (!string.IsNullOrEmpty(Location))
                        FileExplorerHelper.OpenFolderAndSelectFile(Location);

                    return true;
                }

                bool elevated = c.PressedKeys.OnlyModifiersPressed(ModifierKeys.Control | ModifierKeys.Shift);
                if (elevated && !CanRunElevated)
                {
                    context.API.ShowMsgError("Error", "This app is not intended to be run as administrator");
                    return true;
                }

                Launch(context, elevated);
                return true;
            }
        };
    }

    public IEnumerable<Result> CreateContextMenus(PluginInitContext context)
    {
        if (!string.IsNullOrEmpty(Location))
        {
            yield return new Result
            {
                Title = "Open containing folder",
                IconOrGlyph = "\xe838",
                Action = _ =>
                {
                    FileExplorerHelper.OpenFolderAndSelectFile(Location);
                    return true;
                }
            };
        }

        if (CanRunElevated)
        {
            yield return new Result
            {
                Title = "Run as administrator",
                IconOrGlyph = "\xe7ef",
                Action = _ =>
                {
                    Launch(context, elevated: true);
                    return true;
                }
            };
        }
    }

    private void Launch(PluginInitContext context, bool elevated)
    {
        string command = $"shell:AppsFolder\\{UserModelId}";
        _ = Task.Run(() =>
        {
            try
            {
                ProcessHelper.StartProcess(
                    command,
                    useShellExecute: true,
                    verb: elevated ? "runas" : string.Empty);
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                // User cancelled the UAC prompt
            }
            catch (Exception ex)
            {
                context.Logger.LogError(ex, $"Unable to run UWP app '{command}'");
                context.API.ShowMsgError("Error", $"Unable to run {command}: {ex.Message}");
            }
        });
    }

    public bool Equals(UwpProgram? other) =>
        other is not null && string.Equals(UniqueIdentifier, other.UniqueIdentifier, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => Equals(obj as UwpProgram);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(UniqueIdentifier);

    public override string ToString() => DisplayName;
}
