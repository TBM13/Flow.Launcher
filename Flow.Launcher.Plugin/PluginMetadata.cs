using System.Collections.Generic;

namespace Flow.Launcher.Plugin
{
    public record PluginMetadata
    {
        public required string ID { get; init; }
        public required string Name { get; init; }
        public required string Author { get; init; }
        public required string Version { get; init; }
        public required string Description { get; init; }
        public required List<string> ActionKeywords { get; set; }
        public required IAsyncPlugin Plugin { get; init; }

        /// <summary>
        /// Whether the plugin is disabled.
        /// </summary>
        public bool Disabled { get; set; }
        /// <summary>
        /// Whether the plugin is disabled in home query.
        /// </summary>
        public bool HomeDisabled { get; set; }

        /// <summary>
        /// Plugin icon path.
        /// </summary>
        public required string IcoPath { get; init; }

        public int Priority { get; set; }

#pragma warning disable CS8618
        /// <summary>
        /// The path to the plugin settings directory which is not validated.
        /// It is used to store plugin settings files and data files.
        /// When plugin is deleted, FL will ask users whether to keep its settings.
        /// If users do not want to keep, this directory will be deleted.
        /// </summary>
        public string PluginSettingsDirectoryPath { get; internal set; }

        /// <summary>
        /// The path to the plugin cache directory which is not validated.
        /// It is used to store cache files.
        /// When plugin is deleted, this directory will be deleted as well.
        /// </summary>
        public string PluginCacheDirectoryPath { get; internal set; }
#pragma warning restore CS8618

        public override string ToString() => Name;
    }
}
