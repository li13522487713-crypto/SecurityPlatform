using Atlas.Application.DynamicTables;
using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Repositories;
using Atlas.Application.DynamicViews.Abstractions;
using Atlas.Application.DynamicViews.Repositories;
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
        services.AddScoped<IDynamicViewRepository, DynamicViewRepository>();
        services.AddScoped<IDynamicViewVersionRepository, DynamicViewVersionRepository>();
        services.AddScoped<IDynamicSchemaMigrationRepository, DynamicSchemaMigrationRepository>();
        services.AddScoped<IMigrationRecordRepository, MigrationRecordRepository>();
        services.AddScoped<IDynamicTableApprovalBindingRepository, DynamicTableApprovalBindingRepository>();
        services.AddScoped<ISchemaDraftRepository, SchemaDraftRepository>();
        services.AddScoped<ISchemaChangeTaskRepository, SchemaChangeTaskRepository>();
        services.AddScoped<ISchemaPublishSnapshotRepository, SchemaPublishSnapshotRepository>();
        services.AddScoped<ISchemaDraftService, SchemaDraftService>();
        services.AddScoped<ISchemaChangeTaskService, SchemaChangeTaskService>();
        services.AddScoped<ISchemaPublishSnapshotQueryService, SchemaPublishSnapshotQueryService>();
        services.AddScoped<ISchemaPublishSnapshotCommandService, SchemaPublishSnapshotCommandService>();
        services.AddScoped<IAuditFieldTemplateService, AuditFieldTemplateService>();
        services.AddScoped<ISchemaCompatibilityChecker, SchemaCompatibilityChecker>();
        services.AddScoped<IDdlPreviewService, DdlPreviewService>();
        services.AddScoped<IExpandMigrateContractService, ExpandMigrateContractService>();
        services.AddScoped<ISchemaImpactAnalysisService, SchemaImpactAnalysisService>();
        services.AddScoped<IDependencyGraphService, DependencyGraphService>();
        services.AddScoped<IComputedFieldBindingService, ComputedFieldBindingService>();
        services.AddScoped<IDynamicImpactAnalysisService, DynamicImpactAnalysisService>();
        services.AddScoped<IFieldPermissionResolver, FieldPermissionResolver>();
        services.AddScoped<IDynamicTableQueryService, DynamicTableQueryService>();
        services.AddScoped<IDynamicTableCommandService, DynamicTableCommandService>();
        services.AddScoped<IDynamicRecordImportService, DynamicRecordImportService>();
        services.AddScoped<IDynamicDeleteCheckService, DynamicDeleteCheckService>();
        services.AddScoped<IDynamicViewQueryService, DynamicViewQueryService>();
        services.AddScoped<IDynamicViewCommandService, DynamicViewCommandService>();
        services.AddScoped<IDynamicViewP2Service, DynamicViewP2Service>();
        services.AddScoped<IDynamicTransformJobService, DynamicTransformJobService>();
        services.AddScoped<DynamicTransformJobExecutor>();
        services.AddScoped<DynamicViewCompiler>();
        services.AddScoped<DynamicViewRuntime>();
        services.AddScoped<DynamicViewVersionService>();
        services.AddScoped<IDynamicRecordQueryService, DynamicRecordQueryService>();
        services.AddScoped<IDynamicRecordCommandService, DynamicRecordCommandService>();
        services.AddScoped<IMigrationService, MigrationService>();
        services.AddScoped<IDynamicFormValidationService, DynamicFormValidationService>();
        services.AddScoped<IRollupCalculationService, RollupCalculationService>();
        services.AddSingleton<RollupBackgroundWorker>();
        services.AddHostedService(sp => sp.GetRequiredService<RollupBackgroundWorker>());

        return services;
    }
}

