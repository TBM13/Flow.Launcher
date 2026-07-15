using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Flow.Launcher.Infrastructure.Results;
using Flow.Launcher.Infrastructure.Storage;
using Flow.Launcher.Infrastructure.UserSettings;
using Microsoft.Extensions.Logging;

namespace Flow.Launcher.Storage
{
    public class FlowLauncherJsonStorageTopMostRecord
    {
        private readonly JsonStorage<MultipleTopMostRecord> _topMostRecordStorage;
        private readonly MultipleTopMostRecord _topMostRecord;

        public FlowLauncherJsonStorageTopMostRecord(ILoggerFactory loggerFactory)
        {
            _topMostRecordStorage = new JsonStorage<MultipleTopMostRecord>(
                loggerFactory, Path.Combine(DataLocation.SettingsDirectory, "MultipleTopMostRecord.json"));
            _topMostRecord = _topMostRecordStorage.TryLoad();
        }

        public bool TrySave()
        {
            return _topMostRecordStorage.TrySave();
        }

        public bool IsTopMost(Result result)
        {
            return _topMostRecord.IsTopMost(result);
        }

        public int GetTopMostIndex(Result result)
        {
            return _topMostRecord.GetTopMostIndex(result);
        }

        public void Remove(Result result)
        {
            _topMostRecord.Remove(result);
        }

        public void AddOrUpdate(Result result)
        {
            _topMostRecord.AddOrUpdate(result);
        }
    }

    /// <summary>
    /// New data structure to support multiple top most records for the same query
    /// </summary>
    internal class MultipleTopMostRecord
    {
        [JsonInclude]
        [JsonConverter(typeof(ConcurrentDictionaryConcurrentQueueConverter))]
        public ConcurrentDictionary<string, ConcurrentQueue<Record>> records { get; private set; } = new();

        internal bool IsTopMost(Result result)
        {
            // origin query is null when user select the context menu item directly of one item from query list
            // in this case, we do not need to check if the result is top most
            if (records.IsEmpty || result.OriginQuery == null ||
                !records.TryGetValue(result.OriginQuery.TrimmedQuery, out var value))
            {
                return false;
            }

            // since this dictionary should be very small (or empty) going over it should be pretty fast.
            return value.Any(record => record.Equals(result));
        }

        internal int GetTopMostIndex(Result result)
        {
            // origin query is null when user select the context menu item directly of one item from query list
            // in this case, we do not need to check if the result is top most
            if (records.IsEmpty || result.OriginQuery == null ||
                !records.TryGetValue(result.OriginQuery.TrimmedQuery, out var value))
            {
                return -1;
            }

            // since this dictionary should be very small (or empty) going over it should be pretty fast.
            // since the latter items should be more recent, we should return the smaller index for score to subtract
            // which can make them more topmost
            // A, B, C => 2, 1, 0 => (max - 2), (max - 1), (max - 0)
            var index = 0;
            foreach (var record in value)
            {
                if (record.Equals(result))
                {
                    return value.Count - 1 - index;
                }
                index++;
            }
            return -1;
        }

        internal void Remove(Result result)
        {
            // origin query is null when user select the context menu item directly of one item from query list
            // in this case, we do not need to remove the record
            if (result.OriginQuery == null ||
                !records.TryGetValue(result.OriginQuery.TrimmedQuery, out var value))
            {
                return;
            }

            // remove the record from the queue
            var queue = new ConcurrentQueue<Record>(value.Where(r => !r.Equals(result)));
            if (queue.IsEmpty)
            {
                // if the queue is empty, remove the queue from the dictionary
                records.TryRemove(result.OriginQuery.TrimmedQuery, out _);
            }
            else
            {
                // change the queue in the dictionary
                records[result.OriginQuery.TrimmedQuery] = queue;
            }
        }

        internal void AddOrUpdate(Result result)
        {
            // origin query is null when user select the context menu item directly of one item from query list
            // in this case, we do not need to add or update the record
            if (result.OriginQuery == null)
            {
                return;
            }

            var record = new Record
            {
                PluginID = result.PluginID,
                Title = result.Title,
                SubTitle = result.SubTitle,
                RecordKey = result.RecordKey
            };
            if (!records.TryGetValue(result.OriginQuery.TrimmedQuery, out var value))
            {
                // create a new queue if it does not exist
                value = new ConcurrentQueue<Record>();
                value.Enqueue(record);
                records.TryAdd(result.OriginQuery.TrimmedQuery, value);
            }
            else
            {
                // add or update the record in the queue
                var queue = new ConcurrentQueue<Record>(value.Where(r => !r.Equals(result))); // make sure we don't have duplicates
                queue.Enqueue(record);
                records[result.OriginQuery.TrimmedQuery] = queue;
            }
        }
    }

    /// <summary>
    /// Because ConcurrentQueue does not support serialization, we need to convert it to a List
    /// </summary>
    internal class ConcurrentDictionaryConcurrentQueueConverter : JsonConverter<ConcurrentDictionary<string, ConcurrentQueue<Record>>>
    {
        public override ConcurrentDictionary<string, ConcurrentQueue<Record>> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var dictionary = JsonSerializer.Deserialize<Dictionary<string, List<Record>>>(ref reader, options);
            var concurrentDictionary = new ConcurrentDictionary<string, ConcurrentQueue<Record>>();
            foreach (var kvp in dictionary)
            {
                concurrentDictionary.TryAdd(kvp.Key, new ConcurrentQueue<Record>(kvp.Value));
            }
            return concurrentDictionary;
        }

        public override void Write(Utf8JsonWriter writer, ConcurrentDictionary<string, ConcurrentQueue<Record>> value, JsonSerializerOptions options)
        {
            var dict = new Dictionary<string, List<Record>>();
            foreach (var kvp in value)
            {
                dict.Add(kvp.Key, kvp.Value.ToList());
            }
            JsonSerializer.Serialize(writer, dict, options);
        }
    }

    internal class Record
    {
        public string Title { get; init; }
        public string SubTitle { get; init; }
        public string PluginID { get; init; }
        public string RecordKey { get; init; }

        public bool Equals(Result r)
        {
            if (string.IsNullOrEmpty(RecordKey) || string.IsNullOrEmpty(r.RecordKey))
            {
                return Title == r.Title
                    && SubTitle == r.SubTitle
                    && PluginID == r.PluginID;
            }
            else
            {
                return RecordKey == r.RecordKey
                    && PluginID == r.PluginID;
            }
        }
    }
}
