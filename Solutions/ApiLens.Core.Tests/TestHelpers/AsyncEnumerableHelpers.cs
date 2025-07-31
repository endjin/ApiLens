namespace ApiLens.Core.Tests.Helpers;

public static class AsyncEnumerable
{
    public static IAsyncEnumerable<T> Empty<T>()
    {
        return EmptyAsyncEnumerable<T>.Instance;
    }

    public static async IAsyncEnumerable<T> Create<T>(Func<IAsyncEnumerableWriter<T>, CancellationToken, Task> writeFunc)
    {
        var writer = new AsyncEnumerableWriter<T>();
        await writeFunc(writer, CancellationToken.None);
        foreach (var item in writer.Items)
        {
            yield return item;
        }
    }

    public static async IAsyncEnumerable<T> CreateFromEnumerable<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.Yield();
        }
    }

    private class EmptyAsyncEnumerable<T> : IAsyncEnumerable<T>
    {
        public static readonly EmptyAsyncEnumerable<T> Instance = new();

        public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    public interface IAsyncEnumerableWriter<T>
    {
        Task YieldAsync(T item);
    }

    private class AsyncEnumerableWriter<T> : IAsyncEnumerableWriter<T>
    {
        public List<T> Items { get; } = new();

        public Task YieldAsync(T item)
        {
            Items.Add(item);
            return Task.CompletedTask;
        }
    }
}