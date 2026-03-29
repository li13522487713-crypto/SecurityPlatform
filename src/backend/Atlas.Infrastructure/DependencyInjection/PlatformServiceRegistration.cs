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
        services.AddScoped<IAppRoleRepository, AppRoleRepository>();
        services.AddScoped<IAppUserRoleRepository, AppUserRoleRepository>();
        services.AddScoped<IAppRolePermissionRepository, AppRolePermissionRepository>();
        services.AddScoped<IAppRolePageRepository, AppRolePageRepository>();
        services.AddScoped<IAppPermissionRepository, AppPermissionRepository>();
        services.AddScoped<IAppDepartmentRepository, AppDepartmentRepository>();
        services.AddScoped<IAppPositionRepository, AppPositionRepository>();
        services.AddScoped<IAppProjectRepository, AppProjectRepository>();
        services.AddScoped<IPlatformQueryService, PlatformQueryService>();
        services.AddScoped<IAppManifestQueryService, AppManifestQueryService>();
        services.AddScoped<IApplicationCatalogQueryService, ApplicationCatalogQueryService>();
        services.AddScoped<IApplicationCatalogCommandService, ApplicationCatalogCommandService>();
        services.AddScoped<ITenantApplicationQueryService, TenantApplicationQueryService>();
        services.AddScoped<IAppManifestCommandService, AppManifestCommandService>();
        services.AddScoped<IAppReleaseCommandService, AppReleaseCommandService>();
        services.AddScoped<ITenantAppInstanceQueryService, TenantAppInstanceQueryService>();
        services.AddScoped<ITenantAppInstanceCommandService, TenantAppInstanceCommandService>();
        services.AddScoped<ITenantAppMemberQueryService, TenantAppMemberQueryService>();
        services.AddScoped<ITenantAppMemberCommandService, TenantAppMemberCommandService>();
        services.AddScoped<ITenantAppRoleQueryService, TenantAppRoleQueryService>();
        services.AddScoped<ITenantAppRoleCommandService, TenantAppRoleCommandService>();
        services.AddScoped<IAppOrgQueryService, AppOrgQueryService>();
        services.AddScoped<IAppOrgCommandService, AppOrgCommandService>();
        services.AddScoped<IAppOrganizationQueryService, AppOrganizationQueryService>();
        services.AddScoped<IAppOrganizationCommandService, AppOrganizationCommandService>();
        services.AddScoped<IAppRoleAssignmentQueryService, AppRoleAssignmentQueryService>();
        services.AddScoped<IAppRoleAssignmentCommandService, AppRoleAssignmentCommandService>();
        services.AddScoped<IAppPermissionQueryService, AppPermissionQueryService>();
        services.AddScoped<IAppPermissionCommandService, AppPermissionCommandService>();
        services.AddScoped<IResourceCenterQueryService, ResourceCenterQueryService>();
        services.AddScoped<IResourceCenterCommandService, ResourceCenterCommandService>();
        services.AddScoped<IReleaseCenterQueryService, ReleaseCenterQueryService>();
        services.AddScoped<ICozeMappingQueryService, CozeMappingQueryService>();
        services.AddScoped<IDebugLayerQueryService, DebugLayerQueryService>();
        services.AddScoped<IRuntimeContextQueryService, RuntimeContextQueryService>();
        services.AddScoped<IRuntimeExecutionQueryService, RuntimeExecutionQueryService>();
        services.AddScoped<IRuntimeExecutionCommandService, RuntimeExecutionCommandService>();
        services.AddScoped<IRuntimeRouteQueryService, RuntimeRouteQueryService>();
        services.AddScoped<IAppDesignerSnapshotService, AppDesignerSnapshotService>();
        return services;
    }
}
