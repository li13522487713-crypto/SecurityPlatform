using System.Text.Json;
using Atlas.Application.DynamicTables.Abstractions;
using Atlas.Application.DynamicTables.Models;
using Atlas.Application.DynamicTables.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.DynamicTables.Entities;
using Atlas.Domain.DynamicTables.Enums;

namespace Atlas.Infrastructure.Services;

public sealed class SchemaChangeTaskService : ISchemaChangeTaskService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ISchemaChangeTaskRepository _taskRepository;
    private readonly ISchemaDraftRepository _draftRepository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public SchemaChangeTaskService(
        ISchemaChangeTaskRepository taskRepository,
        ISchemaDraftRepository draftRepository,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _taskRepository = taskRepository;
        _draftRepository = draftRepository;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public async Task<IReadOnlyList<SchemaChangeTaskListItem>> ListByAppAsync(
        TenantId tenantId,
        long appInstanceId,
        CancellationToken cancellationToken)
    {
        var tasks = await _taskRepository.ListByAppInstanceAsync(tenantId, appInstanceId, cancellationToken);
        return tasks.Select(ToListItem).ToArray();
    }

    public async Task<SchemaChangeTaskListItem?> GetByIdAsync(
        TenantId tenantId,
        long taskId,
        CancellationToken cancellationToken)
    {
        var task = await _taskRepository.FindByIdAsync(tenantId, taskId, cancellationToken);
        return task is null ? null : ToListItem(task);
    }

    public async Task<long> CreateAndExecuteAsync(
        TenantId tenantId,
        long userId,
        SchemaChangeTaskCreateRequest request,
        CancellationToken cancellationToken)
    {
        // 批量获取草稿（避免循环内查 DB）
        var pendingDrafts = await _draftRepository.ListPendingByAppAsync(tenantId, request.AppInstanceId, cancellationToken);
        var targetDrafts = request.DraftIds.Count > 0
            ? pendingDrafts.Where(d => request.DraftIds.Contains(d.Id)).ToList()
            : pendingDrafts.ToList();

        if (targetDrafts.Count == 0)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "NoPendingDraftsToPublish");
        }

        // 判断高风险：任意草稿为 High 则整任务为高风险
        var isHighRisk = targetDrafts.Any(d => d.RiskLevel == SchemaDraftRiskLevel.High);
        var draftIdsJson = JsonSerializer.Serialize(targetDrafts.Select(d => d.Id).ToArray(), JsonOptions);

        var id = _idGeneratorAccessor.NextId();
        var task = new SchemaChangeTask(
            tenantId,
            request.AppInstanceId,
            draftIdsJson,
            isHighRisk,
            userId,
            id,
            DateTimeOffset.UtcNow);

        await _taskRepository.AddAsync(task, cancellationToken);

        // 高风险任务需等待审批
        if (isHighRisk)
        {
            task.StartValidating();
            task.MarkWaitingApproval();
            await _taskRepository.UpdateAsync(task, cancellationToken);
        }
        else
        {
            // 直接执行
            task.StartValidating();
            task.SetValidationResult(
                JsonSerializer.Serialize(new { ok = true }, JsonOptions),
                null);
            task.StartApplying();

            // 标记草稿为已发布
            foreach (var draft in targetDrafts)
            {
                draft.MarkPublished();
            }

            await _draftRepository.UpdateRangeAsync(targetDrafts, cancellationToken);
            task.MarkApplied(DateTimeOffset.UtcNow);
            await _taskRepository.UpdateAsync(task, cancellationToken);
        }

        return id;
    }

    public async Task CancelAsync(
        TenantId tenantId,
        long taskId,
        CancellationToken cancellationToken)
    {
        var task = await _taskRepository.FindByIdAsync(tenantId, taskId, cancellationToken);
        if (task is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "SchemaChangeTaskNotFound");
        }

        if (task.CurrentState == SchemaChangeTaskStatus.Applied
            || task.CurrentState == SchemaChangeTaskStatus.RolledBack
            || task.CurrentState == SchemaChangeTaskStatus.Cancelled)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "SchemaChangeTaskCannotBeCancelled");
        }

        task.Cancel(DateTimeOffset.UtcNow);
        await _taskRepository.UpdateAsync(task, cancellationToken);
    }

    private static SchemaChangeTaskListItem ToListItem(SchemaChangeTask task)
    {
        return new SchemaChangeTaskListItem(
            task.Id,
            task.AppInstanceId,
            task.CurrentState.ToString(),
            task.IsHighRisk,
            task.ValidationResult,
            task.AffectedResourcesSummary,
            task.ErrorMessage,
            task.RollbackInfo,
            task.Operator,
            task.StartedAt,
            task.EndedAt);
    }
}
