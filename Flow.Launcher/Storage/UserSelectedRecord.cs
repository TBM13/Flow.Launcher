using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Flow.Launcher.PluginSDK;

namespace Flow.Launcher.Storage;

public class UserSelectedRecord
{
    [JsonInclude]
#pragma warning disable IDE0044 // Add readonly modifier
    private Dictionary<long, int> _recordsWithQuery = [];
#pragma warning restore IDE0044 // Add readonly modifier

    public void Add(Result result)
    {
        if (result.OriginQuery is not null)
            Increment(GetQueryAndResultHashCode(result.OriginQuery, result));

        Increment(GetResultHashCode(result));
    }

    public int GetSelectedCount(Result result)
    {
        int queryCount = result.OriginQuery is not null
            ? _recordsWithQuery.GetValueOrDefault(GetQueryAndResultHashCode(result.OriginQuery, result))
            : 0;

        int resultCount = _recordsWithQuery.GetValueOrDefault(GetResultHashCode(result));
        return (queryCount * 5) + resultCount;
    }

    private static long GetResultHashCode(Result result) =>
        string.IsNullOrEmpty(result.RecordKey)
            ? Hash64(domain: 1, [result.Title.ToLowerInvariant(), result.SubTitle.ToLowerInvariant()])
            : Hash64(domain: 1, [result.RecordKey]);

    // TODO: Avoid creating new strings here. Document that RecordKey is case-sensitive. Same for ActionKeyword
    private static long GetQueryAndResultHashCode(Query query, Result result) =>
        string.IsNullOrEmpty(result.RecordKey)
            ? Hash64(domain: 2, [query.ActionKeyword, query.Search.ToLowerInvariant(), result.Title.ToLowerInvariant(), result.SubTitle.ToLowerInvariant()])
            : Hash64(domain: 2, [query.ActionKeyword, query.Search.ToLowerInvariant(), result.RecordKey]);

    private static long Hash64(byte domain, ReadOnlySpan<string?> strings)
    {
        ulong hash = 14695981039346656037UL;
        const ulong fnvPrime = 1099511628211UL;

        hash ^= domain;
        hash *= fnvPrime;

        foreach (string? str in strings)
        {
            if (str is null)
            {
                hash ^= 0xFF;
                hash *= fnvPrime;
                continue;
            }

            foreach (char c in str)
            {
                hash ^= c;
                hash *= fnvPrime;
            }

            // Separator
            hash ^= '\0';
            hash *= fnvPrime;
        }

        return (long)hash;
    }

    private void Increment(long key)
    {
        ref int count = ref CollectionsMarshal.GetValueRefOrAddDefault(_recordsWithQuery, key, out _);
        count++;
    }
}
