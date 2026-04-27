using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Atlas.Application.Microflows.DependencyInjection;

public static class MicroflowApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddAtlasApplicationMicroflows(this IServiceCollection services)
    {
        services.TryAddSingleton<IMicroflowClock, SystemMicroflowClock>();
        services.TryAddScoped<IMicroflowSchemaReader, MicroflowSchemaReader>();
        services.TryAddScoped<IMicroflowResourceService, MicroflowResourceService>();
        services.TryAddScoped<IMicroflowMetadataService, MicroflowMetadataService>();
        services.TryAddScoped<IMicroflowResourceQueryService, InMemoryMicroflowResourceQueryService>();
        services.TryAddScoped<IMicroflowMetadataQueryService, InMemoryMicroflowMetadataQueryService>();
        services.TryAddScoped<IMicroflowValidationService, MicroflowValidationService>();
        services.TryAddScoped<IMicroflowReferenceIndexer, MicroflowReferenceIndexer>();
        services.TryAddScoped<IMicroflowReferenceService, MicroflowReferenceService>();
        services.TryAddScoped<IMicroflowMockRuntimeRunner, MicroflowMockRuntimeRunner>();
        services.TryAddScoped<IMicroflowTestRunService, MicroflowTestRunService>();
        services.TryAddScoped<IMicroflowVersionDiffService, MicroflowVersionDiffService>();
        services.TryAddScoped<IMicroflowPublishImpactService, MicroflowPublishImpactService>();
        services.TryAddScoped<IMicroflowPublishService, MicroflowPublishService>();
        services.TryAddScoped<IMicroflowVersionService, MicroflowVersionService>();

        return services;
    }
}
