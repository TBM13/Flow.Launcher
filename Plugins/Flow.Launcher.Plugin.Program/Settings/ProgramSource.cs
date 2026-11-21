using System.IO;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Flow.Launcher.Plugin.Program.Settings;

/// <summary>
/// Represents a custom program directory or a hidden program entry.
/// </summary>
public sealed class ProgramSource : ObservableObject, IEquatable<ProgramSource>
{
    private string _name;
    private string _location;
    private bool _enabled;
    private string _uniqueIdentifier;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value ?? string.Empty);
    }

    public string Location
    {
        get => _location;
        set
        {
            string normalized = value ?? string.Empty;
            if (SetProperty(ref _location, normalized))
            {
                UniqueIdentifier = normalized;
            }
        }
    }

    public bool Enabled
    {
        get => _enabled;
        set => SetProperty(ref _enabled, value);
    }

    public string UniqueIdentifier
    {
        get => _uniqueIdentifier;
        set => SetProperty(ref _uniqueIdentifier, NormalizeId(value));
    }

    [JsonConstructor]
    public ProgramSource(string? name, string? location, bool enabled, string? uniqueIdentifier)
    {
        _name = name ?? string.Empty;
        _location = location ?? string.Empty;
        _enabled = enabled;
        _uniqueIdentifier = NormalizeId(string.IsNullOrEmpty(uniqueIdentifier) ? _location : uniqueIdentifier);
    }

    public ProgramSource(string location, bool enabled = true)
    {
        string trimmed = string.IsNullOrWhiteSpace(location)
            ? string.Empty
            : Path.TrimEndingDirectorySeparator(location.Trim());

        _location = trimmed;
        _enabled = enabled;
        _uniqueIdentifier = NormalizeId(trimmed);
        _name = GetDirectoryDisplayName(trimmed);
    }

    public ProgramSource(string name, string location, string uniqueIdentifier)
    {
        _name = name ?? string.Empty;
        _location = location ?? string.Empty;
        _enabled = false;
        _uniqueIdentifier = NormalizeId(uniqueIdentifier);
    }

    public static string NormalizeId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return Path.TrimEndingDirectorySeparator(value.Trim()).ToLowerInvariant();
    }

    private static string GetDirectoryDisplayName(string location)
    {
        if (string.IsNullOrEmpty(location))
            return string.Empty;

        try
        {
            string dirName = new DirectoryInfo(location).Name;
            return string.IsNullOrEmpty(dirName) ? location : dirName;
        }
        catch (Exception)
        {
            return location;
        }
    }

    public bool Equals(ProgramSource? other) =>
        other is not null && string.Equals(UniqueIdentifier, other.UniqueIdentifier, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => Equals(obj as ProgramSource);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(UniqueIdentifier);
}
