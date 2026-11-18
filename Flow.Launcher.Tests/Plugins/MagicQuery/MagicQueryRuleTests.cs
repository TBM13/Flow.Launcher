using Flow.Launcher.Plugin.MagicQuery.Settings;
using MagicQuerySettings = Flow.Launcher.Plugin.MagicQuery.Settings.Settings;
using MatchType = Flow.Launcher.Plugin.MagicQuery.Settings.MatchType;

namespace Flow.Launcher.Tests.Plugins.MagicQuery;

public class MagicQueryRuleTests
{
    [Fact]
    public void Settings_DefaultRules_IsEmpty()
    {
        MagicQuerySettings settings = new();

        Assert.Empty(settings.Rules);
    }

    [Fact]
    public void TryMatch_DisabledRule_ReturnsNull()
    {
        MagicQueryRule rule = new()
        {
            Enabled = false,
            WindowTitlePattern = "Visual Studio Code",
            WindowTitleMatchType = MatchType.Partial,
            Query = ">code"
        };

        Assert.Null(rule.TryMatch("main.cs - Visual Studio Code", @"C:\Program Files\VS Code\Code.exe"));
    }

    [Fact]
    public void TryMatch_BothPatternsEmpty_ReturnsNull()
    {
        MagicQueryRule rule = new()
        {
            Enabled = true,
            WindowTitlePattern = string.Empty,
            ProcessPathPattern = string.Empty,
            Query = ">something"
        };

        Assert.Null(rule.TryMatch("Any Title", @"C:\app.exe"));
    }

    [Theory]
    [InlineData(MatchType.Exact, false, "Notepad", "notepad", ">np")]
    [InlineData(MatchType.Exact, false, "Notepad", "Notepad - File.txt", null)]
    [InlineData(MatchType.Exact, true, "Notepad", "Notepad", ">np")]
    [InlineData(MatchType.Exact, true, "Notepad", "notepad", null)]
    [InlineData(MatchType.Partial, false, "Visual Studio", "my app - visual studio code", ">vs")]
    [InlineData(MatchType.Partial, false, "Visual Studio", "Notepad", null)]
    [InlineData(MatchType.Partial, true, "Visual Studio", "my app - Visual Studio Code", ">vs")]
    [InlineData(MatchType.Partial, true, "Visual Studio", "my app - visual studio code", null)]
    [InlineData(MatchType.Regex, false, @"^doc-\d+\.txt$", "DOC-42.TXT", ">doc")]
    [InlineData(MatchType.Regex, false, @"^doc-\d+\.txt$", "doc-abc.txt", null)]
    [InlineData(MatchType.Regex, true, @"^DOC-\d+\.txt$", "DOC-42.txt", ">doc")]
    [InlineData(MatchType.Regex, true, @"^DOC-\d+\.txt$", "doc-42.txt", null)]
    public void TryMatch_WindowTitleOnly_MatchesExpected(
        MatchType matchType, bool caseSensitive, string pattern, string windowTitle, string? expected)
    {
        MagicQueryRule rule = new()
        {
            WindowTitlePattern = pattern,
            WindowTitleMatchType = matchType,
            WindowTitleCaseSensitive = caseSensitive,
            Query = expected ?? ">fallback"
        };

        string? result = rule.TryMatch(windowTitle, @"C:\Windows\notepad.exe");
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(MatchType.Exact, false, @"C:\Windows\notepad.exe", @"c:\windows\notepad.exe", ">np")]
    [InlineData(MatchType.Exact, false, @"C:\Windows\notepad.exe", @"D:\notepad.exe", null)]
    [InlineData(MatchType.Exact, true, @"C:\Windows\notepad.exe", @"C:\Windows\notepad.exe", ">np")]
    [InlineData(MatchType.Exact, true, @"C:\Windows\notepad.exe", @"c:\windows\notepad.exe", null)]
    [InlineData(MatchType.Partial, false, @"\Code.exe", @"C:\Users\dev\AppData\Local\Programs\Microsoft VS Code\code.exe", ">code")]
    [InlineData(MatchType.Partial, false, @"\Code.exe", @"C:\Windows\notepad.exe", null)]
    [InlineData(MatchType.Partial, true, @"\Code.exe", @"C:\Programs\Code.exe", ">code")]
    [InlineData(MatchType.Partial, true, @"\Code.exe", @"C:\Programs\code.exe", null)]
    [InlineData(MatchType.Regex, false, @"\\(chrome|msedge)\.exe$", @"C:\Program Files\Microsoft\Edge\Application\MSEDGE.EXE", ">web")]
    [InlineData(MatchType.Regex, false, @"\\(chrome|msedge)\.exe$", @"C:\Program Files\Mozilla Firefox\firefox.exe", null)]
    [InlineData(MatchType.Regex, true, @"\\Code\.exe$", @"C:\Programs\Code.exe", ">code")]
    [InlineData(MatchType.Regex, true, @"\\Code\.exe$", @"C:\Programs\code.exe", null)]
    public void TryMatch_ProcessPathOnly_MatchesExpected(
        MatchType matchType, bool caseSensitive, string pattern, string processPath, string? expected)
    {
        MagicQueryRule rule = new()
        {
            ProcessPathPattern = pattern,
            ProcessPathMatchType = matchType,
            ProcessPathCaseSensitive = caseSensitive,
            Query = expected ?? ">fallback"
        };

        string? result = rule.TryMatch("Any Window", processPath);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TryMatch_CombinedTitleAndProcessPath_RequiresBothToMatch()
    {
        MagicQueryRule rule = new()
        {
            WindowTitlePattern = "YouTube",
            WindowTitleMatchType = MatchType.Partial,
            WindowTitleCaseSensitive = false,
            ProcessPathPattern = @"\\firefox\.exe$",
            ProcessPathMatchType = MatchType.Regex,
            ProcessPathCaseSensitive = false,
            Query = "yt "
        };

        Assert.Equal("yt ", rule.TryMatch("Video - YouTube", @"C:\Program Files\Mozilla Firefox\firefox.exe"));
        Assert.Null(rule.TryMatch("Video - YouTube", @"C:\Program Files\Google\Chrome\Application\chrome.exe"));
        Assert.Null(rule.TryMatch("GitHub Dashboard", @"C:\Program Files\Mozilla Firefox\firefox.exe"));
    }

    [Fact]
    public void TryMatch_RegexSubstitutions_FromWindowTitle()
    {
        MagicQueryRule rule = new()
        {
            WindowTitlePattern = @"^(?<owner>[A-Za-z0-9_.-]+)/(?<repo>[A-Za-z0-9_.-]+) - Visual Studio Code$",
            WindowTitleMatchType = MatchType.Regex,
            Query = "owner=${owner} sub=$2 full=[$0] match=[$&] cost=$$10 missing=$99 ${unknown}"
        };

        string? result = rule.TryMatch("Flow-Launcher/Flow.Launcher - Visual Studio Code", @"C:\Code.exe");

        Assert.Equal(
            "owner=Flow-Launcher sub=Flow.Launcher full=[Flow-Launcher/Flow.Launcher - Visual Studio Code] match=[Flow-Launcher/Flow.Launcher - Visual Studio Code] cost=$10 missing=$99 ${unknown}",
            result);
    }

    [Fact]
    public void TryMatch_RegexSubstitutions_FromProcessPath()
    {
        MagicQueryRule rule = new()
        {
            ProcessPathPattern = @"^C:\\Tools\\(?<tool>[^\\]+)\\([^\\]+)\.exe$",
            ProcessPathMatchType = MatchType.Regex,
            Query = "run ${tool}/$1 ($2)"
        };

        string? result = rule.TryMatch("Window", @"C:\Tools\Git\bin.exe");

        Assert.Equal("run Git/bin (Git)", result);
    }

    [Fact]
    public void TryMatch_RegexSubstitutions_CombinedTitleAndProcessPath()
    {
        MagicQueryRule rule = new()
        {
            WindowTitlePattern = @"^(?<doc>.+)\s+-\s+Notepad$",
            WindowTitleMatchType = MatchType.Regex,
            ProcessPathPattern = @"\\(?<exe>[^\\]+)\.exe$",
            ProcessPathMatchType = MatchType.Regex,
            Query = "${exe}: ${doc} ($1)"
        };

        string? result = rule.TryMatch("notes.md - Notepad", @"C:\Windows\notepad.exe");

        Assert.Equal("notepad: notes.md (notes.md)", result);
    }

    [Fact]
    public void TryMatch_RegexSubstitutions_UnmatchedOptionalGroup_ReplacedWithEmpty()
    {
        MagicQueryRule rule = new()
        {
            WindowTitlePattern = @"^(foo)(?:-(bar))?$",
            WindowTitleMatchType = MatchType.Regex,
            Query = "$1|$2|"
        };

        Assert.Equal("foo||", rule.TryMatch("foo", null));
        Assert.Equal("foo|bar|", rule.TryMatch("foo-bar", null));
    }

    [Theory]
    [InlineData(MatchType.Exact)]
    [InlineData(MatchType.Partial)]
    public void TryMatch_NonRegexMatch_DoesNotExpandDollarTokens(MatchType matchType)
    {
        MagicQueryRule rule = new()
        {
            WindowTitlePattern = "Terminal",
            WindowTitleMatchType = matchType,
            Query = "echo $1 $$ ${HOME}"
        };

        Assert.Equal("echo $1 $$ ${HOME}", rule.TryMatch("Terminal", null));
    }

    [Fact]
    public void TryMatch_InvalidRegexPattern_ReturnsNullWithoutThrowing()
    {
        MagicQueryRule titleInvalid = new()
        {
            WindowTitlePattern = "[unclosed",
            WindowTitleMatchType = MatchType.Regex,
            Query = "q"
        };

        MagicQueryRule pathInvalid = new()
        {
            ProcessPathPattern = "(unclosed",
            ProcessPathMatchType = MatchType.Regex,
            Query = "q"
        };

        Assert.Null(titleInvalid.TryMatch("[unclosed", @"C:\app.exe"));
        Assert.Null(pathInvalid.TryMatch("Title", "(unclosed"));
    }

    [Fact]
    public void TryMatch_NullInputs_HandledPerConfiguredPatterns()
    {
        MagicQueryRule titleOnly = new()
        {
            WindowTitlePattern = "Code",
            WindowTitleMatchType = MatchType.Partial,
            Query = "title-only"
        };

        MagicQueryRule pathOnly = new()
        {
            ProcessPathPattern = "Code.exe",
            ProcessPathMatchType = MatchType.Partial,
            Query = "path-only"
        };

        Assert.Null(titleOnly.TryMatch(null, @"C:\Code.exe"));
        Assert.Equal("title-only", titleOnly.TryMatch("VS Code", null));

        Assert.Null(pathOnly.TryMatch("VS Code", null));
        Assert.Equal("path-only", pathOnly.TryMatch(null, @"C:\Code.exe"));
    }
}
