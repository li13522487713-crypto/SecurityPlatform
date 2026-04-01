using Atlas.Application.DynamicTables.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.DynamicTables.Abstractions;

public interface IDynamicTableCommandService
{
    Task<long> CreateAsync(
        TenantId tenantId,
        long userId,
        DynamicTableCreateRequest request,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        DynamicTableUpdateRequest request,
        CancellationToken cancellationToken);

    Task AlterAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        DynamicTableAlterRequest request,
        CancellationToken cancellationToken);

    Task<DynamicTableAlterPreviewResponse> PreviewAlterAsync(
        TenantId tenantId,
        string tableKey,
        DynamicTableAlterRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        CancellationToken cancellationToken);

    Task SetRelationsAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        DynamicRelationUpsertRequest request,
        CancellationToken cancellationToken);

    Task SetFieldPermissionsAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        DynamicFieldPermissionUpsertRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// 绑定/解绑审批流
    /// </summary>
    Task BindApprovalFlowAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        DynamicTableApprovalBindingRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// 从动态表记录发起审批
    /// </summary>
    Task<DynamicTableApprovalSubmitResponse> SubmitApprovalAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        long recordId,
        CancellationToken cancellationToken);

    Task RollbackMigrationAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        long migrationId,
        CancellationToken cancellationToken);

    Task ArchiveAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        CancellationToken cancellationToken);

    Task RestoreAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        CancellationToken cancellationToken);

    Task UpdateApprovalBindingAsync(
        TenantId tenantId,
        long userId,
        string tableKey,
        DynamicTableApprovalBindingUpdateRequest request,
        CancellationToken cancellationToken);
}
