using System.Collections.Concurrent;

namespace PdfViewer.Rendering;

public class PageCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly int _maxCacheSize;
    private readonly object _lockObject = new();

    public PageCache(int maxCacheSize = 50)
    {
        _maxCacheSize = maxCacheSize;
    }

    public bool TryGet(string key, out byte[] data)
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            entry.LastAccessed = DateTime.UtcNow;
            data = entry.Data;
            return true;
        }

        data = Array.Empty<byte>();
        return false;
    }

    public void Add(string key, byte[] data)
    {
        lock (_lockObject)
        {
            // Evict old entries if cache is full
            if (_cache.Count >= _maxCacheSize)
            {
                EvictOldest();
            }

            _cache[key] = new CacheEntry
            {
                Key = key,
                Data = data,
                LastAccessed = DateTime.UtcNow
            };
        }
    }

    public void Clear()
    {
        _cache.Clear();
    }

    public void Remove(string key)
    {
        _cache.TryRemove(key, out _);
    }

    private void EvictOldest()
    {
        var oldest = _cache.Values
            .OrderBy(e => e.LastAccessed)
            .FirstOrDefault();

        if (oldest != null)
        {
            _cache.TryRemove(oldest.Key, out _);
        }
    }

    public int Count => _cache.Count;

    private class CacheEntry
    {
        public required string Key { get; set; }
        public required byte[] Data { get; set; }
        public DateTime LastAccessed { get; set; }
    }
}
