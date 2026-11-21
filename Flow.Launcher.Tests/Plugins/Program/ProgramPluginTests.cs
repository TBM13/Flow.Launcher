using Flow.Launcher.Plugin.Program.Programs;
using MemoryPack;
using ProgramSettings = Flow.Launcher.Plugin.Program.Settings.Settings;

namespace Flow.Launcher.Tests.Plugins.Program;

public class ProgramPluginTests
{
    [Fact]
    public void ParseCustomSuffixes_NormalizesDotsWhitespaceDuplicatesAndBuiltins()
    {
        string[] parsed = ProgramSettings.ParseCustomSuffixes(" .bat ; cmd, .PS1 ; exe ; .LNK ; BAT ; ; url ; appref-ms ");

        Assert.Equal(["bat", "cmd", "PS1"], parsed);
    }

    [Fact]
    public void ParseCustomSuffixes_EmptyOrWhitespace_ReturnsEmpty()
    {
        Assert.Empty(ProgramSettings.ParseCustomSuffixes(null));
        Assert.Empty(ProgramSettings.ParseCustomSuffixes("   "));
        Assert.Empty(ProgramSettings.ParseCustomSuffixes(".;exe;lnk"));
    }

    [Fact]
    public void GetAllExtensions_AlwaysIncludesStandardAndCustomExtensions()
    {
        ProgramSettings settings = new()
        {
            CustomSuffixes = [".bat", "cmd", " "]
        };

        HashSet<string> extensions = settings.GetAllExtensions();

        Assert.Contains("exe", extensions);
        Assert.Contains("lnk", extensions);
        Assert.Contains("appref-ms", extensions);
        Assert.Contains("url", extensions);
        Assert.Contains("bat", extensions);
        Assert.Contains("cmd", extensions);
    }

    [Theory]
    [InlineData(@"C:\Program Files\App\unins000.exe", null, "unins000", "", true)]
    [InlineData(@"C:\Program Files\App\unins001.exe", null, "unins001", "", true)]
    [InlineData(@"C:\Program Files\App\uninst.exe", null, "uninst", "", true)]
    [InlineData(@"C:\Program Files\App\uninstall.exe", null, "uninstall", "", true)]
    [InlineData(@"C:\StartMenu\Uninstall MyApp.lnk", @"C:\Windows\System32\msiexec.exe", "Uninstall MyApp", "", true)]
    [InlineData(@"C:\StartMenu\Remove App.lnk", @"C:\Program Files\App\unins000.exe", "Remove App", "", true)]
    [InlineData(@"C:\StartMenu\Désinstaller App.lnk", @"C:\Program Files\App\helper.exe", "Désinstaller App", "", true)]
    [InlineData(@"C:\StartMenu\Desinstalar App.lnk", null, "Desinstalar App", "", true)]
    [InlineData(@"C:\StartMenu\Deinstallieren App.lnk", null, "Deinstallieren App", "", true)]
    [InlineData(@"C:\StartMenu\卸载应用.lnk", null, "卸载应用", "", true)]
    [InlineData(@"C:\StartMenu\App.lnk", @"C:\Program Files\App\app.exe", "App", "Uninstall App", true)]
    [InlineData(@"C:\Program Files\Unity\Unity Hub.exe", null, "Unity Hub", "", false)]
    [InlineData(@"C:\Program Files\Epic\UnrealEditor.exe", null, "UnrealEditor", "", false)]
    [InlineData(@"C:\StartMenu\Visual Studio Code.lnk", @"C:\Programs\VS Code\Code.exe", "Visual Studio Code", "", false)]
    public void CheckIsUninstaller_DetectsUninstallersAccurately(
        string fullPath,
        string? resolvedPath,
        string name,
        string localizedName,
        bool expected)
    {
        bool actual = Win32Program.CheckIsUninstaller(fullPath, resolvedPath, name, localizedName);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("steam://rungameid/730", true)]
    [InlineData("steam://run/570", true)]
    [InlineData("com.epicgames.launcher://apps/Fortnite?action=launch", true)]
    [InlineData("roblox-player://launch", true)]
    [InlineData("  custom-app://open  ", true)]
    [InlineData("http://example.com", false)]
    [InlineData("https://example.com", false)]
    [InlineData("HTTPS://EXAMPLE.COM/app", false)]
    [InlineData("://missing-scheme", false)]
    [InlineData(@"C:\Program Files\App\app.exe", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsNonWebAppUrl_FiltersWebUrlsAndAcceptsAppProtocols(string? url, bool expected)
    {
        Assert.Equal(expected, Win32Indexer.IsNonWebAppUrl(url));
    }

    [Theory]
    [InlineData(@"C:\Apps", @"C:\Apps\Tool", true)]
    [InlineData(@"C:\Apps\", @"c:\apps\tool\sub", true)]
    [InlineData(@"C:\Apps", @"C:\Apps", false)]
    [InlineData(@"C:\App", @"C:\AppData", false)]
    [InlineData(@"C:\App", @"C:\AppData\Sub", false)]
    [InlineData(@"C:\Apps", @"D:\Apps\Tool", false)]
    [InlineData("", @"C:\Apps", false)]
    public void PathContains_ChecksStrictSubdirectoryContainment(string parent, string sub, bool expected)
    {
        Assert.Equal(expected, Win32Indexer.PathContains(parent, sub));
    }

    [Fact]
    public void GetCommonParents_CollapsesNestedDirectoriesAndPreservesSiblings()
    {
        List<string> parents = Win32Indexer.GetCommonParents(
        [
            @"C:\Apps\Tools\Sub",
            @"c:\apps\",
            @"C:\Apps",
            @"C:\AppData\Roaming",
            @"C:\App"
        ]);

        Assert.Equal(3, parents.Count);
        Assert.Contains(@"C:\App", parents, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(@"C:\Apps", parents, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(@"C:\AppData\Roaming", parents, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void SelectBestLogoFile_ExcludesContrastAndPrefersUnplatedAndClosestSize()
    {
        string[] files =
        [
            @"C:\Pkg\Assets\AppList.targetsize-64_contrast-black.png",
            @"C:\Pkg\Assets\AppList.targetsize-64_contrast-white.png",
            @"C:\Pkg\Assets\AppList.targetsize-48_altform-unplated.png",
            @"C:\Pkg\Assets\AppList.targetsize-64.png",
            @"C:\Pkg\Assets\AppList.targetsize-64_altform-lightunplated.png",
            @"C:\Pkg\Assets\AppList.targetsize-64_altform-unplated.png",
            @"C:\Pkg\Assets\AppList.targetsize-256_altform-unplated.png"
        ];

        string icon = UwpIndexer.SelectBestLogoFile(files, baseSize: 44, targetPixelSize: 64);
        string preview = UwpIndexer.SelectBestLogoFile(files, baseSize: 44, targetPixelSize: 256);

        Assert.Equal(@"C:\Pkg\Assets\AppList.targetsize-64_altform-unplated.png", icon);
        Assert.Equal(@"C:\Pkg\Assets\AppList.targetsize-256_altform-unplated.png", preview);
    }

    [Fact]
    public void SelectBestLogoFile_ScaleQualifiers_SelectsClosestToTargetPixelSize()
    {
        string[] files =
        [
            @"C:\Pkg\Assets\Square44x44Logo.scale-100.png",
            @"C:\Pkg\Assets\Square44x44Logo.scale-150.png",
            @"C:\Pkg\Assets\Square44x44Logo.scale-200.png",
            @"C:\Pkg\Assets\Square44x44Logo.scale-400.png"
        ];

        // baseSize 44: scale-150 = 66px (closest to 64), scale-400 = 176px (closest to 256)
        Assert.Equal(@"C:\Pkg\Assets\Square44x44Logo.scale-150.png", UwpIndexer.SelectBestLogoFile(files, 44, 64));
        Assert.Equal(@"C:\Pkg\Assets\Square44x44Logo.scale-400.png", UwpIndexer.SelectBestLogoFile(files, 44, 256));
    }

    [Fact]
    public void ProgramCaches_MemoryPackRoundTrip_PreservesEntries()
    {
        Win32ProgramCache win32Cache = new()
        {
            Programs =
            [
                new Win32Program
                {
                    Name = "Code",
                    LocalizedName = "Visual Studio Code",
                    UniqueIdentifier = @"c:\startmenu\vscode.lnk",
                    FullPath = @"C:\StartMenu\vscode.lnk",
                    LnkResolvedPath = @"C:\Apps\VS Code\Code.exe",
                    Args = "--new-window",
                    ParentDirectory = @"C:\StartMenu",
                    ExecutableName = "Code",
                    Description = "Code Editing. Redefined.",
                    IcoPath = @"C:\StartMenu\vscode.lnk",
                    ProgramType = Win32ProgramType.Shortcut,
                    IsUninstaller = false
                }
            ]
        };

        UwpProgramCache uwpCache = new()
        {
            Programs =
            [
                new UwpProgram
                {
                    DisplayName = "Calculator",
                    Description = "Windows Calculator",
                    UserModelId = "Microsoft.WindowsCalculator_8wekyb3d8bbwe!App",
                    UniqueIdentifier = "microsoft.windowscalculator_8wekyb3d8bbwe!app",
                    Location = @"C:\Program Files\WindowsApps\Microsoft.WindowsCalculator",
                    CanRunElevated = false,
                    LogoPath = @"C:\Logo\calc64.png",
                    PreviewImagePath = @"C:\Logo\calc256.png"
                }
            ]
        };

        byte[] win32Bytes = MemoryPackSerializer.Serialize(win32Cache);
        Win32ProgramCache? deserializedWin32 = MemoryPackSerializer.Deserialize<Win32ProgramCache>(win32Bytes);

        byte[] uwpBytes = MemoryPackSerializer.Serialize(uwpCache);
        UwpProgramCache? deserializedUwp = MemoryPackSerializer.Deserialize<UwpProgramCache>(uwpBytes);

        Assert.NotNull(deserializedWin32);
        Assert.Single(deserializedWin32.Programs);
        Assert.Equal("Visual Studio Code", deserializedWin32.Programs[0].LocalizedName);
        Assert.Equal("--new-window", deserializedWin32.Programs[0].Args);
        Assert.Equal(Win32ProgramType.Shortcut, deserializedWin32.Programs[0].ProgramType);

        Assert.NotNull(deserializedUwp);
        Assert.Single(deserializedUwp.Programs);
        Assert.Equal("Calculator", deserializedUwp.Programs[0].DisplayName);
        Assert.Equal(@"C:\Logo\calc256.png", deserializedUwp.Programs[0].PreviewImagePath);
    }

    [Fact]
    public void DeduplicatePrograms_MergesSameExecutableAndArgs_AndPreservesDifferentArgs()
    {
        Win32Program shortcut = new()
        {
            Name = "Discord",
            UniqueIdentifier = @"c:\startmenu\discord.lnk",
            FullPath = @"C:\StartMenu\Discord.lnk",
            LnkResolvedPath = @"C:\Apps\Discord\Discord.exe",
            Args = "--start-minimized",
            ProgramType = Win32ProgramType.Shortcut
        };

        Win32Program duplicateShortcut = new()
        {
            Name = "Discord (Copy)",
            UniqueIdentifier = @"c:\startmenu\programs\discord.lnk",
            FullPath = @"C:\StartMenu\Programs\Discord.lnk",
            LnkResolvedPath = @"c:\apps\discord\discord.exe",
            Args = "--start-minimized",
            ProgramType = Win32ProgramType.Shortcut
        };

        Win32Program customDirShortcut = new()
        {
            Name = "Discord Custom",
            UniqueIdentifier = @"d:\custom\discord.lnk",
            FullPath = @"D:\Custom\Discord.lnk",
            LnkResolvedPath = @"C:\Apps\Discord\Discord.exe",
            Args = "--start-minimized",
            ProgramType = Win32ProgramType.Shortcut
        };

        Win32Program differentArgsShortcut = new()
        {
            Name = "Discord Canary Profile",
            UniqueIdentifier = @"c:\startmenu\discord-profile.lnk",
            FullPath = @"C:\StartMenu\DiscordProfile.lnk",
            LnkResolvedPath = @"C:\Apps\Discord\Discord.exe",
            Args = "--profile work",
            ProgramType = Win32ProgramType.Shortcut
        };

        Win32Program[] deduplicated = Win32Indexer.DeduplicatePrograms(
        [
            (shortcut, Win32Indexer.SourceKind.StartMenu),
            (duplicateShortcut, Win32Indexer.SourceKind.StartMenu),
            (customDirShortcut, Win32Indexer.SourceKind.CustomDirectory),
            (differentArgsShortcut, Win32Indexer.SourceKind.StartMenu)
        ]);

        Assert.Equal(2, deduplicated.Length);
        Assert.Contains(deduplicated, p => p.UniqueIdentifier == customDirShortcut.UniqueIdentifier);
        Assert.Contains(deduplicated, p => p.UniqueIdentifier == differentArgsShortcut.UniqueIdentifier);
    }
}
