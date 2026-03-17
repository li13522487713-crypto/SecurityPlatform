using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Repositories;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services.Platform;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Infrastructure.DependencyInjection;

public static class PlatformServiceRegistration
{
    public static IServiceCollection AddPlatformInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IRuntimeRouteRepository, RuntimeRouteRepository>();
        services.AddScoped<IPlatformQueryService, PlatformQueryService>();
        services.AddScoped<IAppManifestQueryService, AppManifestQueryService>();
        services.AddScoped<IApplicationCatalogQueryService, ApplicationCatalogQueryService>();
        services.AddScoped<IAppManifestCommandService, AppManifestCommandService>();
        services.AddScoped<IAppReleaseCommandService, AppReleaseCommandService>();
        services.AddScoped<ITenantAppInstanceQueryService, TenantAppInstanceQueryService>();
        services.AddScoped<IResourceCenterQueryService, ResourceCenterQueryService>();
        services.AddScoped<IReleaseCenterQueryService, ReleaseCenterQueryService>();
        services.AddScoped<IRuntimeContextQueryService, RuntimeContextQueryService>();
        services.AddScoped<IRuntimeExecutionQueryService, RuntimeExecutionQueryService>();
        services.AddScoped<IRuntimeRouteQueryService, RuntimeRouteQueryService>();
        return services;
    }
}
