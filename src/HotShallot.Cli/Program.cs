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

return await CreateAndRunApp(registrar, args);

[UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "Spectre.Console.Cli usage is AOT-aware where possible")]
[UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode", Justification = "Spectre.Console.Cli uses dynamic code generation")]
static async Task<int> CreateAndRunApp(ITypeRegistrar registrar, string[] args)
{
    var app = new CommandApp(registrar);

    app.Configure(config =>
    {
        config.SetApplicationName("shallot");
        
            config.AddCommand<LoginCommand>("login");
        
            config.AddCommand<LogoutCommand>("logout");
        
            config.AddCommand<StatusCommand>("status");
        
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
        
                project.AddCommand<ProjectListCommand>("list");
        
                project.AddCommand<ProjectCreateCommand>("create");
        
            });    });

    return await app.RunAsync(args);
}

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

    [UnconditionalSuppressMessage("Trimming", "IL2067:TargetParameterArgumentDoesNotSatisfyRequirement", Justification = "Spectre.Console.Cli guarantees types are valid for DI")]
    public void Register(Type service, Type implementation)
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

        var service = _provider.GetService(type);
        if (service != null)
        {
            return service;
        }

        // Fallback: Try to create concrete types (like Settings) that aren't registered
        if (!type.IsInterface && !type.IsAbstract)
        {
            try
            {
                #pragma warning disable IL2067
                return CreateInstance(type);
                #pragma warning restore IL2067
            }
            catch
            {
                // Ignore and return null if creation fails
            }
        }

        return null;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2067:TargetParameterArgumentDoesNotSatisfyRequirement", Justification = "Spectre.Console.Cli settings types are typically simple POCOs or we accept the risk")]
    private object CreateInstance([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type)
    {
        return ActivatorUtilities.CreateInstance(_provider, type);
    }

    public void Dispose()
    {
        if (_provider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
