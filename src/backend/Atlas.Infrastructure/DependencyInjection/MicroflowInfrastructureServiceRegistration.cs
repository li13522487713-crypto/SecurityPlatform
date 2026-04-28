using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Repositories;
using Atlas.Infrastructure.Repositories.Microflows;
using Atlas.Infrastructure.Services.Microflows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Infrastructure.DependencyInjection;

public static class MicroflowInfrastructureServiceRegistration
{
    public static IServiceCollection AddMicroflowInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IMicroflowResourceRepository, MicroflowResourceRepository>();
        services.AddScoped<IMicroflowSchemaSnapshotRepository, MicroflowSchemaSnapshotRepository>();
        services.AddScoped<IMicroflowVersionRepository, MicroflowVersionRepository>();
        services.AddScoped<IMicroflowPublishSnapshotRepository, MicroflowPublishSnapshotRepository>();
        services.AddScoped<IMicroflowReferenceRepository, MicroflowReferenceRepository>();
        services.AddScoped<IMicroflowRunRepository, MicroflowRunRepository>();
        services.AddScoped<IMicroflowMetadataCacheRepository, MicroflowMetadataCacheRepository>();
        services.AddScoped<IMicroflowStorageTransaction, MicroflowStorageTransaction>();

        services.AddScoped<IMicroflowResourceQueryService, MicroflowDbResourceQueryService>();
        services.AddScoped<IMicroflowMetadataQueryService, MicroflowDbMetadataQueryService>();
        services.AddScoped<IMicroflowStorageDiagnosticsService, MicroflowStorageDiagnosticsService>();
        services.AddScoped<IMicroflowAppAssetService, MicroflowAppAssetService>();
        services.AddHostedService<MicroflowSeedDataHostedService>();
        services.AddHostedService<MicroflowMetadataSeedHostedService>();

        return services;
    }
}
