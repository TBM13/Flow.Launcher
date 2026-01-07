using System;
using System.Collections.Generic;
using System.Reflection;
#pragma warning disable IDE0005
using Flow.Launcher.Infrastructure.Logger;
#pragma warning restore IDE0005
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Core.Plugin
{
    public static class PluginsLoader
    {
        private static readonly string ClassName = nameof(PluginsLoader);

        public static List<PluginPair> Plugins(List<PluginMetadata> metadatas, PluginsSettings settings)
        {
            var dotnetPlugins = DotNetPlugins(metadatas);
            return dotnetPlugins;
        }

        private static List<PluginPair> DotNetPlugins(List<PluginMetadata> source)
        {
            var erroredPlugins = new List<string>();
            var plugins = new List<PluginPair>();

            foreach (var metadata in source)
            {
                Assembly assembly = null;
                IAsyncPlugin plugin = null;

                try
                {
                    var assemblyLoader = new PluginAssemblyLoader(metadata.ExecuteFilePath);
                    assembly = assemblyLoader.LoadAssemblyAndDependencies();

                    var type = PluginAssemblyLoader.FromAssemblyGetTypeOfInterface(assembly,
                        typeof(IAsyncPlugin));

                    plugin = Activator.CreateInstance(type) as IAsyncPlugin;

                    metadata.AssemblyName = assembly.GetName().Name;
                }
#if DEBUG
                catch (Exception)
                {
                    throw;
                }
#else
                catch (Exception e) when (assembly == null)
                {
                    PublicApi.Instance.LogException(ClassName, $"Couldn't load assembly for the plugin: {metadata.Name}", e);
                }
                catch (InvalidOperationException e)
                {
                    PublicApi.Instance.LogException(ClassName, $"Can't find the required IPlugin interface for the plugin: <{metadata.Name}>", e);
                }
                catch (ReflectionTypeLoadException e)
                {
                    PublicApi.Instance.LogException(ClassName, $"The GetTypes method was unable to load assembly types for the plugin: <{metadata.Name}>", e);
                }
                catch (Exception e)
                {
                    PublicApi.Instance.LogException(ClassName, $"The following plugin has errored and can not be loaded: <{metadata.Name}>", e);
                }
#endif

                if (plugin == null)
                {
                    erroredPlugins.Add(metadata.Name);
                    continue;
                }

                plugins.Add(new PluginPair { Plugin = plugin, Metadata = metadata });
            }

            if (erroredPlugins.Count > 0)
            {
                var errorPluginString = string.Join(Environment.NewLine, erroredPlugins);

                var errorMessage = erroredPlugins.Count > 1 ?
                    Localize.pluginsHaveErrored() :
                    Localize.pluginHasErrored();

                PublicApi.Instance.ShowMsgError($"{errorMessage}{Environment.NewLine}{Environment.NewLine}" +
                    $"{errorPluginString}{Environment.NewLine}{Environment.NewLine}" +
                    Localize.referToLogs());
            }

            return plugins;
        }
    }
}
