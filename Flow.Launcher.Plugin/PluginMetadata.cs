using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;

namespace Flow.Launcher.Plugin
{
    public record PluginMetadata
    {
        public required string ID { get; init; }
        public required string Name { get; set; }
        public required string Author { get; init; }
        public required string Version { get; init; }
        /// <summary>
        /// See <see cref="AllowedLanguage"/>.
        /// </summary>
        public required string Language { get; init; }
        public required string Description { get; set; }
        public required string Website { get; init; }
        public required List<string> ActionKeywords { get; set; }

        /// <summary>
        /// Whether the plugin is disabled.
        /// </summary>
        public bool Disabled { get; set; }
        /// <summary>
        /// Whether the plugin is disabled in home query.
        /// </summary>
        public bool HomeDisabled { get; set; }

#pragma warning disable CS8618
        public string ExecuteFilePath { get; private set; }
#pragma warning restore CS8618
        public required string ExecuteFileName { get; set; }
#pragma warning disable CS8618
        [JsonIgnore]
        public string AssemblyName { get; internal set; }
#pragma warning restore CS8618

        /// <summary>
        /// Plugin source directory.
        /// </summary>
#pragma warning disable CS9264
        public string PluginDirectory
#pragma warning restore CS9264
        {
            get => field;
            internal set
            {
                field = value;
                ExecuteFilePath = Path.Combine(value, ExecuteFileName);
                IcoPath = Path.Combine(value, IcoPath);
            }
        }

        /// <summary>
        /// Plugin icon path.
        /// </summary>
        public required string IcoPath { get; set; }

        [JsonIgnore]
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
