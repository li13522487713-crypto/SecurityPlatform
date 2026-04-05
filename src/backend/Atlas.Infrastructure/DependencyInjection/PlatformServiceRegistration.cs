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
        services.AddScoped<IAppMemberRepository, AppMemberRepository>();
        services.AddScoped<IAppMemberDepartmentRepository, AppMemberDepartmentRepository>();
        services.AddScoped<IAppMemberPositionRepository, AppMemberPositionRepository>();
        services.AddScoped<IAppRoleRepository, AppRoleRepository>();
        services.AddScoped<IAppUserRoleRepository, AppUserRoleRepository>();
        services.AddScoped<IAppRolePermissionRepository, AppRolePermissionRepository>();
        services.AddScoped<IAppRolePageRepository, AppRolePageRepository>();
        services.AddScoped<IAppPermissionRepository, AppPermissionRepository>();
        services.AddScoped<IAppDepartmentRepository, AppDepartmentRepository>();
        services.AddScoped<IAppPositionRepository, AppPositionRepository>();
        services.AddScoped<IAppProjectRepository, AppProjectRepository>();
        services.AddScoped<IAppProjectUserRepository, AppProjectUserRepository>();
        services.AddScoped<IPlatformQueryService, PlatformQueryService>();
        services.AddScoped<IAppManifestQueryService, AppManifestQueryService>();
        services.AddScoped<IApplicationCatalogQueryService, ApplicationCatalogQueryService>();
        services.AddScoped<IApplicationCatalogCommandService, ApplicationCatalogCommandService>();
        services.AddScoped<ITenantApplicationQueryService, TenantApplicationQueryService>();
        services.AddScoped<IAppManifestCommandService, AppManifestCommandService>();
        services.AddScoped<IAppBootstrapService, AppBootstrapService>();
        services.AddScoped<IAppReleaseCommandService, AppReleaseCommandService>();
        services.AddScoped<IAppPermissionQueryService, AppPermissionQueryService>();
        services.AddScoped<IAppPermissionCommandService, AppPermissionCommandService>();
        services.AddScoped<IResourceCenterQueryService, ResourceCenterQueryService>();
        services.AddScoped<IResourceCenterCommandService, ResourceCenterCommandService>();
        services.AddScoped<IReleaseCenterQueryService, ReleaseCenterQueryService>();
        services.AddScoped<ICozeMappingQueryService, CozeMappingQueryService>();
        services.AddScoped<IDebugLayerQueryService, DebugLayerQueryService>();
        services.AddScoped<IRuntimeContextQueryService, RuntimeContextQueryService>();
        services.AddScoped<IRuntimeExecutionQueryService, RuntimeExecutionQueryService>();
        services.AddScoped<IAppDesignerSnapshotService, AppDesignerSnapshotService>();
        return services;
    }
}
