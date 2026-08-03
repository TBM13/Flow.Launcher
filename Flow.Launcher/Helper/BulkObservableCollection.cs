using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Flow.Launcher.Helper;

public class BulkObservableCollection<T> : ObservableCollection<T>
{
    // Cache event args
    private static readonly PropertyChangedEventArgs CountEventArgs = new(nameof(Count));
    private static readonly PropertyChangedEventArgs IndexerEventArgs = new("Item[]");
    private static readonly NotifyCollectionChangedEventArgs ResetEventArgs = new(NotifyCollectionChangedAction.Reset);

    public BulkObservableCollection() { }
    public BulkObservableCollection(IEnumerable<T> collection) : base(collection) { }
    public BulkObservableCollection(List<T> list) : base(list) { }

    /// <summary>
    /// Replaces the collection's contents with new items in a single batch.
    /// </summary>
    public void ReplaceAll(IEnumerable<T> newItems)
    {
        CheckReentrancy();

        if (Items is List<T> list)
        {
            list.Clear();

            // If the amount of new items is low and the list has a large capacity,
            // trim the excess to avoid memory bloat
            bool trimmedExcess = false;
            if (newItems.TryGetNonEnumeratedCount(out int newCount))
            {
                if (list.Capacity > 4000 && newCount < 1000)
                    list.Capacity = newCount;

                trimmedExcess = true;
            }

            list.AddRange(newItems);

            // If we failed to trim the excess capacity earlier, do it now
            if (!trimmedExcess && list.Capacity > 4000 && list.Count < 1000)
                list.TrimExcess();
        }
        else
            throw new InvalidOperationException("Expected Items to be of type List<T>");

        // Notify UI of batch changes
        using (BlockReentrancy())
        {
            OnPropertyChanged(CountEventArgs);
            OnPropertyChanged(IndexerEventArgs);
            OnCollectionChanged(ResetEventArgs);
        }
    }
}
