using Atlas.Application.DynamicTables;
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
        services.AddScoped<IDynamicRelationRepository, DynamicRelationRepository>();
        services.AddScoped<IFieldPermissionRepository, FieldPermissionRepository>();
        services.AddScoped<IDynamicRecordRepository, DynamicRecordRepository>();
        services.AddScoped<IDynamicSchemaMigrationRepository, DynamicSchemaMigrationRepository>();
        services.AddScoped<IMigrationRecordRepository, MigrationRecordRepository>();
        services.AddScoped<IFieldPermissionResolver, FieldPermissionResolver>();
        services.AddScoped<IDynamicTableQueryService, DynamicTableQueryService>();
        services.AddScoped<IDynamicTableCommandService, DynamicTableCommandService>();
        services.AddScoped<IDynamicRecordQueryService, DynamicRecordQueryService>();
        services.AddScoped<IDynamicRecordCommandService, DynamicRecordCommandService>();
        services.AddScoped<IMigrationService, MigrationService>();
        services.AddScoped<IDynamicFormValidationService, DynamicFormValidationService>();

        return services;
    }
}
