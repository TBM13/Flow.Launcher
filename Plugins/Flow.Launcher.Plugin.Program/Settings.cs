using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using Flow.Launcher.Plugin.Program.Views.Models;

namespace Flow.Launcher.Plugin.Program
{
    public class Settings
    {
        public DateTime LastIndexTime { get; set; }

        /// <summary>
        /// User-added program sources' directories
        /// </summary>
        public List<ProgramSource> ProgramSources { get; set; } = [];

        /// <summary>
        /// Disabled single programs, not including User-added directories
        /// </summary>
        public List<ProgramSource> DisabledProgramSources { get; set; } = [];

        public string[] CustomSuffixes { get; set; } = [];  // Custom suffixes only
        public string[] CustomProtocols { get; set; } = [];

        public Dictionary<string, bool> BuiltinSuffixesStatus { get; set; } = new Dictionary<string, bool>{
            { "exe", true }, { "appref-ms", true }, { "lnk", true }
        };

        public Dictionary<string, bool> BuiltinProtocolsStatus { get; set; } = new Dictionary<string, bool>{
            { "steam", true }, { "epic", true }, { "http", false }
        };

        [JsonIgnore]
        public Dictionary<string, string> BuiltinProtocols { get; set; } = new Dictionary<string, string>{
            { "steam", $"steam://run/{SuffixSeparator}steam://rungameid/" }, { "epic", "com.epicgames.launcher://apps/" }, { "http",  $"http://{SuffixSeparator}https://"}
        };

        public bool UseCustomSuffixes { get; set; } = false;
        public bool UseCustomProtocols { get; set; } = false;

        public string[] GetSuffixes()
        {
            List<string> extensions = [];
            foreach (var item in BuiltinSuffixesStatus)
            {
                if (item.Value)
                {
                    extensions.Add(item.Key);
                }
            }

            if (BuiltinProtocolsStatus.Values.Any(x => x == true) || UseCustomProtocols)
            {
                extensions.Add("url");
            }

            if (UseCustomSuffixes)
            {
                return [.. extensions.Concat(CustomSuffixes).DistinctBy(x => x.ToLower())];
            }
            else
            {
                return [.. extensions.DistinctBy(x => x.ToLower())];
            }
        }

        public string[] GetProtocols()
        {
            List<string> protocols = [];
            foreach (var item in BuiltinProtocolsStatus)
            {
                if (item.Value)
                {
                    if (BuiltinProtocols.TryGetValue(item.Key, out string ps))
                    {
                        var tmp = ps.Split(SuffixSeparator, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var protocol in tmp)
                        {
                            protocols.Add(protocol);
                        }
                    }
                }
            }

            if (UseCustomProtocols)
            {
                return [.. protocols.Concat(CustomProtocols).DistinctBy(x => x.ToLower())];
            }
            else
            {
                return [.. protocols.DistinctBy(x => x.ToLower())];
            }
        }

        public bool EnableStartMenuSource { get; set; } = true;
        public bool EnableDescription { get; set; } = false;
        public bool HideAppsPath { get; set; } = true;
        public bool HideUninstallers { get; set; } = false;
        public bool EnableRegistrySource { get; set; } = true;
        public bool EnablePathSource { get; set; } = false;
        public bool EnableUWP { get; set; } = true;
        public bool HideDuplicatedWindowsApp { get; set; } = false;

        internal const char SuffixSeparator = ';';
    }
}
