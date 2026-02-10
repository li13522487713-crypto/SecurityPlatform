using Atlas.Application.Visualization.Abstractions;
using Atlas.Application.Workflow.Abstractions;
using Atlas.Infrastructure.Services;
using Atlas.Infrastructure.Services.Visualization;
using Atlas.Infrastructure.Workflow;
using Atlas.WorkflowCore.Abstractions.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Infrastructure.DependencyInjection;

/// <summary>
/// Registers workflow and visualization services.
/// </summary>
public static class WorkflowServiceRegistration
{
    public static IServiceCollection AddWorkflowInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IPersistenceProvider, SqlSugarPersistenceProvider>();
        services.AddScoped<IWorkflowQueryService, WorkflowQueryService>();
        services.AddScoped<IWorkflowCommandService, WorkflowCommandService>();
        services.AddScoped<IVisualizationQueryService, VisualizationQueryService>();

        return services;
    }
}
