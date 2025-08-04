namespace ApiLens.Core.Tests.Helpers;

public static class AsyncEnumerable
{
    public static IAsyncEnumerable<T> Empty<T>()
    {
        return EmptyAsyncEnumerable<T>.Instance;
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
}