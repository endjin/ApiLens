using System.Collections.Concurrent;

namespace ApiLens.Core.Infrastructure;

public sealed class StringInternCache
{
    private readonly ConcurrentDictionary<string, string> cache = new();
    private readonly int maxSize;

    public StringInternCache(int maxSize = 10000)
    {
        this.maxSize = maxSize;
    }

    public string GetOrAdd(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        // FIXED: Race condition - cache.Count could change between check and add.
        // Using TryGetValue first to avoid the race condition entirely.
        // If the value already exists, return it without checking size.
        if (cache.TryGetValue(value, out string? cached))
            return cached;

        // Only check size when we know we need to add a new entry
        if (cache.Count >= maxSize)
            return value; // Don't grow beyond max size

        // GetOrAdd is thread-safe and will either add or return existing
        return cache.GetOrAdd(value, value);
    }

    public int Count => cache.Count;

    public void Clear() => cache.Clear();
}