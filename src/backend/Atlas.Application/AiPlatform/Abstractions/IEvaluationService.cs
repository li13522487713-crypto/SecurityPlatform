using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IEvaluationService
{
    Task<long> CreateDatasetAsync(
        TenantId tenantId,
        long userId,
        EvaluationDatasetCreateRequest request,
        CancellationToken cancellationToken);

    Task<PagedResult<EvaluationDatasetDto>> GetDatasetsAsync(
        TenantId tenantId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task<long> CreateCaseAsync(
        TenantId tenantId,
        long datasetId,
        EvaluationCaseCreateRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<EvaluationCaseDto>> GetCasesAsync(
        TenantId tenantId,
        long datasetId,
        CancellationToken cancellationToken);

    Task<long> CreateTaskAsync(
        TenantId tenantId,
        long userId,
        EvaluationTaskCreateRequest request,
        CancellationToken cancellationToken);

    Task<PagedResult<EvaluationTaskDto>> GetTasksAsync(
        TenantId tenantId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken);

    Task<EvaluationTaskDto?> GetTaskAsync(
        TenantId tenantId,
        long taskId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<EvaluationResultDto>> GetTaskResultsAsync(
        TenantId tenantId,
        long taskId,
        CancellationToken cancellationToken);

    Task<EvaluationComparisonResult> CompareTasksAsync(
        TenantId tenantId,
        long leftTaskId,
        long rightTaskId,
        CancellationToken cancellationToken);
}
