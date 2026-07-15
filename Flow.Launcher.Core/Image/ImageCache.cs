using System.Diagnostics.CodeAnalysis;
using System.Windows.Media;
using BitFaster.Caching;
using BitFaster.Caching.Lfu;

namespace Flow.Launcher.Core.Image;

public class ImageCache<Key>(int capacity, IEqualityComparer<Key>? keyComparer = null) where Key : notnull
{
    private readonly ICache<(Key, bool), ImageSource> _cache =
        new ConcurrentLfuBuilder<(Key, bool), ImageSource>()
            .WithKeyComparer(new ImageCacheKeyComparer(keyComparer ?? EqualityComparer<Key>.Default))
            .WithCapacity(capacity)
            .Build();

    private class ImageCacheKeyComparer(IEqualityComparer<Key> keyComparer) : IEqualityComparer<(Key Key, bool IsFullImage)>
    {
        public bool Equals((Key Key, bool IsFullImage) x, (Key Key, bool IsFullImage) y)
        {
            return x.IsFullImage == y.IsFullImage
                && keyComparer.Equals(x.Key, y.Key);
        }

        public int GetHashCode((Key Key, bool IsFullImage) obj)
        {
            return HashCode.Combine(
                keyComparer.GetHashCode(obj.Key),
                obj.IsFullImage
            );
        }
    }

    public ImageSource? this[Key key, bool isFullImage = false]
    {
        get => _cache.TryGet((key, isFullImage), out var value) ? value : null;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _cache.AddOrUpdate((key, isFullImage), value);
        }
    }

    public bool TryGetValue(Key key, bool isFullImage, [NotNullWhen(true)] out ImageSource? image)
    {
        return _cache.TryGet((key, isFullImage), out image);
    }
}
