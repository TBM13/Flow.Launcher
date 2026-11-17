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

        // Materialize before clearing Items so deferred queries referencing this collection don't read an empty list
        ICollection<T> materialized = newItems as ICollection<T> ?? [.. newItems];
        if (ReferenceEquals(materialized, this) || ReferenceEquals(materialized, Items))
            materialized = [.. materialized];

        if (Items is List<T> list)
        {
            list.Clear();

            // If the amount of new items is low and the list has a large capacity,
            // trim the excess to avoid memory bloat
            if (list.Capacity > 4000 && materialized.Count < 1000)
                list.Capacity = materialized.Count;

            list.AddRange(materialized);
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
