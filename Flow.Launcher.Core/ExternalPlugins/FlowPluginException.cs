using System;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core.ExternalPlugins
{
    public class FlowPluginException(PluginMetadata metadata, Exception e) : Exception(e.Message, e)
    {
        public PluginMetadata Metadata { get; set; } = metadata;

        public override string ToString()
        {
            return $@"{Metadata.Name} Exception: 
Author: {Metadata.Author}
Version: {Metadata.Version}
{base.ToString()}";
        }
    }
}
