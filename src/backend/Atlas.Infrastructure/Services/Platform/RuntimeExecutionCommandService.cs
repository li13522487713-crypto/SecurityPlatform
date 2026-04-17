using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.Identity;
using Atlas.Application.LowCode.Models;
using Atlas.Application.Options;
using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Domain.Audit.Entities;
using Atlas.Domain.Identity.Entities;
using Atlas.Domain.LowCode.Entities;
using Atlas.Domain.LowCode.Enums;
using Atlas.Domain.Platform.Entities;
using Atlas.Domain.System.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SqlSugar;
using System.Text.Json;
using TenantAppDataSourceBindingDto = Atlas.Application.Platform.Models.TenantAppDataSourceBinding;
using TenantAppDataSourceBindingEntity = Atlas.Domain.System.Entities.TenantAppDataSourceBinding;

namespace Atlas.Infrastructure.Services.Platform;


public sealed class RuntimeExecutionCommandService : IRuntimeExecutionCommandService
{
    private readonly ISqlSugarClient _mainDb;
    private readonly Atlas.Infrastructure.Services.IAppDbScopeFactory _appDbScopeFactory;
    private readonly IDagWorkflowExecutionService _workflowExecutionService;

    public RuntimeExecutionCommandService(
        ISqlSugarClient db,
        Atlas.Infrastructure.Services.IAppDbScopeFactory appDbScopeFactory,
        IDagWorkflowExecutionService workflowExecutionService)
    {
        _mainDb = db;
        _appDbScopeFactory = appDbScopeFactory;
        _workflowExecutionService = workflowExecutionService;
    }

    public RuntimeExecutionCommandService(
        ISqlSugarClient db,
        IDagWorkflowExecutionService workflowExecutionService)
        : this(db, new Atlas.Infrastructure.Services.MainOnlyAppDbScopeFactory(db), workflowExecutionService)
    {
    }

    public async Task<RuntimeExecutionOperationResult> CancelAsync(
        TenantId tenantId,
        long operatorUserId,
        long executionId,
        CancellationToken cancellationToken = default)
    {
        var execution = await GetExecutionAsync(tenantId, executionId, cancellationToken);
        if (execution.Status is ExecutionStatus.Completed or ExecutionStatus.Failed or ExecutionStatus.Cancelled)
        {
            return new RuntimeExecutionOperationResult(
                "cancel",
                executionId.ToString(),
                execution.Status.ToString(),
                "当前执行状态不支持取消。",
                null);
        }

        await _workflowExecutionService.CancelAsync(tenantId, executionId, cancellationToken);
        await WriteAuditAsync(tenantId, operatorUserId, "runtime.execution.cancel", $"RuntimeExecution:{executionId}", cancellationToken);
        return new RuntimeExecutionOperationResult("cancel", executionId.ToString(), ExecutionStatus.Cancelled.ToString(), "执行已取消。", null);
    }

    public async Task<RuntimeExecutionOperationResult> RetryAsync(
        TenantId tenantId,
        long operatorUserId,
        long executionId,
        CancellationToken cancellationToken = default)
    {
        var execution = await GetExecutionAsync(tenantId, executionId, cancellationToken);
        if (execution.Status is not (ExecutionStatus.Failed or ExecutionStatus.Cancelled or ExecutionStatus.Interrupted))
        {
            return new RuntimeExecutionOperationResult(
                "retry",
                executionId.ToString(),
                execution.Status.ToString(),
                "仅失败/取消/中断状态支持重试。",
                null);
        }

        var runResult = await _workflowExecutionService.AsyncRunAsync(
            tenantId,
            execution.WorkflowId,
            operatorUserId,
            new DagWorkflowRunRequest(execution.InputsJson),
            cancellationToken);
        await WriteAuditAsync(
            tenantId,
            operatorUserId,
            "runtime.execution.retry",
            $"RuntimeExecution:{executionId}->{runResult.ExecutionId}",
            cancellationToken);

        return new RuntimeExecutionOperationResult(
            "retry",
            executionId.ToString(),
            runResult.Status?.ToString() ?? ExecutionStatus.Pending.ToString(),
            "已发起重试执行。",
            runResult.ExecutionId);
    }

    public async Task<RuntimeExecutionOperationResult> ResumeAsync(
        TenantId tenantId,
        long operatorUserId,
        long executionId,
        CancellationToken cancellationToken = default)
    {
        var execution = await GetExecutionAsync(tenantId, executionId, cancellationToken);
        if (execution.Status != ExecutionStatus.Interrupted)
        {
            return new RuntimeExecutionOperationResult(
                "resume",
                executionId.ToString(),
                execution.Status.ToString(),
                "仅中断状态支持恢复。",
                null);
        }

        await _workflowExecutionService.ResumeAsync(tenantId, executionId, cancellationToken);
        await WriteAuditAsync(tenantId, operatorUserId, "runtime.execution.resume", $"RuntimeExecution:{executionId}", cancellationToken);
        return new RuntimeExecutionOperationResult("resume", executionId.ToString(), ExecutionStatus.Running.ToString(), "执行已恢复。", null);
    }

    public async Task<RuntimeExecutionOperationResult> DebugAsync(
        TenantId tenantId,
        long operatorUserId,
        long executionId,
        RuntimeExecutionDebugRequest request,
        CancellationToken cancellationToken = default)
    {
        var execution = await GetExecutionAsync(tenantId, executionId, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.NodeKey))
        {
            throw new InvalidOperationException("NodeKey 不能为空。");
        }

        var debugRequest = new DagWorkflowNodeDebugRequest(request.NodeKey.Trim(), request.InputsJson ?? execution.InputsJson);
        var debugResult = await _workflowExecutionService.DebugNodeAsync(
            tenantId,
            execution.WorkflowId,
            operatorUserId,
            debugRequest,
            cancellationToken);
        await WriteAuditAsync(
            tenantId,
            operatorUserId,
            "runtime.execution.debug",
            $"RuntimeExecution:{executionId}->{debugResult.ExecutionId}",
            cancellationToken);

        return new RuntimeExecutionOperationResult(
            "debug",
            executionId.ToString(),
            debugResult.Status?.ToString() ?? ExecutionStatus.Pending.ToString(),
            "单节点调试执行已完成。",
            debugResult.ExecutionId);
    }

    public async Task<RuntimeExecutionTimeoutDiagnosis?> GetTimeoutDiagnosisAsync(
        TenantId tenantId,
        long executionId,
        CancellationToken cancellationToken = default)
    {
        var execution = await FindExecutionAcrossDbsAsync(tenantId, executionId, cancellationToken);
        if (execution is null)
        {
            return null;
        }

        var completedAt = execution.CompletedAt;
        var elapsed = (completedAt ?? DateTime.UtcNow) - execution.StartedAt;
        var elapsedSeconds = Math.Max(0, elapsed.TotalSeconds);
        var timeoutRisk = execution.Status == ExecutionStatus.Running && elapsedSeconds >= 300;
        var diagnosis = timeoutRisk
            ? "执行运行时间超过 5 分钟，存在超时风险。"
            : execution.Status switch
            {
                ExecutionStatus.Failed => $"执行失败：{execution.ErrorMessage ?? "未记录异常信息"}",
                ExecutionStatus.Interrupted => $"执行中断：{execution.InterruptType}",
                ExecutionStatus.Cancelled => "执行已取消。",
                _ => "执行状态正常。"
            };

        var suggestions = new List<string>();
        if (timeoutRisk)
        {
            suggestions.Add("检查外部依赖（数据库/API）响应时间。");
            suggestions.Add("考虑对执行进行 cancel 后 retry，或拆分长耗时节点。");
        }

        if (execution.Status == ExecutionStatus.Failed)
        {
            suggestions.Add("查看 ErrorMessage 并定位失败节点。");
            suggestions.Add("修复后执行 retry。");
        }

        if (execution.Status == ExecutionStatus.Interrupted)
        {
            suggestions.Add("确认中断原因并执行 resume。");
        }

        if (suggestions.Count == 0)
        {
            suggestions.Add("当前无需额外处理。");
        }

        return new RuntimeExecutionTimeoutDiagnosis(
            execution.Id.ToString(),
            execution.Status.ToString(),
            execution.StartedAt.ToString("O"),
            completedAt?.ToString("O"),
            elapsedSeconds,
            timeoutRisk,
            diagnosis,
            suggestions);
    }

    private async Task<WorkflowExecution> GetExecutionAsync(
        TenantId tenantId,
        long executionId,
        CancellationToken cancellationToken)
    {
        return await FindExecutionAcrossDbsAsync(tenantId, executionId, cancellationToken)
            ?? throw new InvalidOperationException("执行实例不存在。");
    }

    private async Task WriteAuditAsync(
        TenantId tenantId,
        long operatorUserId,
        string action,
        string target,
        CancellationToken cancellationToken)
    {
        var audit = new AuditRecord(
            tenantId,
            operatorUserId.ToString(),
            action,
            "Success",
            target,
            null,
            null);
        await _mainDb.Insertable(audit).ExecuteCommandAsync(cancellationToken);
    }

    private async Task<WorkflowExecution?> FindExecutionAcrossDbsAsync(
        TenantId tenantId,
        long executionId,
        CancellationToken cancellationToken)
    {
        var execution = await _mainDb.Queryable<WorkflowExecution>()
            .FirstAsync(item => item.TenantIdValue == tenantId.Value && item.Id == executionId, cancellationToken);
        if (execution is not null)
        {
            return execution;
        }

        var appIds = await _mainDb.Queryable<LowCodeApp>()
            .Where(item => item.TenantIdValue == tenantId.Value)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        foreach (var appId in appIds)
        {
            var appDb = await _appDbScopeFactory.GetAppClientAsync(tenantId, appId, cancellationToken);
            execution = await appDb.Queryable<WorkflowExecution>()
                .FirstAsync(item => item.TenantIdValue == tenantId.Value && item.Id == executionId, cancellationToken);
            if (execution is not null)
            {
                return execution;
            }
        }

        return null;
    }
}

