/*The MIT License

Copyright (c) Microsoft Corporation. All rights reserved.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */

namespace Flow.Launcher.Plugin.WindowsSettings.Classes;

internal enum WindowsSettingType
{
    AppControlPanel,
    AppSettingsApp,
    AppMMC
}

internal record WindowsSetting
{
    /// <summary>
    /// The symbol which is used as delimiter between the parts of the path.
    /// </summary>
    private const string PATH_DELIMITER_SEQUENCE = "\u0020\u0020\u02C3\u0020\u0020"; // "<space><space><arrow><space><space>"

    public required string Name { get; set; }
    public required WindowsSettingType Type { get; set; }
    public required string Command { get; set; }

    public string DisplayType
    {
        get => field ?? Enum.GetName(Type) ?? "<Unknown>";
        set;
    }

    /// <summary>
    /// The areas of this setting. The order is fixed to the order in json.
    /// </summary>
    public IList<string>? Areas { get; set; }

    /// <summary>
    /// The alternative names of this setting.
    /// </summary>
    public IEnumerable<string>? AltNames { get; set; }

    /// <summary>
    /// An additional note of this settings.
    /// <para>(e.g. why is not supported on your system)</para>
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// The value with the generated area path as string.
    /// This IS NOT part of the JSON data.
    /// </summary>
    public string JoinedAreaPath
    {
        get
        {
            field ??= Areas is null ? string.Empty : string.Join(PATH_DELIMITER_SEQUENCE, Areas);
            return field;
        }
    }

    /// <summary>
    /// The value with the generated full settings path (App and areas) as string.
    /// This IS NOT part of the JSON data.
    /// </summary>
    public string JoinedFullSettingsPath
    {
        get
        {
            if (field is null)
            {
                string path = string.IsNullOrEmpty(JoinedAreaPath) ? Command : JoinedAreaPath;
                field = $"{DisplayType}{PATH_DELIMITER_SEQUENCE}{path}";
            }

            return field;
        }
    }

    public string? IconGlyph { get; set; }
}
