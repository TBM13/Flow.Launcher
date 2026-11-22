using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using Flow.Launcher.Plugin.Calculator.Engine;
using Flow.Launcher.Plugin.Calculator.Preview;
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
    private PluginInitContext? _context;

    public ValueTask InitAsync(PluginInitContext context)
    {
        _context = context;
        return ValueTask.CompletedTask;
    }

    public async IAsyncEnumerable<Result> QueryAsync(
        Query query,
        [EnumeratorCancellation] CancellationToken token)
    {
        if (query.IsHomeQuery || query.Search.Length < 2)
            yield break;

        Value? value = null;
        Result? errorResult = null;
        try
        {
            value = Evaluator.Evaluate(query.Search);
        }
        catch (Exception e)
        {
            errorResult = new Result
            {
                Title = "Error",
                SubTitle = e.Message,
                IconOrGlyph = PluginMetadataDefinition.Metadata.IcoPath,
                Score = 300,
            };
        }

        if (errorResult is not null)
        {
            yield return errorResult;
            yield break;
        }

        if (value is not { } v)
            yield break;

        string valueString = v.ToString(CultureInfo.InvariantCulture);
        yield return new Result
        {
            Title = valueString,
            AutocompleteText = (true, valueString),
            SubTitle = v.IsDecimal ? string.Empty : $"0x{v.AsInt128():X}",
            IconOrGlyph = PluginMetadataDefinition.Metadata.IcoPath,
            Score = 300,
            CopyText = valueString,
            PreviewPanel = new Lazy<UserControl>(() => new CalculatorPreviewPanel(
                    v,
                    newValue => _context?.API.ChangeQuery(ToQueryText(newValue)),
                    text => _context?.API.CopyToClipboard(text)))
        };
    }

    private static string ToQueryText(Int128 value)
    {
        string text = value.ToString(CultureInfo.InvariantCulture);

        // Queries shorter than two characters are ignored by us,
        // so prefix single-digit values with a unary plus (e.g. "+8").
        return text.Length < 2 ? "+" + text : text;
    }
}
