using Atlas.Application.Audit.Mappings;
using Atlas.Application.Mappings;
using Atlas.Application.Visualization;
using Atlas.Application.Workflow;
using Atlas.Application.Workflow.Mappings;
using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Atlas.Application;

public static class ServiceCollectionExtensions
{
    private static readonly Assembly[] DefaultMappingAssemblies =
    [
        typeof(IdentityMappingProfile).Assembly,
        typeof(AuditMappingProfile).Assembly,
        typeof(WorkflowMappingProfile).Assembly
    ];

    public static IServiceCollection AddAtlasApplicationShared(
        this IServiceCollection services,
        params Assembly[] additionalMappingAssemblies)
    {
        var mappingAssemblies = additionalMappingAssemblies.Length == 0
            ? DefaultMappingAssemblies
            : DefaultMappingAssemblies
                .Concat(additionalMappingAssemblies)
                .Distinct()
                .ToArray();

        services.AddAutoMapper(mappingAssemblies);

        return services;
    }

    public static IServiceCollection AddAtlasApplicationPlatform(this IServiceCollection services)
    {
        return services;
    }

    public static IServiceCollection AddAtlasApplicationRuntime(this IServiceCollection services)
    {
        services.AddWorkflowApplication();
        services.AddVisualizationApplication();

        return services;
    }

    public static IServiceCollection AddAtlasApplication(
        this IServiceCollection services,
        params Assembly[] additionalMappingAssemblies)
    {
        return services
            .AddAtlasApplicationShared(additionalMappingAssemblies)
            .AddAtlasApplicationPlatform()
            .AddAtlasApplicationRuntime();
    }
}
