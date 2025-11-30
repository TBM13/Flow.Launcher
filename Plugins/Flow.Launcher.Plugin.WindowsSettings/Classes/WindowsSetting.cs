using System.Collections.Generic;

namespace Flow.Launcher.Plugin.WindowsSettings.Classes
{
    internal record WindowsSetting(string Name, string Area, string Command, string Type)
    {
        /// <summary>
        /// The type display name of this setting.
        /// </summary>
        public string? DisplayType { get; set; }

        /// <summary>
        /// The alternative names of this setting.
        /// </summary>
        public IEnumerable<string>? AltNames { get; set; }

        /// <summary>
        /// The Keywords names of this task link.
        /// </summary>
        public IEnumerable<IEnumerable<string>>? Keywords { get; set; }

        public GlyphInfo? IconGlyph { get; set; }

        /// <summary>
        /// An additional note of this settings.
        /// <para>(e.g. why is not supported on your system)</para>
        /// </summary>
        public string? Note { get; set; }

        /// <summary>
        /// The minimum needed Windows build for this setting.
        /// </summary>
        public uint? IntroducedInBuild { get; set; }

        /// <summary>
        /// The Windows build since this settings is not longer present.
        /// </summary>
        public uint? DeprecatedInBuild { get; set; }
    }
}
