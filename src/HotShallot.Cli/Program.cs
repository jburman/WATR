using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;
using HotShallot.Cli.Services;
using HotShallot.Cli.Commands;
using System.Diagnostics.CodeAnalysis;

var services = new ServiceCollection();
services.AddSingleton<ConfigService>();
services.AddSingleton<TokenStore>();
services.AddSingleton<ApiClient>();

var registrar = new TypeRegistrar(services);
#pragma warning disable IL2026 // Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code
#pragma warning disable IL3050 // Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.
var app = new CommandApp(registrar);
#pragma warning restore IL3050
#pragma warning restore IL2026

app.Configure(config =>
{
    config.SetApplicationName("shallot");
    
    config.AddCommand<LoginCommand>("login");
    config.AddCommand<LogoutCommand>("logout");
    config.AddCommand<ListCommand>("list");
    config.AddCommand<ShowCommand>("show");
    config.AddCommand<AddCommand>("add");
    config.AddCommand<UpdateCommand>("update");
    config.AddCommand<ClaimCommand>("claim");
    config.AddCommand<DoneCommand>("done");
    config.AddCommand<NoteCommand>("note");
    
    config.AddBranch("project", project =>
    {
        project.AddCommand<ProjectSetCommand>("set");
    });
});

return await app.RunAsync(args);

// Simple TypeRegistrar for Spectre.Console
public sealed class TypeRegistrar : ITypeRegistrar
{
    private readonly IServiceCollection _builder;

    public TypeRegistrar(IServiceCollection builder)
    {
        _builder = builder;
    }

    public ITypeResolver Build()
    {
        return new TypeResolver(_builder.BuildServiceProvider());
    }

    public void Register(Type service, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementation)
    {
        _builder.AddSingleton(service, implementation);
    }

    public void RegisterInstance(Type service, object instance)
    {
        _builder.AddSingleton(service, instance);
    }

    public void RegisterLazy(Type service, Func<object> factory)
    {
        _builder.AddSingleton(service, _ => factory());
    }
}

public sealed class TypeResolver : ITypeResolver, IDisposable
{
    private readonly IServiceProvider _provider;

    public TypeResolver(IServiceProvider provider)
    {
        _provider = provider;
    }

    public object? Resolve(Type? type)
    {
        if (type == null)
        {
            return null;
        }

        return _provider.GetService(type);
    }

    public void Dispose()
    {
        if (_provider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
