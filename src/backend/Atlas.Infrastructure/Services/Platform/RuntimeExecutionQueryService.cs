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


public sealed class RuntimeExecutionQueryService : IRuntimeExecutionQueryService
{
    private readonly ISqlSugarClient _mainDb;
    private readonly Atlas.Infrastructure.Services.IAppDbScopeFactory _appDbScopeFactory;

    public RuntimeExecutionQueryService(
        ISqlSugarClient db,
        Atlas.Infrastructure.Services.IAppDbScopeFactory appDbScopeFactory)
    {
        _mainDb = db;
        _appDbScopeFactory = appDbScopeFactory;
    }

    public RuntimeExecutionQueryService(ISqlSugarClient db)
        : this(db, new Atlas.Infrastructure.Services.MainOnlyAppDbScopeFactory(db))
    {
    }

    public async Task<PagedResult<RuntimeExecutionListItem>> QueryAsync(
        TenantId tenantId,
        PagedRequest request,
        string? appId = null,
        string? status = null,
        DateTimeOffset? startedFrom = null,
        DateTimeOffset? startedTo = null,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;

        long? appIdValue = null;
        if (!string.IsNullOrWhiteSpace(appId))
        {
            if (!long.TryParse(appId, out var parsedAppId))
            {
                return new PagedResult<RuntimeExecutionListItem>(Array.Empty<RuntimeExecutionListItem>(), 0, pageIndex, pageSize);
            }
            appIdValue = parsedAppId;
        }

        ExecutionStatus? statusValue = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<ExecutionStatus>(status, true, out var parsedStatus))
            {
                return new PagedResult<RuntimeExecutionListItem>(Array.Empty<RuntimeExecutionListItem>(), 0, pageIndex, pageSize);
            }
            statusValue = parsedStatus;
        }

        var db = await ResolveExecutionDbAsync(tenantId, appIdValue, cancellationToken);
        var query = db.Queryable<WorkflowExecution>()
            .Where(execution => execution.TenantIdValue == tenantValue);

        if (appIdValue.HasValue)
        {
            query = query.Where(execution => execution.AppId == appIdValue.Value);
        }

        if (statusValue.HasValue)
        {
            query = query.Where(execution => execution.Status == statusValue.Value);
        }

        if (startedFrom.HasValue)
        {
            query = query.Where(execution => execution.StartedAt >= startedFrom.Value.UtcDateTime);
        }

        if (startedTo.HasValue)
        {
            query = query.Where(execution => execution.StartedAt <= startedTo.Value.UtcDateTime);
        }

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            if (long.TryParse(keyword, out var idKeyword))
            {
                query = query.Where(execution =>
                    execution.WorkflowId == idKeyword
                    || execution.AppId == idKeyword
                    || execution.ReleaseId == idKeyword
                    || execution.RuntimeContextId == idKeyword
                    || (execution.ErrorMessage != null && execution.ErrorMessage.Contains(keyword)));
            }
            else if (Enum.TryParse<ExecutionStatus>(keyword, true, out var keywordStatus))
            {
                query = query.Where(execution =>
                    execution.Status == keywordStatus
                    || (execution.ErrorMessage != null && execution.ErrorMessage.Contains(keyword)));
            }
            else
            {
                query = query.Where(execution => execution.ErrorMessage != null && execution.ErrorMessage.Contains(keyword));
            }
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(execution => execution.StartedAt)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        var items = rows.Select(execution => new RuntimeExecutionListItem(
            execution.Id.ToString(),
            execution.WorkflowId.ToString(),
            execution.RuntimeContextId?.ToString(),
            execution.ReleaseId?.ToString(),
            execution.AppId?.ToString(),
            execution.Status.ToString(),
            execution.StartedAt.ToString("O"),
            execution.CompletedAt?.ToString("O"),
            execution.ErrorMessage,
            ClassifyErrorCategory(execution.Status, execution.ErrorMessage))).ToArray();

        return new PagedResult<RuntimeExecutionListItem>(items, total, pageIndex, pageSize);
    }

    public async Task<RuntimeExecutionDetail?> GetByIdAsync(
        TenantId tenantId,
        long executionId,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var execution = await FindExecutionAcrossDbsAsync(tenantId, executionId, cancellationToken);
        if (execution is null)
        {
            return null;
        }

        return new RuntimeExecutionDetail(
            execution.Id.ToString(),
            execution.WorkflowId.ToString(),
            execution.RuntimeContextId?.ToString(),
            execution.ReleaseId?.ToString(),
            execution.AppId?.ToString(),
            execution.Status.ToString(),
            execution.StartedAt.ToString("O"),
            execution.CompletedAt?.ToString("O"),
            execution.InputsJson,
            execution.OutputsJson,
            execution.ErrorMessage);
    }

    public async Task<PagedResult<RuntimeExecutionAuditTrailItem>> GetAuditTrailsAsync(
        TenantId tenantId,
        long executionId,
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;
        var execution = await FindExecutionAcrossDbsAsync(tenantId, executionId, cancellationToken);
        var targetSet = BuildAuditTargetSet(executionId, execution);
        var auditTargets = targetSet.ToArray();
        var query = _mainDb.Queryable<AuditRecord>()
            .Where(item => item.TenantIdValue == tenantValue
                && SqlFunc.ContainsArray(auditTargets, item.Target));
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(item => item.Action.Contains(keyword) || item.Target.Contains(keyword) || item.Actor.Contains(keyword));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(item => item.OccurredAt)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        var items = rows.Select(item => new RuntimeExecutionAuditTrailItem(
            item.Id.ToString(),
            item.Actor,
            item.Action,
            item.Result,
            item.Target,
            item.OccurredAt.ToString("O")))
            .ToArray();

        return new PagedResult<RuntimeExecutionAuditTrailItem>(items, total, pageIndex, pageSize);
    }

    public async Task<RuntimeExecutionStats> GetStatsAsync(
        TenantId tenantId,
        string? appId = null,
        DateTimeOffset? startedFrom = null,
        DateTimeOffset? startedTo = null,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;

        long? appIdValue = null;
        if (!string.IsNullOrWhiteSpace(appId) && long.TryParse(appId, out var parsedAppId))
        {
            appIdValue = parsedAppId;
        }

        var db = await ResolveExecutionDbAsync(tenantId, appIdValue, cancellationToken);
        var query = db.Queryable<WorkflowExecution>()
            .Where(execution => execution.TenantIdValue == tenantValue);

        if (appIdValue.HasValue)
        {
            query = query.Where(execution => execution.AppId == appIdValue.Value);
        }

        if (startedFrom.HasValue)
        {
            query = query.Where(execution => execution.StartedAt >= startedFrom.Value.UtcDateTime);
        }

        if (startedTo.HasValue)
        {
            query = query.Where(execution => execution.StartedAt <= startedTo.Value.UtcDateTime);
        }

        var rows = await query
            .Select(execution => new
            {
                execution.Status,
                execution.StartedAt,
                execution.CompletedAt,
                execution.ErrorMessage
            })
            .ToListAsync(cancellationToken);

        var total = rows.Count;
        var running = rows.Count(r => r.Status == ExecutionStatus.Running || r.Status == ExecutionStatus.Pending);
        var succeeded = rows.Count(r => r.Status == ExecutionStatus.Completed);
        var failed = rows.Count(r => r.Status == ExecutionStatus.Failed);
        var cancelled = rows.Count(r => r.Status == ExecutionStatus.Cancelled);

        var durations = rows
            .Where(r => r.CompletedAt.HasValue)
            .Select(r => (r.CompletedAt!.Value - r.StartedAt).TotalMilliseconds)
            .OrderBy(d => d)
            .ToArray();

        double? avgDurationMs = durations.Length > 0 ? durations.Average() : null;
        double? p95DurationMs = null;
        if (durations.Length > 0)
        {
            var p95Index = (int)Math.Ceiling(durations.Length * 0.95) - 1;
            p95DurationMs = durations[Math.Max(0, p95Index)];
        }

        var errorCategories = rows
            .Where(r => r.Status == ExecutionStatus.Failed)
            .GroupBy(r => ClassifyErrorCategory(r.Status, r.ErrorMessage) ?? "Unknown")
            .ToDictionary(g => g.Key, g => (long)g.Count());

        return new RuntimeExecutionStats(
            total,
            running,
            succeeded,
            failed,
            cancelled,
            avgDurationMs,
            p95DurationMs,
            errorCategories);
    }

    private static string? ClassifyErrorCategory(ExecutionStatus status, string? errorMessage)
    {
        if (status != ExecutionStatus.Failed)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return "Unknown";
        }

        var msg = errorMessage.ToLowerInvariant();
        if (msg.Contains("timeout") || msg.Contains("超时") || msg.Contains("timed out"))
        {
            return "Timeout";
        }

        if (msg.Contains("network") || msg.Contains("connection") || msg.Contains("网络") || msg.Contains("连接"))
        {
            return "NetworkError";
        }

        if (msg.Contains("validation") || msg.Contains("invalid") || msg.Contains("校验") || msg.Contains("格式"))
        {
            return "ValidationError";
        }

        if (msg.Contains("config") || msg.Contains("配置") || msg.Contains("setting"))
        {
            return "ConfigError";
        }

        if (msg.Contains("permission") || msg.Contains("forbidden") || msg.Contains("unauthorized")
            || msg.Contains("权限") || msg.Contains("未授权"))
        {
            return "PermissionError";
        }

        return "Unknown";
    }

    private static HashSet<string> BuildAuditTargetSet(long executionId, WorkflowExecution? execution)
    {
        var executionIdText = executionId.ToString();
        var targetSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            executionIdText,
            $"WorkflowExecution:{executionIdText}",
            $"RuntimeExecution:{executionIdText}"
        };

        if (execution?.ReleaseId is { } releaseId)
        {
            targetSet.Add($"Release:{releaseId}");
            targetSet.Add($"AppRelease:{releaseId}");
        }

        if (execution?.RuntimeContextId is { } runtimeContextId)
        {
            targetSet.Add($"RuntimeContext:{runtimeContextId}");
            targetSet.Add($"RuntimeRoute:{runtimeContextId}");
        }

        if (execution?.AppId is { } appId)
        {
            targetSet.Add($"App:{appId}");
            targetSet.Add($"AppManifest:{appId}");
        }

        return targetSet;
    }

    private async Task<ISqlSugarClient> ResolveExecutionDbAsync(
        TenantId tenantId,
        long? appId,
        CancellationToken cancellationToken)
    {
        if (appId.HasValue && appId.Value > 0)
        {
            return await _appDbScopeFactory.GetAppClientAsync(tenantId, appId.Value, cancellationToken);
        }

        return _mainDb;
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

