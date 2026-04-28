using Atlas.Application.Microflows.DependencyInjection;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Runtime.Actions.Http;
using Atlas.Application.Microflows.Runtime.Security;
using Atlas.AppHost.Microflows.Infrastructure;

namespace Atlas.AppHost.Microflows.DependencyInjection;

public static class MicroflowBackendServiceCollectionExtensions
{
    public static IServiceCollection AddMicroflowBackend(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAtlasApplicationMicroflows();
        var entityAccessOptions = new MicroflowEntityAccessOptions();
        configuration.GetSection("Microflow:Runtime").Bind(entityAccessOptions);
        services.AddSingleton(entityAccessOptions);
        var restOptions = new MicroflowRestExecutionOptions();
        configuration.GetSection("Microflow:Runtime:Rest").Bind(restOptions);
        services.AddSingleton(restOptions);
        services.AddScoped<IMicroflowRequestContextAccessor, HttpMicroflowRequestContextAccessor>();
        services.AddScoped<MicroflowApiExceptionFilter>();
        services.AddScoped<MicroflowProductionGuardFilter>();
        services.AddScoped<MicroflowWorkspaceOwnershipFilter>();

        return services;
    }
}
