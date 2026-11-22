using System.Collections.Specialized;
using Flow.Launcher.Helper;

namespace Flow.Launcher.Tests.Helper;

public class BulkObservableCollectionTests
{
    [Fact]
    public void ReplaceAll_ArrayListAndDeferredEnumerable_FiresSingleResetNotification()
    {
        BulkObservableCollection<int> collection = [1, 2, 3];
        List<NotifyCollectionChangedAction> actions = [];
        List<string?> changedProperties = [];

        collection.CollectionChanged += (_, e) => actions.Add(e.Action);
        ((System.ComponentModel.INotifyPropertyChanged)collection).PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        collection.ReplaceAll([10, 20]);
        Assert.Equal([10, 20], collection);
        Assert.Single(actions);
        Assert.Equal(NotifyCollectionChangedAction.Reset, actions[0]);
        Assert.Equal(["Count", "Item[]"], changedProperties);

        actions.Clear();
        collection.ReplaceAll(new List<int> { 30, 40, 50 });
        Assert.Equal([30, 40, 50], collection);
        Assert.Single(actions);

        actions.Clear();
        collection.ReplaceAll(collection.Select(x => x * 2));
        Assert.Equal([60, 80, 100], collection);
        Assert.Single(actions);
    }

    [Fact]
    public void ReplaceAll_SelfReferential_PreservesItemsBeforeClearing()
    {
        BulkObservableCollection<string> collection = ["a", "b", "c"];
        collection.ReplaceAll(collection);

        Assert.Equal(["a", "b", "c"], collection);
    }

    [Fact]
    public void Clear_And_EmptyReplaceAll_AreNoOpsWhenAlreadyEmpty()
    {
        BulkObservableCollection<int> collection = [];
        int notificationCount = 0;
        collection.CollectionChanged += (_, _) => notificationCount++;

        collection.Clear();
        collection.ReplaceAll(ReadOnlySpan<int>.Empty);
        collection.ReplaceAll([]);

        Assert.Equal(0, notificationCount);
    }

    [Fact]
    public void ReplaceAll_ReadOnlySpan_UpdatesAndTrimsExcessCapacity()
    {
        BulkObservableCollection<int> collection = [];
        int[] largeBatch = [.. Enumerable.Range(0, 1500)];

        collection.ReplaceAll(largeBatch.AsSpan());
        Assert.Equal(1500, collection.Count);

        int[] smallBatch = [1, 2, 3];
        collection.ReplaceAll(smallBatch.AsSpan());
        Assert.Equal([1, 2, 3], collection);

        collection.Clear();
        Assert.Empty(collection);
    }
}
