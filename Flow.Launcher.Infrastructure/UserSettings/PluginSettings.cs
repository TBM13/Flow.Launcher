using System.Collections.Generic;
using System.Text.Json.Serialization;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Infrastructure.UserSettings
{
    public class PluginsSettings : BaseModel
    {
        /// <summary>
        /// Only used for serialization
        /// </summary>
        public Dictionary<string, Plugin> Plugins { get; set; } = [];

        /// <summary>
        /// Update plugin settings with metadata.
        /// FL will get default values from metadata first and then load settings to metadata
        /// </summary>
        /// <param name="metadatas">Parsed plugin metadatas</param>
        public void UpdatePluginSettings(IEnumerable<PluginMetadata> metadatas)
        {
            foreach (var metadata in metadatas)
            {
                if (Plugins.TryGetValue(metadata.ID, out var settings))
                {
                    // If settings exist, update settings & metadata value
                    // update settings values with metadata
                    if (string.IsNullOrEmpty(settings.Version))
                    {
                        settings.Version = metadata.Version;
                    }
                    settings.DefaultActionKeywords = metadata.ActionKeywords; // metadata provides default values

                    // update metadata values with settings
                    if (settings.ActionKeywords?.Count > 0)
                        metadata.ActionKeywords = settings.ActionKeywords;

                    metadata.Disabled = settings.Disabled;
                    metadata.Priority = settings.Priority;
                    metadata.HomeDisabled = settings.HomeDisabled;
                }
                else
                {
                    // If settings does not exist, create a new one
                    Plugins[metadata.ID] = new Plugin
                    {
                        ID = metadata.ID,
                        Name = metadata.Name,
                        Version = metadata.Version,
                        DefaultActionKeywords = metadata.ActionKeywords, // metadata provides default values
                        ActionKeywords = metadata.ActionKeywords, // use default value
                        Disabled = metadata.Disabled,
                        HomeDisabled = metadata.HomeDisabled,
                        Priority = metadata.Priority
                    };
                }
            }
        }

        public Plugin? GetPluginSettings(string id)
        {
            if (Plugins.TryGetValue(id, out var plugin))
            {
                return plugin;
            }
            return null;
        }

        public Plugin? RemovePluginSettings(string id)
        {
            Plugins.Remove(id, out var plugin);
            return plugin;
        }
    }

    public class Plugin
    {
        public required string ID { get; set; }

        public required string Name { get; set; }

        public required string Version { get; set; }

        [JsonIgnore]
        public List<string> DefaultActionKeywords { get; set; }

        // a reference of the action keywords from plugin manager
        public required List<string> ActionKeywords { get; set; }

        public required int Priority { get; set; }

        /// <summary>
        /// Used only to save the state of the plugin in settings
        /// </summary>
        public bool Disabled { get; set; }
        public bool HomeDisabled { get; set; }
    }
}
