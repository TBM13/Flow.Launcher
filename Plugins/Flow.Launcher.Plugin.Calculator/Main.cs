using Flow.Launcher.Plugin.Calculator.Engine;
using Flow.Launcher.PluginSDK;
using Flow.Launcher.PluginSDK.Plugins;
using Flow.Launcher.PluginSDK.Plugins.Interfaces;

namespace Flow.Launcher.Plugin.Calculator;

public static class PluginMetadataDefinition
{
    public static readonly PluginMetadata Metadata = new()
    {
        ID = "999fc3a66fe54b7cbbba439d8e835efe",
        ActionKeywords = ["*"],
        Name = "Calculator",
        Description = "Perform basic mathematical calculations and bitwise operations.",
        Author = "TBM13",
        Version = "1.0.0",
        IcoPath = "Images/Plugin.Calculator.png",

        Plugin = new Main()
    };
}

public class Main : IPlugin
{
    public void Init(PluginInitContext context)
    {

    }

    public List<Result>? Query(Query query)
    {
        if (query.IsHomeQuery || query.Search.Length < 2)
            return null;

        Value? value;
        try
        {
            value = Evaluator.Evaluate(query.Search);
        }
        catch (Exception e)
        {
            Result error = new()
            {
                Title = "Error",
                SubTitle = e.Message,
                IcoPath = PluginMetadataDefinition.Metadata.IcoPath,
                Score = 300,
            };

            return [error];
        }

        if (value is not { } v)
            return null;

        string valueString = v.ToString();
        Result res = new()
        {
            Title = valueString,
            SubTitle = v.IsDecimal ? string.Empty : $"0x{v.AsInt128():X}",
            IcoPath = PluginMetadataDefinition.Metadata.IcoPath,
            Score = 300,
            CopyText = valueString
        };

        return [res];
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
