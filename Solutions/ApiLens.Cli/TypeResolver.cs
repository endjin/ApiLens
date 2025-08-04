namespace ApiLens.Cli;

public sealed class TypeResolver : ITypeResolver
{
    private readonly IServiceProvider provider;

    public TypeResolver(IServiceProvider provider)
    {
        this.provider = provider;
    }

    public object? Resolve(Type? type)
    {
        return type == null ? null : provider.GetService(type);
    }
}