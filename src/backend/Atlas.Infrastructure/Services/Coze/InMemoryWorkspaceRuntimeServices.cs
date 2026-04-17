using Atlas.Application.Coze.Abstractions;
using Atlas.Application.Coze.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Infrastructure.Services.Coze;

// Coze PRD Phase III - M4.4 后：InMemoryWorkspaceTaskService 已被持久化版本
// WorkspaceTaskService 取代（基于 EvaluationTask）。该 in-memory 实现历史代码已删除。

/// <summary>
/// 效果评测（PRD 02-7.5）。第一阶段：无评测记录。
/// 第二阶段直接对接 EvaluationDataset / EvaluationTask / EvaluationResult 现有 Domain。
/// </summary>
public sealed class InMemoryWorkspaceEvaluationService : IWorkspaceEvaluationService
{
    public Task<PagedResult<EvaluationItemDto>> ListAsync(
        TenantId tenantId,
        string workspaceId,
        string? keyword,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken)
    {
        var pageIndex = Math.Max(1, pagedRequest.PageIndex);
        var pageSize = Math.Clamp(pagedRequest.PageSize, 1, 100);
        return Task.FromResult(new PagedResult<EvaluationItemDto>(
            Array.Empty<EvaluationItemDto>(),
            0,
            pageIndex,
            pageSize));
    }

    public Task<EvaluationDetailDto?> GetAsync(
        TenantId tenantId,
        string workspaceId,
        string evaluationId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult<EvaluationDetailDto?>(null);
    }
}

// Coze PRD Phase III - M4.3 后：InMemoryWorkspaceTestsetService 已被持久化版本
// WorkspaceTestsetService 取代（基于 EvaluationDataset / EvaluationCase）。
// 该 in-memory 实现历史代码已删除，避免误注册造成数据漂移。
