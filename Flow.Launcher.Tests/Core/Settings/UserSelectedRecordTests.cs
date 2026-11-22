using Flow.Launcher.Core.Settings;
using Flow.Launcher.PluginSDK;

namespace Flow.Launcher.Tests.Core.Settings;

public class UserSelectedRecordTests
{
    [Fact]
    public void AddAndGetSelectedCount_TracksCaseInsensitiveResultAndCaseSensitiveKeys()
    {
        UserSelectedRecord record = new();
        Query query1 = new()
        {
            OriginalQuery = "cfg Display",
            Search = "Display",
            ActionKeyword = "cfg",
            IsHomeQuery = false,
            IsReQuery = false
        };
        Query query1DifferentCase = query1 with { Search = "display" };
        Query queryDifferentKeywordCase = query1 with { ActionKeyword = "CFG", Search = "display" };

        Result result = new() { Title = "Display Settings", SubTitle = "System" };
        Result resultDifferentCase = new() { Title = "display settings", SubTitle = "SYSTEM" };

        record.Add(result, query1);

        Assert.Equal(6, record.GetSelectedCount(resultDifferentCase, query1DifferentCase));
        Assert.Equal(1, record.GetSelectedCount(resultDifferentCase, queryDifferentKeywordCase));
        Assert.Equal(1, record.GetSelectedCount(resultDifferentCase, query: null));
    }

    [Fact]
    public void AddAndGetSelectedCount_UsesRecordKeyWhenPresent()
    {
        UserSelectedRecord record = new();
        Query query = new()
        {
            OriginalQuery = "calc",
            Search = "calc",
            ActionKeyword = string.Empty,
            IsHomeQuery = false,
            IsReQuery = false
        };

        Result first = new() { Title = "Title A", SubTitle = "Sub A", RecordKey = "AppId-1" };
        Result renamedSameKey = new() { Title = "Title B", SubTitle = "Sub B", RecordKey = "AppId-1" };
        Result differentCaseKey = new() { Title = "Title B", SubTitle = "Sub B", RecordKey = "appid-1" };

        record.Add(first, query);

        Assert.Equal(6, record.GetSelectedCount(renamedSameKey, query));
        Assert.Equal(0, record.GetSelectedCount(differentCaseKey, query));
    }

    [Fact]
    public async Task ConcurrentAddAndGetSelectedCount_AreThreadSafe()
    {
        UserSelectedRecord record = new();
        Query query = new()
        {
            OriginalQuery = "app",
            Search = "app",
            ActionKeyword = string.Empty,
            IsHomeQuery = false,
            IsReQuery = false
        };

        Result[] results = [.. Enumerable.Range(0, 20).Select(i => new Result { Title = $"App {i}", SubTitle = "Program" })];

        Task[] writers = [.. Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            for (int iter = 0; iter < 50; iter++)
            {
                foreach (Result result in results)
                    record.Add(result, query);
            }
        }))];

        Task[] readers = [.. Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            for (int iter = 0; iter < 100; iter++)
            {
                foreach (Result result in results)
                    _ = record.GetSelectedCount(result, query);
            }
        }))];

        await Task.WhenAll([.. writers, .. readers]);

        foreach (Result result in results)
            Assert.Equal(4 * 50 * 6, record.GetSelectedCount(result, query));
    }
}
