using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Core.UserSettings;
using Flow.Launcher.PluginSDK;
using Flow.Launcher.PluginSDK.Hotkeys;
using Flow.Launcher.PluginSDK.Plugins;
using Flow.Launcher.PluginSDK.Plugins.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;

namespace Flow.Launcher.Tests.Core.Plugin;

public class PluginManagerTests
{
    private static PluginMetadata CreateMetadata(
        string id,
        string name,
        IPlugin plugin,
        List<string>? actionKeywords = null,
        int priority = 0,
        bool disabled = false)
    {
        return new PluginMetadata
        {
            ID = id,
            Name = name,
            Author = "Test",
            Version = "1.0.0",
            Description = "Test plugin",
            ActionKeywords = actionKeywords ?? [Query.GlobalPluginWildcard],
            Plugin = plugin,
            IcoPath = "test.png",
            Priority = priority,
            Disabled = disabled,
        };
    }

    [Fact]
    public async Task InitializePluginsAsync_MarksSucceededAndDisablesFailedPlugins()
    {
        TestPlugin goodPlugin = new();
        TestPlugin badPlugin = new() { ThrowOnInit = true };

        PluginMetadata goodMeta = CreateMetadata("good", "Good Plugin", goodPlugin, ["g"]);
        PluginMetadata badMeta = CreateMetadata("bad", "Bad Plugin", badPlugin, ["b"]);

        PluginsSettings settings = new();
        await using PluginManager manager = new(NullLoggerFactory.Instance, [goodMeta, badMeta]);
        manager.LoadPlugins(settings);

        await manager.InitializePluginsAsync();

        Assert.Equal(2, manager.GetAllLoadedPlugins().Count);
        IReadOnlyList<PluginMetadata> succeeded = manager.GetAllInitializedPlugins(includeFailed: false);
        IReadOnlyList<PluginMetadata> allInit = manager.GetAllInitializedPlugins(includeFailed: true);

        Assert.Single(succeeded);
        Assert.Equal("good", succeeded[0].ID);
        Assert.Equal(2, allInit.Count);

        Assert.False(goodMeta.Disabled);
        Assert.True(badMeta.Disabled);
        Assert.True(badMeta.HomeDisabled);
        Assert.True(settings.GetPluginSettings("bad")!.Disabled);
        Assert.True(settings.GetPluginSettings("bad")!.HomeDisabled);
    }

    [Fact]
    public async Task QueryForPluginAsync_BeforeInitialization_ReturnsPlaceholderForNormalQueryOnly()
    {
        TestPlugin plugin = new();
        PluginMetadata meta = CreateMetadata("p1", "Pending Plugin", plugin);
        await using PluginManager manager = new(NullLoggerFactory.Instance, [meta]);

        Query normalQuery = manager.BuildQuery("hello", isReQuery: false);
        List<Result> normalResults = await CollectAsync(
            manager.QueryForPluginAsync(meta, normalQuery, TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        Assert.Single(normalResults);
        Assert.Equal("Pending Plugin: This plugin is still initializing...", normalResults[0].Title);

        Query homeQuery = manager.BuildQuery(string.Empty, isReQuery: false);
        List<Result> homeResults = await CollectAsync(
            manager.QueryForPluginAsync(meta, homeQuery, TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        Assert.Empty(homeResults);
    }

    [Fact]
    public async Task QueryForPluginAsync_StreamsResultsIncrementallyWithoutBuffering()
    {
        TaskCompletionSource releaseSecond = new(TaskCreationOptions.RunContinuationsAsynchronously);
        StreamingTestPlugin plugin = new((_, token) => StreamResults(token));

        async IAsyncEnumerable<Result> StreamResults([EnumeratorCancellation] CancellationToken token)
        {
            yield return new Result { Title = "First" };
            await releaseSecond.Task.WaitAsync(token);
            yield return new Result { Title = "Second" };
        }

        PluginMetadata meta = CreateMetadata("stream", "Streaming Plugin", plugin);
        await using PluginManager manager = new(NullLoggerFactory.Instance, [meta]);
        await manager.InitializePluginsAsync();

        Query query = manager.BuildQuery("test", isReQuery: false);
        await using IAsyncEnumerator<Result> enumerator = manager
            .QueryForPluginAsync(meta, query, TestContext.Current.CancellationToken)
            .GetAsyncEnumerator(TestContext.Current.CancellationToken);

        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal("First", enumerator.Current.Title);

        ValueTask<bool> secondMove = enumerator.MoveNextAsync();
        Assert.False(secondMove.IsCompleted);

        releaseSecond.SetResult();
        Assert.True(await secondMove);
        Assert.Equal("Second", enumerator.Current.Title);
        Assert.False(await enumerator.MoveNextAsync());
    }

    [Fact]
    public async Task QueryForPluginAsync_HandlesMidStreamException()
    {
        StreamingTestPlugin plugin = new((_, _) => ThrowMidStream());

        static async IAsyncEnumerable<Result> ThrowMidStream()
        {
            yield return new Result { Title = "Partial" };
            await Task.Yield();
            throw new InvalidOperationException("Boom");
        }

        PluginMetadata meta = CreateMetadata("err", "Error Plugin", plugin);
        await using PluginManager manager = new(NullLoggerFactory.Instance, [meta]);
        await manager.InitializePluginsAsync();

        Query normalQuery = manager.BuildQuery("test", isReQuery: false);
        List<Result> normalResults = await CollectAsync(
            manager.QueryForPluginAsync(meta, normalQuery, TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, normalResults.Count);
        Assert.Equal("Partial", normalResults[0].Title);
        Assert.Equal("Error Plugin: Failed to respond!", normalResults[1].Title);

        Query homeQuery = manager.BuildQuery(string.Empty, isReQuery: false);
        List<Result> homeResults = await CollectAsync(
            manager.QueryForPluginAsync(meta, homeQuery, TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken);

        Assert.Single(homeResults);
        Assert.Equal("Partial", homeResults[0].Title);
    }

    [Fact]
    public async Task QueryForPluginAsync_HonorsCancellationMidStream()
    {
        TaskCompletionSource waitForever = new(TaskCreationOptions.RunContinuationsAsynchronously);
        StreamingTestPlugin plugin = new((_, token) => StreamWithCancel(token));

        async IAsyncEnumerable<Result> StreamWithCancel([EnumeratorCancellation] CancellationToken token)
        {
            yield return new Result { Title = "First" };
            await waitForever.Task.WaitAsync(token);
            yield return new Result { Title = "ShouldNotAppear" };
        }

        PluginMetadata meta = CreateMetadata("cancel", "Cancel Plugin", plugin);
        await using PluginManager manager = new(NullLoggerFactory.Instance, [meta]);
        await manager.InitializePluginsAsync();

        using CancellationTokenSource cts = new();
        Query query = manager.BuildQuery("test", isReQuery: false);
        List<Result> collected = [];

        await foreach (Result result in manager.QueryForPluginAsync(meta, query, cts.Token))
        {
            collected.Add(result);
            cts.Cancel();
        }

        Assert.Single(collected);
        Assert.Equal("First", collected[0].Title);
    }

    [Fact]
    public async Task ActionKeywords_BuildQueryRoutingAndMutationsWorkCorrectly()
    {
        PluginMetadata globalMeta = CreateMetadata("global", "Global", new TestPlugin(), [Query.GlobalPluginWildcard]);
        PluginMetadata symbolMeta = CreateMetadata("sys", "Sys", new TestPlugin(), [">"]);
        PluginMetadata kwMeta = CreateMetadata("kill", "Kill", new TestPlugin(), ["kill"]);

        PluginsSettings settings = new();
        await using PluginManager manager = new(NullLoggerFactory.Instance, [globalMeta, symbolMeta, kwMeta]);
        manager.LoadPlugins(settings);
        await manager.InitializePluginsAsync();

        Query globalQuery = manager.BuildQuery("notepad", isReQuery: false);
        Assert.Equal(string.Empty, globalQuery.ActionKeyword);
        Assert.Equal("notepad", globalQuery.Search);
        Assert.Single(manager.ValidPluginsForQuery(globalQuery));
        Assert.Equal("global", manager.ValidPluginsForQuery(globalQuery)[0].ID);

        Query symbolQuery = manager.BuildQuery(">shut", isReQuery: false);
        Assert.Equal(">", symbolQuery.ActionKeyword);
        Assert.Equal("shut", symbolQuery.Search);
        Assert.Single(manager.ValidPluginsForQuery(symbolQuery));
        Assert.Equal("sys", manager.ValidPluginsForQuery(symbolQuery)[0].ID);

        manager.AddActionKeyword("kill", "pk");
        Assert.True(manager.ActionKeywordRegistered("pk"));
        Assert.Contains("pk", kwMeta.ActionKeywords);
        Assert.Contains("pk", settings.GetPluginSettings("kill")!.ActionKeywords);
        Assert.DoesNotContain("pk", settings.GetPluginSettings("kill")!.DefaultActionKeywords);

        Query addedKeywordQuery = manager.BuildQuery("pk chrome", isReQuery: false);
        Assert.Equal("pk", addedKeywordQuery.ActionKeyword);
        Assert.Equal("chrome", addedKeywordQuery.Search);
        Assert.Equal("kill", manager.ValidPluginsForQuery(addedKeywordQuery)[0].ID);

        manager.RemoveActionKeyword("kill", "pk");
        Assert.False(manager.ActionKeywordRegistered("pk"));
        Assert.DoesNotContain("pk", kwMeta.ActionKeywords);
        Assert.DoesNotContain("pk", settings.GetPluginSettings("kill")!.ActionKeywords);
    }

    [Fact]
    public async Task GetContextMenusForPluginAsync_ReturnsItemsAndHandlesErrors()
    {
        TestPlugin goodPlugin = new()
        {
            ContextMenuItems = [new Result { Title = "Menu 1" }, new Result { Title = "Menu 2" }]
        };
        TestPlugin throwingPlugin = new() { ThrowOnContextMenu = true };
        MinimalPlugin noMenuPlugin = new();

        PluginMetadata goodMeta = CreateMetadata("good", "Good", goodPlugin);
        PluginMetadata badMeta = CreateMetadata("bad", "Bad", throwingPlugin);
        PluginMetadata noMenuMeta = CreateMetadata("nomenu", "NoMenu", noMenuPlugin);

        await using PluginManager manager = new(NullLoggerFactory.Instance, [goodMeta, badMeta, noMenuMeta]);
        await manager.InitializePluginsAsync();

        Result dummyResult = new() { Title = "Item" };
        ActionContext dummyContext = new()
        {
            PressedKeys = new FakePressedKeys(),
            ResultPosition = new Point(0, 0)
        };

        List<Result> goodMenus = await manager.GetContextMenusForPluginAsync("good", dummyResult, dummyContext, TestContext.Current.CancellationToken);
        Assert.Equal(["Menu 1", "Menu 2"], goodMenus.Select(r => r.Title));

        List<Result> badMenus = await manager.GetContextMenusForPluginAsync("bad", dummyResult, dummyContext, TestContext.Current.CancellationToken);
        Assert.Empty(badMenus);

        List<Result> noMenus = await manager.GetContextMenusForPluginAsync("nomenu", dummyResult, dummyContext, TestContext.Current.CancellationToken);
        Assert.Empty(noMenus);
    }

    [Fact]
    public async Task GenerateMagicQuery_RespectsPriorityAndSkipsDisabledOrThrowingPlugins()
    {
        MagicPlugin highNull = new() { MagicResult = null };
        MagicPlugin throwingHigher = new() { ThrowOnMagic = true };
        MagicPlugin disabledHighest = new() { MagicResult = "disabled-query" };
        MagicPlugin lowWinner = new() { MagicResult = "winner-query" };

        PluginMetadata m1 = CreateMetadata("1", "HighNull", highNull, [], priority: 10);
        PluginMetadata m2 = CreateMetadata("2", "Throwing", throwingHigher, [], priority: 8);
        PluginMetadata m3 = CreateMetadata("3", "Disabled", disabledHighest, [], priority: 20, disabled: true);
        PluginMetadata m4 = CreateMetadata("4", "LowWinner", lowWinner, [], priority: 5);

        await using PluginManager manager = new(NullLoggerFactory.Instance, [m4, m1, m3, m2]);
        await manager.InitializePluginsAsync();

        string? result = manager.GenerateMagicQuery();
        Assert.Equal("winner-query", result);
    }

    [Fact]
    public async Task DisposeAsync_DisposesInitializedAndFailedPluginsOnly()
    {
        TestPlugin goodPlugin = new();
        TestPlugin failedPlugin = new() { ThrowOnInit = true };
        TestPlugin uninitializedPlugin = new();

        PluginMetadata goodMeta = CreateMetadata("good", "Good", goodPlugin);
        PluginMetadata failedMeta = CreateMetadata("failed", "Failed", failedPlugin);

        PluginManager manager = new(NullLoggerFactory.Instance, [goodMeta, failedMeta]);
        await manager.InitializePluginsAsync();
        await manager.DisposeAsync();

        Assert.True(goodPlugin.Disposed);
        Assert.True(failedPlugin.Disposed);

        PluginMetadata uninitMeta = CreateMetadata("uninit", "Uninit", uninitializedPlugin);
        PluginManager uninitManager = new(NullLoggerFactory.Instance, [uninitMeta]);
        await uninitManager.DisposeAsync();

        Assert.False(uninitializedPlugin.Disposed);
    }

    private static async Task<List<Result>> CollectAsync(IAsyncEnumerable<Result> source, CancellationToken token)
    {
        List<Result> results = [];
        await foreach (Result item in source.WithCancellation(token))
            results.Add(item);
        return results;
    }

    private sealed class MinimalPlugin : IPlugin
    {
        public ValueTask InitAsync(PluginInitContext context) => ValueTask.CompletedTask;
    }

    private sealed class TestPlugin : IPlugin, IContextMenu
    {
        public bool ThrowOnInit { get; init; }
        public bool ThrowOnContextMenu { get; init; }
        public List<Result> ContextMenuItems { get; init; } = [];
        public bool Disposed { get; private set; }

        public ValueTask InitAsync(PluginInitContext context)
        {
            if (ThrowOnInit)
                throw new InvalidOperationException("Init failed");

            return ValueTask.CompletedTask;
        }

        public async IAsyncEnumerable<Result> QueryAsync(
            Query query,
            [EnumeratorCancellation] CancellationToken token)
        {
            yield break;
        }

        public async IAsyncEnumerable<Result> LoadContextMenusAsync(
            Result selectedResult,
            ActionContext context,
            [EnumeratorCancellation] CancellationToken token = default)
        {
            if (ThrowOnContextMenu)
                throw new InvalidOperationException("Context menu failed");

            foreach (Result item in ContextMenuItems)
                yield return item;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StreamingTestPlugin(Func<Query, CancellationToken, IAsyncEnumerable<Result>> queryHandler)
        : IPlugin
    {
        public ValueTask InitAsync(PluginInitContext context) => ValueTask.CompletedTask;

        public IAsyncEnumerable<Result> QueryAsync(Query query, CancellationToken token)
            => queryHandler(query, token);
    }

    private sealed class MagicPlugin : IPlugin, IMagicQueryProvider
    {
        public string? MagicResult { get; init; }
        public bool ThrowOnMagic { get; init; }

        public ValueTask InitAsync(PluginInitContext context) => ValueTask.CompletedTask;

        public string? GenerateMagicQuery()
        {
            if (ThrowOnMagic)
                throw new InvalidOperationException("Magic query failed");

            return MagicResult;
        }
    }

    private sealed class FakePressedKeys : IPressedKeys
    {
        public int PressedCount => 0;
        public int PressedModifiersCount => 0;
        public bool AltPressed => false;
        public bool CtrlPressed => false;
        public bool ShiftPressed => false;
        public bool WindowsPressed => false;
        public bool IsKeyPressed(Key key) => false;
        public bool IsModifierPressed(ModifierKeys modifiers) => false;
        public bool OnlyKeyPressed(Key key) => false;
        public bool OnlyModifiersPressed(ModifierKeys modifiers) => false;
        public Hotkey ToHotkey() => default;
    }
}
