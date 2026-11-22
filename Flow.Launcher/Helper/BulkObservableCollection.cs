using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Flow.Launcher.Helper;

public class BulkObservableCollection<T> : ObservableCollection<T>
{
    private const int TrimCapacityThreshold = 1024;
    private const int MinTrimmedCapacity = 64;

    private static readonly PropertyChangedEventArgs CountEventArgs = new(nameof(Count));
    private static readonly PropertyChangedEventArgs IndexerEventArgs = new("Item[]");
    private static readonly NotifyCollectionChangedEventArgs ResetEventArgs = new(NotifyCollectionChangedAction.Reset);

    public BulkObservableCollection() { }
    public BulkObservableCollection(IEnumerable<T> collection) : base(collection) { }
    public BulkObservableCollection(List<T> list) : base(list) { }

    protected override void ClearItems()
    {
        CheckReentrancy();

        if (Items.Count == 0)
            return;

        if (Items is List<T> list)
        {
            list.Clear();
            if (list.Capacity > TrimCapacityThreshold)
                list.Capacity = MinTrimmedCapacity;
        }
        else
        {
            Items.Clear();
        }

        OnPropertyChanged(CountEventArgs);
        OnPropertyChanged(IndexerEventArgs);
        OnCollectionChanged(ResetEventArgs);
    }

    /// <summary>
    /// Replaces the collection's contents with new items in a single batch.
    /// </summary>
    public void ReplaceAll(IEnumerable<T> newItems)
    {
        if (newItems is T[] array)
        {
            ReplaceAll(array.AsSpan());
            return;
        }

        if (newItems is List<T> sourceList && !ReferenceEquals(sourceList, Items))
        {
            ReplaceAll(CollectionsMarshal.AsSpan(sourceList));
            return;
        }

        // Materialize deferred sequences (or self-referential collections) before clearing Items.
        T[] materialized = [.. newItems];
        ReplaceAll(materialized.AsSpan());
    }

    /// <summary>
    /// Replaces the collection's contents with the items from the span in a single batch.
    /// </summary>
    public void ReplaceAll(ReadOnlySpan<T> newItems)
    {
        CheckReentrancy();

        if (Items is not List<T> list)
            throw new InvalidOperationException("Expected Items to be of type List<T>");

        if (list.Count == 0 && newItems.IsEmpty)
            return;

        list.Clear();
        if (list.Capacity > TrimCapacityThreshold && newItems.Length < 256)
            list.Capacity = Math.Max(newItems.Length, MinTrimmedCapacity);

        list.AddRange(newItems);

        OnPropertyChanged(CountEventArgs);
        OnPropertyChanged(IndexerEventArgs);
        OnCollectionChanged(ResetEventArgs);
    }
}
