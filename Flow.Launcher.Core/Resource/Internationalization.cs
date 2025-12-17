using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core.Resource
{
    public class Internationalization
    {
        private static readonly string ClassName = nameof(Internationalization);

        private const string Folder = "Languages";
        private const string DefaultLanguageCode = "en";
        private const string DefaultFile = "en.xaml";
        private const string Extension = ".xaml";
        private readonly List<string> _languageDirectories = [];

        #region Initialization

        /// <summary>
        /// Initialize language. Will change app language and plugin language based on settings.
        /// </summary>
        public async Task InitializeLanguageAsync()
        {
            // Add Flow Launcher language directory
            AddFlowLauncherLanguageDirectory();

            // Add plugin language directories first so that we can load language files from plugins
            AddPluginLanguageDirectories();

            // Load default language resources
            LoadDefaultLanguage();
        }

        private void AddFlowLauncherLanguageDirectory()
        {
            // Check if Flow Launcher language directory exists
            var directory = Path.Combine(Constant.ProgramDirectory, Folder);
            if (!Directory.Exists(directory))
            {
                PublicApi.Instance.LogError(ClassName, $"Flow Launcher language directory can't be found <{directory}>");
                return;
            }

            _languageDirectories.Add(directory);
        }

        private void AddPluginLanguageDirectories()
        {
            foreach (var pluginsDir in PluginManager.Directories)
            {
                if (!Directory.Exists(pluginsDir)) continue;

                // Enumerate all top directories in the plugin directory
                foreach (var dir in Directory.GetDirectories(pluginsDir))
                {
                    // Check if the directory contains a language folder
                    var pluginLanguageDir = Path.Combine(dir, Folder);
                    if (!Directory.Exists(pluginLanguageDir)) continue;

                    // Check if the language directory contains default language file since it will be checked later
                    _languageDirectories.Add(pluginLanguageDir);
                }
            }
        }

        private void LoadDefaultLanguage()
        {
            LoadLanguage(DefaultLanguageCode);
        }

        #endregion

        #region Language Resources Management

        private void LoadLanguage(string languageCode)
        {
            var flowEnglishFile = Path.Combine(Constant.ProgramDirectory, Folder, DefaultFile);
            var dicts = Application.Current.Resources.MergedDictionaries;
            var filename = $"{languageCode}{Extension}";
            var files = _languageDirectories
                .Select(d => LanguageFile(d, filename))
                // Exclude Flow's English language file since it's built into the binary, and there's no need to load
                // it again from the file system.
                .Where(f => !string.IsNullOrEmpty(f) && f != flowEnglishFile)
                .ToArray();

            if (files.Length > 0)
            {
                foreach (var f in files)
                {
                    var r = new ResourceDictionary
                    {
                        Source = new Uri(f, UriKind.Absolute)
                    };
                    dicts.Add(r);
                }
            }
        }

        private static string LanguageFile(string folder, string language)
        {
            if (Directory.Exists(folder))
            {
                var path = Path.Combine(folder, language);
                if (File.Exists(path))
                {
                    return path;
                }
                else
                {
                    PublicApi.Instance.LogError(ClassName, $"Language path can't be found <{path}>");
                    var english = Path.Combine(folder, DefaultFile);
                    if (File.Exists(english))
                    {
                        return english;
                    }
                    else
                    {
                        PublicApi.Instance.LogError(ClassName, $"Default English Language path can't be found <{path}>");
                        return string.Empty;
                    }
                }
            }
            else
            {
                return string.Empty;
            }
        }

        #endregion

        #region Get Translations

        public static string GetTranslation(string key)
        {
            var translation = Application.Current.TryFindResource(key);
            if (translation is string)
            {
                return translation.ToString();
            }
            else
            {
                PublicApi.Instance.LogError(ClassName, $"No Translation for key {key}");
                return $"No Translation for key {key}";
            }
        }

        #endregion

        #region Update Metadata
        public static void UpdatePluginMetadataTranslation(PluginPair p)
        {
            // Update plugin metadata name & description
            if (p.Plugin is not IPluginI18n pluginI18N) return;
            try
            {
                p.Metadata.Name = pluginI18N.GetTranslatedPluginTitle();
                p.Metadata.Description = pluginI18N.GetTranslatedPluginDescription();
                pluginI18N.OnCultureInfoChanged(CultureInfo.CurrentCulture);
            }
            catch (Exception e)
            {
                PublicApi.Instance.LogException(ClassName, $"Failed for <{p.Metadata.Name}>", e);
            }
        }

        #endregion
    }
}
