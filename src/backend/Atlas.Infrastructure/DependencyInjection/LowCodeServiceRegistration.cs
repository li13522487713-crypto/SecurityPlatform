using Atlas.Application.LowCode.Abstractions;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services.LowCode;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Infrastructure.DependencyInjection;

/// <summary>
/// Registers low-code platform services and repositories.
/// </summary>
public static class LowCodeServiceRegistration
{
    public static IServiceCollection AddLowCodeInfrastructure(this IServiceCollection services)
    {
        // Repositories
        services.AddScoped<IFormDefinitionRepository, FormDefinitionRepository>();
        services.AddScoped<ILowCodeAppRepository, LowCodeAppRepository>();
        services.AddScoped<ILowCodePageRepository, LowCodePageRepository>();

        // Query Services
        services.AddScoped<IFormDefinitionQueryService, FormDefinitionQueryService>();
        services.AddScoped<ILowCodeAppQueryService, LowCodeAppQueryService>();

        // Command Services
        services.AddScoped<IFormDefinitionCommandService, FormDefinitionCommandService>();
        services.AddScoped<ILowCodeAppCommandService, LowCodeAppCommandService>();
        services.AddScoped<ILowCodePageCommandService, LowCodePageCommandService>();

        // Process Monitor
        services.AddScoped<IProcessMonitorService, ProcessMonitorService>();

        // Report & Dashboard
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IDashboardService, DashboardService>();

        // Message Center
        services.AddScoped<IMessageService, MessageService>();

        // AI Service
        services.AddScoped<IAiService, AiService>();

        return services;
    }
}
