using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Flow.Launcher.Plugin.WindowsSettings.Classes;

namespace Flow.Launcher.Plugin.WindowsSettings.Helper
{
    /// <summary>
    /// Helper for the JSON file that contains all Windows settings.
    /// </summary>
    internal static class JsonSettingsListHelper
    {
        private const string SETTINGS_FILE = "WindowsSettings.json";
        private static readonly JsonSerializerOptions _options = new()
        {
            Converters =
            {
                new JsonStringEnumConverter()
            }
        };

        internal static IEnumerable<WindowsSetting> ReadAllPossibleSettings()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var type = assembly.GetTypes().FirstOrDefault(x => x.Name == nameof(Main));

            var resourceName = $"{type?.Namespace}.{SETTINGS_FILE}";
            using var stream = assembly.GetManifestResourceStream(resourceName) ?? throw new Exception("stream is null");
            using var reader = new StreamReader(stream);
            var text = reader.ReadToEnd();

            return JsonSerializer.Deserialize<IEnumerable<WindowsSetting>>(text, _options) ?? [];
        }
    }
}
