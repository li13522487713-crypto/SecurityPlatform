using Atlas.Application.Microflows.DependencyInjection;
using Atlas.Application.Microflows.Infrastructure;
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
        services.AddScoped<IMicroflowRequestContextAccessor, HttpMicroflowRequestContextAccessor>();
        services.AddScoped<MicroflowApiExceptionFilter>();

        return services;
    }
}
