using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IDatabaseStructureService
{
    Task<IReadOnlyList<DatabaseObjectDto>> GetObjectsAsync(
        TenantId tenantId,
        long databaseId,
        AiDatabaseRecordEnvironment environment,
        string type,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DatabaseColumnDto>> GetTableColumnsAsync(
        TenantId tenantId,
        long databaseId,
        AiDatabaseRecordEnvironment environment,
        string tableName,
        string? schema,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DatabaseColumnDto>> GetViewColumnsAsync(
        TenantId tenantId,
        long databaseId,
        AiDatabaseRecordEnvironment environment,
        string viewName,
        string? schema,
        CancellationToken cancellationToken);

    Task<DdlResponse> GetTableDdlAsync(
        TenantId tenantId,
        long databaseId,
        AiDatabaseRecordEnvironment environment,
        string tableName,
        string? schema,
        CancellationToken cancellationToken);

    Task<DdlResponse> GetViewDdlAsync(
        TenantId tenantId,
        long databaseId,
        AiDatabaseRecordEnvironment environment,
        string viewName,
        string? schema,
        CancellationToken cancellationToken);

    Task<PreviewDataResponse> PreviewTableDataAsync(
        TenantId tenantId,
        long databaseId,
        string tableName,
        PreviewDataRequest request,
        CancellationToken cancellationToken);

    Task<PreviewDataResponse> PreviewViewDataAsync(
        TenantId tenantId,
        long databaseId,
        string viewName,
        PreviewDataRequest request,
        CancellationToken cancellationToken);

    Task<DdlResponse> BuildCreateTableDdlAsync(
        TenantId tenantId,
        long databaseId,
        PreviewCreateTableDdlRequest request,
        CancellationToken cancellationToken);

    Task CreateTableAsync(TenantId tenantId, long databaseId, CreateTableRequest request, CancellationToken cancellationToken);

    Task CreateTableBySqlAsync(TenantId tenantId, long databaseId, CreateTableSqlRequest request, CancellationToken cancellationToken);

    Task<PreviewDataResponse> PreviewViewSqlAsync(
        TenantId tenantId,
        long databaseId,
        PreviewViewSqlRequest request,
        CancellationToken cancellationToken);

    Task CreateViewAsync(TenantId tenantId, long databaseId, CreateViewRequest request, CancellationToken cancellationToken);

    Task DropTableAsync(
        TenantId tenantId,
        long databaseId,
        string tableName,
        DropDatabaseObjectRequest request,
        CancellationToken cancellationToken);

    Task DropViewAsync(
        TenantId tenantId,
        long databaseId,
        string viewName,
        DropDatabaseObjectRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DatabaseObjectDto>> GetProcedureListAsync(
        TenantId tenantId,
        long databaseId,
        AiDatabaseRecordEnvironment environment,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DatabaseObjectDto>> GetTriggerListAsync(
        TenantId tenantId,
        long databaseId,
        AiDatabaseRecordEnvironment environment,
        CancellationToken cancellationToken);
}
