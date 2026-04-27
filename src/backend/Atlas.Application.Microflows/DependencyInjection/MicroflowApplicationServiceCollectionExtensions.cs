using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Application.Microflows.DependencyInjection;

public static class MicroflowApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddAtlasApplicationMicroflows(this IServiceCollection services)
    {
        services.AddSingleton<IMicroflowClock, SystemMicroflowClock>();
        services.AddScoped<IMicroflowResourceQueryService, InMemoryMicroflowResourceQueryService>();
        services.AddScoped<IMicroflowMetadataQueryService, InMemoryMicroflowMetadataQueryService>();
        services.AddScoped<IMicroflowValidationService, SkeletonMicroflowValidationService>();
        services.AddScoped<IMicroflowRuntimeSkeletonService, SkeletonMicroflowRuntimeService>();

        return services;
    }
}
