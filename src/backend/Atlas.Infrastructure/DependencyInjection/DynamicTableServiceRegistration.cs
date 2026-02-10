using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Repositories;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Infrastructure.DependencyInjection;

/// <summary>
/// Registers dynamic table services and repositories.
/// </summary>
public static class DynamicTableServiceRegistration
{
    public static IServiceCollection AddDynamicTableInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IDynamicTableRepository, DynamicTableRepository>();
        services.AddScoped<IDynamicFieldRepository, DynamicFieldRepository>();
        services.AddScoped<IDynamicIndexRepository, DynamicIndexRepository>();
        services.AddScoped<IDynamicRecordRepository, DynamicRecordRepository>();
        services.AddScoped<IDynamicTableQueryService, DynamicTableQueryService>();
        services.AddScoped<IDynamicTableCommandService, DynamicTableCommandService>();
        services.AddScoped<IDynamicRecordQueryService, DynamicRecordQueryService>();
        services.AddScoped<IDynamicRecordCommandService, DynamicRecordCommandService>();

        return services;
    }
}
