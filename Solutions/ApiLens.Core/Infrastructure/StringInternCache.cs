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

        if (cache.Count >= maxSize)
            return value; // Don't grow beyond max size

        return cache.GetOrAdd(value, value);
    }

    public int Count => cache.Count;

    public void Clear() => cache.Clear();
}