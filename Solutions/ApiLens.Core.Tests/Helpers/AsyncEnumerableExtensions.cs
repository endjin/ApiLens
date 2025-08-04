namespace ApiLens.Core.Tests.Helpers;

public static class AsyncEnumerableExtensions
{
    public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source, CancellationToken cancellationToken = default)
    {
        List<T> list = [];
        await foreach (T item in source.WithCancellation(cancellationToken))
        {
            list.Add(item);
        }
        return list;
    }
}