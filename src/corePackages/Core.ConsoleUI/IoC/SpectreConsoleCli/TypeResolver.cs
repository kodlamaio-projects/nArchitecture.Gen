using Spectre.Console.Cli;

namespace Core.ConsoleUI.IoC.SpectreConsoleCli;

public class TypeResolver : ITypeResolver
{
    private readonly IServiceProvider _provider;

    public TypeResolver(IServiceProvider provider)
    {
        _provider = provider ?? throw new ArgumentNullException(paramName: nameof(provider));
    }

    public object? Resolve(Type? type)
    {
        if (type is null)
            return null;

        return _provider.GetService(type);
    }

    public void Dispose()
    {
        if (_provider is IDisposable disposable)
            disposable.Dispose();
    }
}
