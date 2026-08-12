using System.Runtime.CompilerServices;
using System.Windows.Markup;

[assembly: InternalsVisibleTo("Flow.Launcher")]
[assembly: InternalsVisibleTo("Flow.Launcher.Core")]
[assembly: XmlnsDefinition("http://schemas.flowlauncher.com/pluginsdk", "Flow.Launcher.PluginSDK")]
[assembly: XmlnsDefinition("http://schemas.flowlauncher.com/pluginsdk", "Flow.Launcher.PluginSDK.API")]
[assembly: XmlnsDefinition("http://schemas.flowlauncher.com/pluginsdk", "Flow.Launcher.PluginSDK.UI")]
[assembly: XmlnsDefinition("http://schemas.flowlauncher.com/pluginsdk", "Flow.Launcher.PluginSDK.UI.Converters")]
[assembly: XmlnsPrefix("http://schemas.flowlauncher.com/pluginsdk", "pluginsdk")]
