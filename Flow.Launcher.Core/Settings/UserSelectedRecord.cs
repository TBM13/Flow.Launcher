using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using Flow.Launcher.Core.Storage;
using Flow.Launcher.PluginSDK;

namespace Flow.Launcher.Core.Settings;

public class UserSelectedRecord
{
    private readonly Lock _lock = new();

    [JsonInclude]
#pragma warning disable IDE0044 // Add readonly modifier
    private Dictionary<long, int> _recordsWithQuery = [];
#pragma warning restore IDE0044 // Add readonly modifier

    public void Add(Result result, Query? query)
    {
        long resultHash = GetResultHashCode(result);
        long queryHash = query is not null ? GetQueryAndResultHashCode(query, result) : 0;

        lock (_lock)
        {
            if (query is not null)
                Increment(queryHash);

            Increment(resultHash);
        }
    }

    public int GetSelectedCount(Result result, Query? query)
    {
        long resultHash = GetResultHashCode(result);
        long queryHash = query is not null ? GetQueryAndResultHashCode(query, result) : 0;

        lock (_lock)
        {
            int queryCount = query is not null
                ? _recordsWithQuery.GetValueOrDefault(queryHash)
                : 0;

            int resultCount = _recordsWithQuery.GetValueOrDefault(resultHash);
            return (queryCount * 5) + resultCount;
        }
    }

    public bool SaveTo(JsonStorage<UserSelectedRecord> storage)
    {
        lock (_lock)
            return storage.TrySave();
    }

    private readonly record struct HashSegment(string? Value, bool Lowercase);

    private static long GetResultHashCode(Result result) =>
        string.IsNullOrEmpty(result.RecordKey)
            ? Hash64(domain: 1, [new(result.Title, true), new(result.SubTitle, true)])
            : Hash64(domain: 1, [new(result.RecordKey, false)]);

    // RecordKey and ActionKeyword are case-sensitive; Search, Title, and SubTitle are case-insensitive.
    private static long GetQueryAndResultHashCode(Query query, Result result) =>
        string.IsNullOrEmpty(result.RecordKey)
            ? Hash64(domain: 2, [new(query.ActionKeyword, false), new(query.Search, true), new(result.Title, true), new(result.SubTitle, true)])
            : Hash64(domain: 2, [new(query.ActionKeyword, false), new(query.Search, true), new(result.RecordKey, false)]);

    private static long Hash64(byte domain, ReadOnlySpan<HashSegment> segments)
    {
        ulong hash = 14695981039346656037UL;
        const ulong fnvPrime = 1099511628211UL;

        hash ^= domain;
        hash *= fnvPrime;

        foreach ((string? str, bool lowercase) in segments)
        {
            if (str is null)
            {
                hash ^= 0xFF;
                hash *= fnvPrime;
                continue;
            }

            if (lowercase)
            {
                foreach (char c in str)
                {
                    hash ^= char.ToLowerInvariant(c);
                    hash *= fnvPrime;
                }
            }
            else
            {
                foreach (char c in str)
                {
                    hash ^= c;
                    hash *= fnvPrime;
                }
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
