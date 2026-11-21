using System.IO;
using System.Security.Principal;
using System.Xml.Linq;
using Flow.Launcher.Plugin.Program.Settings;
using Flow.Launcher.PluginSDK.Plugins;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Management.Deployment;

namespace Flow.Launcher.Plugin.Program.Programs;

internal static class UwpIndexer
{
    private const string Win10ManifestNamespace = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
    private const string Win81ManifestNamespace = "http://schemas.microsoft.com/appx/2013/manifest";
    private const string Win8ManifestNamespace = "http://schemas.microsoft.com/appx/2010/manifest";
    private const string RestrictedCapabilitiesNamespace = "http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities";

    private readonly record struct ManifestLogoSpec(
        string SmallAttr,
        int SmallBaseSize,
        string BigAttr,
        int BigBaseSize);

    internal static bool IsSupported => Environment.OSVersion.Version.Major >= 10;

    internal static UwpProgram[] IndexAll(Settings.Settings settings, PluginInitContext? context = null)
    {
        if (!settings.EnableUWP || !IsSupported)
            return [];

        string? userSid = WindowsIdentity.GetCurrent().User?.Value;
        if (string.IsNullOrEmpty(userSid))
            return [];

        PackageManager packageManager = CreatePackageManager();
        IEnumerable<Package> packages = packageManager.FindPackagesForUser(userSid)
            .Where(p => IsValidPackage(p, context));

        return
        [
            .. packages
                .AsParallel()
                .SelectMany(p => ExtractProgramsFromPackage(p, context))
                .DistinctBy(p => p.UniqueIdentifier, StringComparer.OrdinalIgnoreCase)
        ];
    }

    private static PackageManager CreatePackageManager()
    {
        try
        {
            return new PackageManager();
        }
        catch
        {
            // CsWinRT workaround: first instantiation can fail transiently on some SDK versions.
            return new PackageManager();
        }
    }

    private static bool IsValidPackage(Package package, PluginInitContext? context)
    {
        try
        {
            return !package.IsFramework
                && !package.IsDevelopmentMode
                && !string.IsNullOrEmpty(package.InstalledLocation?.Path);
        }
        catch (Exception ex)
        {
            context?.Logger.LogDebug($"Failed to verify package '{package.Id?.FullName}': {ex.Message}");
            return false;
        }
    }

    private static IEnumerable<UwpProgram> ExtractProgramsFromPackage(Package package, PluginInitContext? context)
    {
        IReadOnlyList<AppListEntry> appEntries;
        string location;
        try
        {
            location = package.InstalledLocation.Path;
            appEntries = package.GetAppListEntries();
        }
        catch (Exception ex)
        {
            context?.Logger.LogDebug($"Failed to enumerate entries for package '{package.Id?.FullName}': {ex.Message}");
            return [];
        }

        if (appEntries.Count == 0)
            return [];

        XDocument? manifest = LoadManifest(location, context);
        XElement? root = manifest?.Root;
        ManifestLogoSpec? logoSpec = root is not null ? GetLogoSpec(root.Name.NamespaceName) : null;

        bool packageCanElevate = root is not null && root.Descendants().Any(e =>
            e.Name.LocalName == "Capability"
            && e.Name.NamespaceName == RestrictedCapabilitiesNamespace
            && string.Equals(e.Attribute("Name")?.Value, "allowElevation", StringComparison.OrdinalIgnoreCase));

        XElement? applicationsElement = root?.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "Applications");

        List<UwpProgram> result = new(appEntries.Count);
        foreach (AppListEntry entry in appEntries)
        {
            try
            {
                string userModelId = entry.AppUserModelId;
                if (string.IsNullOrWhiteSpace(userModelId))
                    continue;

                string appId = ExtractAppId(userModelId);
                XElement? appElement = !string.IsNullOrEmpty(appId)
                    ? applicationsElement?.Elements().FirstOrDefault(e =>
                        e.Name.LocalName == "Application"
                        && string.Equals(e.Attribute("Id")?.Value, appId, StringComparison.OrdinalIgnoreCase))
                    : null;

                bool canElevate = packageCanElevate || CanAppRunElevated(appElement);

                (string logoPath, string previewPath) = ResolveAppLogos(
                    location,
                    appElement,
                    logoSpec);

                result.Add(new UwpProgram
                {
                    UserModelId = userModelId,
                    UniqueIdentifier = ProgramSource.NormalizeId(userModelId),
                    DisplayName = entry.DisplayInfo.DisplayName ?? string.Empty,
                    Description = entry.DisplayInfo.Description ?? string.Empty,
                    Location = location,
                    CanRunElevated = canElevate,
                    LogoPath = logoPath,
                    PreviewImagePath = previewPath
                });
            }
            catch (Exception ex)
            {
                context?.Logger.LogDebug($"Failed to construct UWP app from '{package.Id?.FullName}': {ex.Message}");
            }
        }

        return result;
    }

    private static XDocument? LoadManifest(string packageLocation, PluginInitContext? context)
    {
        string manifestPath = Path.Combine(packageLocation, "AppxManifest.xml");
        if (!File.Exists(manifestPath))
            return null;

        try
        {
            return XDocument.Load(manifestPath);
        }
        catch (Exception ex)
        {
            context?.Logger.LogDebug($"Failed to parse AppxManifest.xml in '{packageLocation}': {ex.Message}");
            return null;
        }
    }

    private static ManifestLogoSpec? GetLogoSpec(string rootNamespace) => rootNamespace switch
    {
        Win10ManifestNamespace => new("Square44x44Logo", 44, "Square150x150Logo", 150),
        Win81ManifestNamespace => new("Square30x30Logo", 30, "Square150x150Logo", 150),
        Win8ManifestNamespace => new("SmallLogo", 30, "Logo", 150),
        _ => null
    };

    private static string ExtractAppId(string userModelId)
    {
        int bangIndex = userModelId.IndexOf('!');
        return bangIndex >= 0 && bangIndex + 1 < userModelId.Length
            ? userModelId[(bangIndex + 1)..]
            : string.Empty;
    }

    private static bool CanAppRunElevated(XElement? appElement)
    {
        if (appElement is null)
            return false;

        if (string.Equals(appElement.Attribute("EntryPoint")?.Value, "Windows.FullTrustApplication", StringComparison.Ordinal))
            return true;

        return appElement.Attributes().Any(a =>
            a.Name.LocalName == "TrustLevel"
            && string.Equals(a.Value, "mediumIL", StringComparison.OrdinalIgnoreCase));
    }

    private static (string LogoPath, string PreviewImagePath) ResolveAppLogos(
        string location,
        XElement? appElement,
        ManifestLogoSpec? spec)
    {
        if (appElement is null || spec is not { } logoSpec)
            return (string.Empty, string.Empty);

        XElement? visualElements = appElement.Elements()
            .FirstOrDefault(e => e.Name.LocalName.EndsWith("VisualElements", StringComparison.Ordinal));
        if (visualElements is null)
            return (string.Empty, string.Empty);

        string? smallUri = visualElements.Attribute(logoSpec.SmallAttr)?.Value;
        string? bigUri = visualElements.Attribute(logoSpec.BigAttr)?.Value;

        if (!string.IsNullOrWhiteSpace(smallUri))
        {
            IReadOnlyList<string> candidates = FindLogoCandidates(location, smallUri);
            if (candidates.Count > 0)
            {
                return (
                    SelectBestLogoFile(candidates, logoSpec.SmallBaseSize, 64),
                    SelectBestLogoFile(candidates, logoSpec.SmallBaseSize, 256));
            }
        }

        if (!string.IsNullOrWhiteSpace(bigUri))
        {
            IReadOnlyList<string> candidates = FindLogoCandidates(location, bigUri);
            if (candidates.Count > 0)
            {
                return (
                    SelectBestLogoFile(candidates, logoSpec.BigBaseSize, 64),
                    SelectBestLogoFile(candidates, logoSpec.BigBaseSize, 256));
            }
        }

        return (string.Empty, string.Empty);
    }

    private static IReadOnlyList<string> FindLogoCandidates(string location, string relativeUri)
    {
        string normalizedUri = relativeUri.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        string directPath = Path.Combine(location, normalizedUri);

        IReadOnlyList<string> candidates = EnumerateLogoVariants(directPath);
        if (candidates.Count > 0)
            return candidates;

        string assetsFallback = Path.Combine(location, "Assets", normalizedUri);
        if (!assetsFallback.Equals(directPath, StringComparison.OrdinalIgnoreCase))
            return EnumerateLogoVariants(assetsFallback);

        return [];
    }

    private static IReadOnlyList<string> EnumerateLogoVariants(string baseLogoPath)
    {
        string? dir = Path.GetDirectoryName(baseLogoPath);
        string namePrefix = Path.GetFileNameWithoutExtension(baseLogoPath);
        string ext = Path.GetExtension(baseLogoPath);

        if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(namePrefix) || string.IsNullOrEmpty(ext) || !Directory.Exists(dir))
            return [];

        List<string> matches = [];
        try
        {
            foreach (string file in Directory.EnumerateFiles(dir, $"{namePrefix}*{ext}"))
            {
                string candidateName = Path.GetFileNameWithoutExtension(file);
                if (candidateName.Equals(namePrefix, StringComparison.OrdinalIgnoreCase)
                    || (candidateName.Length > namePrefix.Length
                        && candidateName.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase)
                        && (candidateName[namePrefix.Length] == '.' || candidateName[namePrefix.Length] == '_')))
                {
                    matches.Add(file);
                }
            }
        }
        catch
        {
            return [];
        }

        if (matches.Count == 0 && File.Exists(baseLogoPath))
            matches.Add(baseLogoPath);

        return matches;
    }

    internal static string SelectBestLogoFile(IReadOnlyList<string> files, int baseSize, int targetPixelSize)
    {
        if (files.Count == 0)
            return string.Empty;

        if (files.Count == 1)
            return files[0];

        bool hasNonContrast = files.Any(f => !IsContrastVariant(f));

        string bestFile = string.Empty;
        int bestSizeDiff = int.MaxValue;
        int bestUnplatedRank = -1;
        int bestPixelSize = -1;

        foreach (string file in files)
        {
            if (hasNonContrast && IsContrastVariant(file))
                continue;

            string fileName = Path.GetFileNameWithoutExtension(file);
            int pixelSize = EstimatePixelSize(fileName, baseSize);
            int sizeDiff = Math.Abs(pixelSize - targetPixelSize);
            int unplatedRank = GetUnplatedRank(fileName);

            bool isBetter = bestFile.Length == 0
                || sizeDiff < bestSizeDiff
                || (sizeDiff == bestSizeDiff && unplatedRank > bestUnplatedRank)
                || (sizeDiff == bestSizeDiff && unplatedRank == bestUnplatedRank && pixelSize > bestPixelSize)
                || (sizeDiff == bestSizeDiff && unplatedRank == bestUnplatedRank && pixelSize == bestPixelSize && file.Length < bestFile.Length);

            if (isBetter)
            {
                bestFile = file;
                bestSizeDiff = sizeDiff;
                bestUnplatedRank = unplatedRank;
                bestPixelSize = pixelSize;
            }
        }

        return bestFile;
    }

    private static bool IsContrastVariant(string path)
    {
        string fileName = Path.GetFileNameWithoutExtension(path);
        return fileName.Contains("contrast-black", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains("contrast-white", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetUnplatedRank(string fileName)
    {
        if (fileName.Contains("altform-unplated", StringComparison.OrdinalIgnoreCase))
            return 2;

        if (fileName.Contains("altform-lightunplated", StringComparison.OrdinalIgnoreCase))
            return 1;

        return 0;
    }

    private static int EstimatePixelSize(string fileName, int baseSize)
    {
        if (TryParseQualifierNumber(fileName, "targetsize-", out int targetSize) && targetSize > 0)
            return targetSize;

        if (TryParseQualifierNumber(fileName, "scale-", out int scale) && scale > 0)
            return (int)Math.Round(baseSize * (scale / 100.0), MidpointRounding.AwayFromZero);

        return baseSize;
    }

    private static bool TryParseQualifierNumber(string fileName, string prefix, out int value)
    {
        value = 0;
        int index = fileName.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return false;

        int start = index + prefix.Length;
        int end = start;
        while (end < fileName.Length && char.IsAsciiDigit(fileName[end]))
            end++;

        return end > start && int.TryParse(fileName.AsSpan(start, end - start), out value);
    }
}
