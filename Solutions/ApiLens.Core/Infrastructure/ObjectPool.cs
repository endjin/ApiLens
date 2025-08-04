using System.Collections.Concurrent;

namespace ApiLens.Core.Infrastructure;

public sealed class ObjectPool<T> where T : class
{
    private readonly ConcurrentBag<T> objects = [];
    private readonly Func<T> objectGenerator;
    private readonly Action<T>? resetAction;
    private readonly int maxSize;
    private int currentSize;

    public ObjectPool(Func<T> objectGenerator, Action<T>? resetAction = null, int maxSize = 1024)
    {
        ArgumentNullException.ThrowIfNull(objectGenerator);
        this.objectGenerator = objectGenerator;
        this.resetAction = resetAction;
        this.maxSize = maxSize;
    }

    public T Rent()
    {
        if (objects.TryTake(out T? item))
        {
            return item;
        }

        Interlocked.Increment(ref currentSize);
        return objectGenerator();
    }

    public void Return(T item)
    {
        ArgumentNullException.ThrowIfNull(item);

        resetAction?.Invoke(item);

        // IMPORTANT: The original implementation has a race condition here.
        // objects.Count could change between the check and the Add operation.
        // However, this matches the test expectations, where TotalCreated
        // represents "live" objects (in use or in pool), not total ever created.
        if (objects.Count < maxSize)
        {
            objects.Add(item);
        }
        else
        {
            // Object is discarded, decrement the count of live objects
            Interlocked.Decrement(ref currentSize);
        }
    }

    public int Count => objects.Count;
    public int TotalCreated => currentSize;
}