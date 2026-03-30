using Atlas.Application.System.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.System.Abstractions;

public interface IAppMigrationService
{
    Task<long> CreateTaskAsync(
        TenantId tenantId,
        long userId,
        AppMigrationTaskCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<AppMigrationTaskListItem>> QueryTasksAsync(
        TenantId tenantId,
        PagedRequest request,
        CancellationToken cancellationToken = default);

    Task<AppMigrationTaskDetail?> GetTaskAsync(
        TenantId tenantId,
        long taskId,
        CancellationToken cancellationToken = default);

    Task<AppMigrationPrecheckResult> PrecheckAsync(
        TenantId tenantId,
        long taskId,
        CancellationToken cancellationToken = default);

    Task<AppMigrationActionResult> StartAsync(
        TenantId tenantId,
        long userId,
        long taskId,
        CancellationToken cancellationToken = default);

    Task<AppMigrationTaskProgress?> GetProgressAsync(
        TenantId tenantId,
        long taskId,
        CancellationToken cancellationToken = default);

    Task<AppIntegrityCheckSummary> ValidateIntegrityAsync(
        TenantId tenantId,
        long userId,
        long taskId,
        CancellationToken cancellationToken = default);

    Task<AppMigrationActionResult> CutoverAsync(
        TenantId tenantId,
        long userId,
        long taskId,
        AppMigrationCutoverRequest request,
        CancellationToken cancellationToken = default);

    Task<AppMigrationActionResult> RollbackCutoverAsync(
        TenantId tenantId,
        long userId,
        long taskId,
        CancellationToken cancellationToken = default);

    Task<AppMigrationBindingRepairResult> RepairPrimaryBindingAsync(
        TenantId tenantId,
        long userId,
        AppMigrationBindingRepairRequest request,
        CancellationToken cancellationToken = default);

    Task<AppMigrationActionResult> ResetFailedTaskAsync(
        TenantId tenantId,
        long userId,
        long taskId,
        CancellationToken cancellationToken = default);
}
