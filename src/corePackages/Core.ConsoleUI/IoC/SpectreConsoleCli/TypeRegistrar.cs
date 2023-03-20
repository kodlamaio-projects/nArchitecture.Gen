using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace Core.ConsoleUI.IoC.SpectreConsoleCli;

public class TypeRegistrar : ITypeRegistrar
{
    private readonly IServiceCollection _builder;

    public TypeRegistrar(IServiceCollection builder)
    {
        _builder = builder;
    }

    public ITypeResolver Build() => new TypeResolver(provider: _builder.BuildServiceProvider());

    public void Register(Type service, Type implementation)
    {
        _builder.AddSingleton(service, implementation);
    }

    public void RegisterInstance(Type service, object implementation)
    {
        _builder.AddSingleton(service, implementation);
    }

    public void RegisterLazy(Type service, Func<object> func)
    {
        if (func is null)
            throw new ArgumentNullException(paramName: nameof(func));

        _builder.AddSingleton(service, implementationFactory: provider => func());
    }
}
