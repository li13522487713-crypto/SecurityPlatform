using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.Identity;
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


public sealed class ResourceCenterQueryService : IResourceCenterQueryService
{
    private const int ResourceCenterPerAppConcurrency = 4;
    private readonly ISqlSugarClient _mainDb;
    private readonly Atlas.Infrastructure.Services.IAppDbScopeFactory _appDbScopeFactory;
    private readonly ILogger<ResourceCenterQueryService> _logger;

    public ResourceCenterQueryService(
        ISqlSugarClient db,
        Atlas.Infrastructure.Services.IAppDbScopeFactory appDbScopeFactory,
        ILogger<ResourceCenterQueryService> logger)
    {
        _mainDb = db;
        _appDbScopeFactory = appDbScopeFactory;
        _logger = logger;
    }

    public ResourceCenterQueryService(ISqlSugarClient db)
        : this(
            db,
            new Atlas.Infrastructure.Services.MainOnlyAppDbScopeFactory(db),
            NullLogger<ResourceCenterQueryService>.Instance)
    {
    }

    public async Task<ResourceCenterGroupsResponse> GetGroupsAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenantIdValue = tenantId.Value;
        var tenantIdText = tenantId.ToString();

        var catalogsTask = _mainDb.Queryable<AppManifest>()
            .Where(item => item.TenantIdValue == tenantIdValue)
            .OrderByDescending(item => item.UpdatedAt)
            .ToListAsync(cancellationToken);
        var tenantApplicationsTask = _mainDb.Queryable<TenantApplication>()
            .Where(item => item.TenantIdValue == tenantIdValue)
            .OrderByDescending(item => item.UpdatedAt)
            .ToListAsync(cancellationToken);
        var dataSourcesTask = _mainDb.Queryable<TenantDataSource>()
            .Where(item => item.TenantIdValue == tenantIdText)
            .OrderByDescending(item => item.UpdatedAt)
            .ToListAsync(cancellationToken);
        var releasesTask = _mainDb.Queryable<AppRelease>()
            .Where(item => item.TenantIdValue == tenantIdValue)
            .OrderByDescending(item => item.ReleasedAt)
            .ToListAsync(cancellationToken);
        var auditsTask = _mainDb.Queryable<AuditRecord>()
            .Where(item => item.TenantIdValue == tenantIdValue)
            .OrderByDescending(item => item.OccurredAt)
            .Take(200)
            .ToListAsync(cancellationToken);
        await Task.WhenAll(
            catalogsTask,
            tenantApplicationsTask,
            dataSourcesTask,
            releasesTask,
            auditsTask);

        var catalogs = catalogsTask.Result;
        var tenantApplications = tenantApplicationsTask.Result;
        var appInstances = tenantApplications;
        var dataSources = dataSourcesTask.Result;
        var releases = releasesTask.Result;
        var runtimeContextResult = await LoadRuntimeRoutesAcrossAppsAsync(tenantId, appInstances, cancellationToken);
        var runtimeExecutionResult = await LoadRuntimeExecutionsAcrossAppsAsync(tenantId, appInstances, cancellationToken);
        var runtimeContexts = runtimeContextResult.Items;
        var runtimeExecutions = runtimeExecutionResult.Items;
        var warnings = runtimeContextResult.Warnings
            .Concat(runtimeExecutionResult.Warnings)
            .GroupBy(item => item.AppInstanceId)
            .Select(group => group.First())
            .ToArray();
        var audits = auditsTask.Result;

        var catalogById = catalogs.ToDictionary(item => item.Id);
        var appInstanceById = appInstances
            .GroupBy(item => item.AppInstanceId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.UpdatedAt).First());

        var catalogItems = catalogs
            .Select(item => new ResourceCenterGroupEntry(
                item.Id.ToString(),
                item.Name,
                "ApplicationCatalog",
                item.Status.ToString(),
                item.Description,
                $"/console/application-catalogs?catalogId={item.Id}",
                item.Id.ToString()))
            .ToArray();
        var tenantApplicationItems = tenantApplications
            .Select(item => new ResourceCenterGroupEntry(
                item.Id.ToString(),
                item.Name,
                "TenantApplication",
                item.Status.ToString(),
                $"AppKey={item.AppKey}",
                $"/console/tenant-applications?applicationId={item.Id}",
                item.CatalogId.ToString(),
                item.AppInstanceId.ToString()))
            .ToArray();
        var appInstanceItems = appInstances
            .Select(item => new ResourceCenterGroupEntry(
                item.AppInstanceId.ToString(),
                item.Name,
                "TenantAppInstance",
                item.Status.ToString(),
                $"AppKey={item.AppKey}",
                $"/console/tenant-app-instances?instanceId={item.AppInstanceId}",
                null,
                item.AppInstanceId.ToString()))
            .ToArray();
        var dataSourceItems = dataSources
            .Select(item => new ResourceCenterGroupEntry(
                item.Id.ToString(),
                item.Name,
                "TenantDataSource",
                item.IsActive ? "Active" : "Disabled",
                item.LastTestMessage,
                $"/console/datasource-consumption?dataSourceId={item.Id}",
                null,
                item.AppId?.ToString()))
            .ToArray();
        var releaseItems = releases
            .Select(item =>
            {
                var manifest = catalogById.TryGetValue(item.ManifestId, out var manifestValue) ? manifestValue : null;
                return new ResourceCenterGroupEntry(
                    item.Id.ToString(),
                    $"v{item.Version}",
                    "Release",
                    item.Status.ToString(),
                    manifest is null ? item.ReleaseNote : $"{manifest.Name} / {manifest.AppKey}",
                    $"/console/releases?releaseId={item.Id}",
                    item.ManifestId.ToString(),
                    null,
                    item.Id.ToString());
            })
            .ToArray();
        var runtimeContextItems = runtimeContexts
            .Select(item => new ResourceCenterGroupEntry(
                item.Id.ToString(),
                $"{item.AppKey}/{item.PageKey}",
                "RuntimeContext",
                item.IsActive ? "Active" : "Inactive",
                $"SchemaVersion={item.SchemaVersion}, Env={item.EnvironmentCode}",
                $"/console/runtime-contexts?contextId={item.Id}",
                item.ManifestId.ToString(),
                null,
                null,
                item.Id.ToString()))
            .ToArray();
        var runtimeExecutionItems = runtimeExecutions
            .Select(item => new ResourceCenterGroupEntry(
                item.Id.ToString(),
                $"Workflow:{item.WorkflowId}",
                "RuntimeExecution",
                item.Status.ToString(),
                $"StartedAt={item.StartedAt:O}",
                $"/console/runtime-executions?executionId={item.Id}",
                null,
                item.AppId?.ToString(),
                item.ReleaseId?.ToString(),
                item.RuntimeContextId?.ToString(),
                item.Id.ToString()))
            .ToArray();
        var auditSummaryItems = audits
            .GroupBy(item => new { item.Action, item.Result })
            .OrderByDescending(group => group.Count())
            .Take(20)
            .Select(group => new ResourceCenterGroupEntry(
                $"{group.Key.Action}:{group.Key.Result}",
                group.Key.Action,
                "AuditSummary",
                group.Key.Result,
                $"近200条记录命中 {group.Count()} 次",
                $"/audit?keyword={Uri.EscapeDataString(group.Key.Action)}"))
            .ToArray();
        var debugEntryItems = audits
            .Where(item =>
                item.Action.Contains("debug", StringComparison.OrdinalIgnoreCase) ||
                item.Target.Contains("debug", StringComparison.OrdinalIgnoreCase))
            .Take(20)
            .Select(item => new ResourceCenterGroupEntry(
                $"{item.Action}:{item.OccurredAt:yyyyMMddHHmmssfff}",
                item.Action,
                "DebugEntry",
                item.Result,
                $"{item.Target} @ {item.OccurredAt:O}",
                "/console/debug"))
            .ToArray();

        var groups = new[]
        {
            new ResourceCenterGroupItem("catalogs", "应用目录", catalogItems.Length, catalogItems),
            new ResourceCenterGroupItem("tenant-applications", "租户开通关系", tenantApplicationItems.Length, tenantApplicationItems),
            new ResourceCenterGroupItem("instances", "租户应用实例", appInstanceItems.Length, appInstanceItems),
            new ResourceCenterGroupItem("releases", "发布记录", releaseItems.Length, releaseItems),
            new ResourceCenterGroupItem("runtime-contexts", "运行上下文", runtimeContextItems.Length, runtimeContextItems),
            new ResourceCenterGroupItem("runtime-executions", "运行执行", runtimeExecutionItems.Length, runtimeExecutionItems),
            new ResourceCenterGroupItem("datasources", "租户数据源", dataSourceItems.Length, dataSourceItems),
            new ResourceCenterGroupItem("audit-summary", "审计汇总", auditSummaryItems.Length, auditSummaryItems),
            new ResourceCenterGroupItem("debug-entries", "调试记录", debugEntryItems.Length, debugEntryItems)
        };
        var warningItems = warnings
            .Select(item => new ResourceCenterWarningItem(
                item.AppInstanceId.ToString(),
                item.AppName,
                item.ErrorCode,
                item.Message))
            .ToArray();
        return new ResourceCenterGroupsResponse(groups, warningItems);
    }

    public async Task<ResourceCenterGroupsSummaryResponse> GetGroupsSummaryAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        var detail = await GetGroupsAsync(tenantId, cancellationToken);
        var groups = detail.Groups
            .Select(group => new ResourceCenterGroupSummaryItem(
                group.GroupKey,
                group.GroupName,
                group.Total))
            .ToArray();
        return new ResourceCenterGroupsSummaryResponse(
            groups,
            detail.Warnings.Count,
            DateTimeOffset.UtcNow.ToString("O"));
    }

    public async Task<ResourceCenterDataSourceConsumptionResponse> GetDataSourceConsumptionAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenantIdValue = tenantId.Value;
        var tenantIdText = tenantIdValue.ToString();

        var dataSourcesTask = _mainDb.Queryable<TenantDataSource>()
            .Where(item => item.TenantIdValue == tenantIdText)
            .OrderByDescending(item => item.UpdatedAt)
            .ToListAsync(cancellationToken);
        var appInstancesTask = _mainDb.Queryable<TenantApplication>()
            .Where(item => item.TenantIdValue == tenantIdValue)
            .OrderByDescending(item => item.UpdatedAt)
            .ToListAsync(cancellationToken);
        var bindingsTask = _mainDb.Queryable<TenantAppDataSourceBindingEntity>()
            .Where(item => item.TenantIdValue == tenantIdValue)
            .OrderByDescending(item => item.UpdatedAt ?? item.BoundAt)
            .ToListAsync(cancellationToken);
        await Task.WhenAll(dataSourcesTask, appInstancesTask, bindingsTask);

        var dataSources = dataSourcesTask.Result;
        var appInstances = appInstancesTask.Result;
        var bindings = bindingsTask.Result;

        var appInstanceById = appInstances
            .GroupBy(item => item.AppInstanceId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.UpdatedAt).First());
        var bindingAppSet = bindings
            .Select(item => item.TenantAppInstanceId)
            .ToHashSet();
        var allRelations = bindings
            .Select(item => new DataSourceBindingRelation(
                item.Id.ToString(),
                item.TenantAppInstanceId,
                item.DataSourceId,
                item.BindingType.ToString(),
                item.IsActive,
                item.BoundAt,
                item.UpdatedAt,
                "BindingTable"))
            .ToList();
        allRelations.AddRange(
            appInstances
                .Where(item => item.DataSourceId.HasValue && !bindingAppSet.Contains(item.AppInstanceId))
                .Select(item => new DataSourceBindingRelation(
                    $"legacy-{item.AppInstanceId}-{item.DataSourceId!.Value}",
                    item.AppInstanceId,
                    item.DataSourceId!.Value,
                    TenantAppDataSourceBindingType.Primary.ToString(),
                    true,
                    null,
                    null,
                    "LegacyTenantApplication.DataSourceId")));

        var activeBoundAppSet = allRelations
            .Where(item => item.IsActive)
            .Select(item => item.TenantAppInstanceId)
            .ToHashSet();
        var relationsByDataSourceId = allRelations
            .GroupBy(item => item.DataSourceId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.IsActive)
                    .ThenBy(item => item.BindingType)
                    .ThenByDescending(item => item.UpdatedAt ?? item.BoundAt ?? DateTimeOffset.MinValue)
                    .ToArray());
        var duplicateCountByKey = dataSources
            .GroupBy(item =>
            {
                var scope = item.AppId.HasValue ? "app" : "platform";
                return $"{scope}:{item.AppId?.ToString() ?? "0"}:{item.DbType}:{item.Name}".ToLowerInvariant();
            })
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        TenantDataSourceConsumptionItem MapDataSource(TenantDataSource dataSource)
        {
            var relations = relationsByDataSourceId.TryGetValue(dataSource.Id, out var dataSourceRelations)
                ? dataSourceRelations
                : [];
            var bindingRelations = relations
                .Select(item => new TenantDataSourceBindingRelationItem(
                    item.BindingId,
                    item.TenantAppInstanceId.ToString(),
                    item.DataSourceId.ToString(),
                    item.BindingType,
                    item.IsActive,
                    item.BoundAt?.ToString("O"),
                    item.UpdatedAt?.ToString("O"),
                    item.Source))
                .ToArray();
            var boundApps = relations
                .Where(item => item.IsActive)
                .Select(item => appInstanceById.TryGetValue(item.TenantAppInstanceId, out var app) ? MapAppConsumer(app) : null)
                .Where(item => item is not null)
                .GroupBy(item => item!.TenantAppInstanceId, StringComparer.Ordinal)
                .Select(group => group.First()!)
                .ToArray();

            var scope = dataSource.AppId.HasValue ? "AppScoped" : "Platform";
            string? scopeAppId = null;
            string? scopeAppName = null;
            if (dataSource.AppId.HasValue && appInstanceById.TryGetValue(dataSource.AppId.Value, out var scopeApp))
            {
                scopeAppId = scopeApp.Id.ToString();
                scopeAppName = scopeApp.Name;
            }
            else if (dataSource.AppId.HasValue)
            {
                scopeAppId = dataSource.AppId.Value.ToString();
            }

            var duplicateScopeKey = dataSource.AppId.HasValue ? "app" : "platform";
            var duplicateKey = $"{duplicateScopeKey}:{dataSource.AppId?.ToString() ?? "0"}:{dataSource.DbType}:{dataSource.Name}".ToLowerInvariant();
            var isDuplicate = duplicateCountByKey.TryGetValue(duplicateKey, out var duplicateCount) && duplicateCount > 1;
            var isOrphan = dataSource.AppId.HasValue && !appInstanceById.ContainsKey(dataSource.AppId.Value);
            var isUnbound = boundApps.Length == 0;
            var testMessage = dataSource.LastTestMessage ?? string.Empty;
            var isInvalid = !dataSource.IsActive
                || testMessage.Contains("失败", StringComparison.OrdinalIgnoreCase)
                || testMessage.Contains("error", StringComparison.OrdinalIgnoreCase)
                || testMessage.Contains("exception", StringComparison.OrdinalIgnoreCase);
            var impactScope = scope == "Platform"
                ? $"Platform / BoundApps:{boundApps.Length}"
                : $"AppScoped:{scopeAppName ?? scopeAppId ?? "-"} / BoundApps:{boundApps.Length}";
            var repairSuggestion = isOrphan
                ? "建议执行“解绑孤儿绑定”"
                : isInvalid
                    ? "建议执行“禁用无效绑定”"
                    : isDuplicate
                        ? "建议执行“切换主绑定”并清理重复绑定"
                        : isUnbound
                            ? "建议执行“切换主绑定”补齐有效绑定"
                            : "无需修复";

            return new TenantDataSourceConsumptionItem(
                dataSource.Id.ToString(),
                dataSource.Name,
                dataSource.DbType,
                dataSource.IsActive,
                scope,
                scopeAppId,
                scopeAppName,
                boundApps.Length,
                boundApps,
                bindingRelations,
                dataSource.LastTestedAt?.ToString("O"),
                dataSource.LastTestMessage,
                isOrphan,
                isDuplicate,
                isInvalid,
                isUnbound,
                impactScope,
                repairSuggestion);
        }

        var platformDataSources = dataSources
            .Where(item => !item.AppId.HasValue)
            .Select(MapDataSource)
            .ToArray();
        var appScopedDataSources = dataSources
            .Where(item => item.AppId.HasValue)
            .Select(MapDataSource)
            .ToArray();
        var unboundTenantApps = appInstances
            .Where(item => !activeBoundAppSet.Contains(item.AppInstanceId))
            .Select(MapAppConsumer)
            .ToArray();

        return new ResourceCenterDataSourceConsumptionResponse(
            platformDataSources.Length,
            appScopedDataSources.Length,
            unboundTenantApps.Length,
            platformDataSources,
            appScopedDataSources,
            unboundTenantApps);
    }

    public async Task<ResourceCenterDataSourceConsumptionSummaryResponse> GetDataSourceConsumptionSummaryAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        var detail = await GetDataSourceConsumptionAsync(tenantId, cancellationToken);
        return new ResourceCenterDataSourceConsumptionSummaryResponse(
            detail.PlatformDataSourceTotal,
            detail.AppScopedDataSourceTotal,
            detail.UnboundTenantAppTotal,
            detail.PlatformDataSources
                .Select(MapDataSourceSummary)
                .ToArray(),
            detail.AppScopedDataSources
                .Select(MapDataSourceSummary)
                .ToArray(),
            detail.UnboundTenantApps,
            DateTimeOffset.UtcNow.ToString("O"));
    }

    private static TenantAppConsumerItem MapAppConsumer(TenantApplication app)
    {
        return new TenantAppConsumerItem(
            app.AppInstanceId.ToString(),
            app.AppKey,
            app.Name,
            app.Status.ToString());
    }

    private static TenantDataSourceConsumptionSummaryItem MapDataSourceSummary(TenantDataSourceConsumptionItem item)
    {
        return new TenantDataSourceConsumptionSummaryItem(
            item.DataSourceId,
            item.Name,
            item.DbType,
            item.Scope,
            item.ScopeAppId,
            item.ScopeAppName,
            item.BoundTenantAppCount,
            item.BoundTenantApps,
            item.LastTestedAt,
            item.IsOrphan,
            item.IsDuplicate,
            item.IsInvalid,
            item.IsUnbound);
    }

    private sealed record DataSourceBindingRelation(
        string BindingId,
        long TenantAppInstanceId,
        long DataSourceId,
        string BindingType,
        bool IsActive,
        DateTimeOffset? BoundAt,
        DateTimeOffset? UpdatedAt,
        string Source);

    private async Task<ResourceCenterAppLoadResult<RuntimeRoute>> LoadRuntimeRoutesAcrossAppsAsync(
        TenantId tenantId,
        IReadOnlyList<TenantApplication> appInstances,
        CancellationToken cancellationToken)
    {
        using var gate = new SemaphoreSlim(ResourceCenterPerAppConcurrency);
        var tasks = new List<Task<ResourceCenterPerAppLoadResult<RuntimeRoute>>>
        {
            LoadMainDbRoutesAsync(tenantId, cancellationToken)
        };
        tasks.AddRange(appInstances.Select(async app =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                var appDb = await _appDbScopeFactory.TryGetAppClientAsync(tenantId, app.AppInstanceId, cancellationToken);
                if (appDb is null)
                {
                    return ResourceCenterPerAppLoadResult<RuntimeRoute>.Skipped(
                        new ResourceCenterAppLoadWarning(
                            app.AppInstanceId,
                            app.Name,
                            ErrorCodes.AppDataSourceNotBound,
                            $"应用实例 {app.AppInstanceId} 未绑定可用数据源，已跳过。"));
                }
                var items = await appDb.Queryable<RuntimeRoute>()
                    .Where(item => item.TenantIdValue == tenantId.Value)
                    .ToListAsync(cancellationToken);
                return ResourceCenterPerAppLoadResult<RuntimeRoute>.Success(items);
            }
            finally
            {
                gate.Release();
            }
        }));

        await Task.WhenAll(tasks);
        var results = tasks.Select(task => task.Result).ToArray();
        var items = results
            .SelectMany(task => task.Items)
            .GroupBy(item => item.Id)
            .Select(group => group.First())
            .OrderByDescending(item => item.Id)
            .ToArray();
        var warnings = results
            .Select(item => item.Warning)
            .Where(item => item is not null)
            .Select(item => item!)
            .ToArray();
        return new ResourceCenterAppLoadResult<RuntimeRoute>(items, warnings);
    }

    private async Task<ResourceCenterAppLoadResult<WorkflowExecution>> LoadRuntimeExecutionsAcrossAppsAsync(
        TenantId tenantId,
        IReadOnlyList<TenantApplication> appInstances,
        CancellationToken cancellationToken)
    {
        using var gate = new SemaphoreSlim(ResourceCenterPerAppConcurrency);
        var tasks = new List<Task<ResourceCenterPerAppLoadResult<WorkflowExecution>>>
        {
            LoadMainDbExecutionsAsync(tenantId, cancellationToken)
        };
        tasks.AddRange(appInstances.Select(async app =>
        {
            await gate.WaitAsync(cancellationToken);
            try
            {
                var appDb = await _appDbScopeFactory.TryGetAppClientAsync(tenantId, app.AppInstanceId, cancellationToken);
                if (appDb is null)
                {
                    return ResourceCenterPerAppLoadResult<WorkflowExecution>.Skipped(
                        new ResourceCenterAppLoadWarning(
                            app.AppInstanceId,
                            app.Name,
                            ErrorCodes.AppDataSourceNotBound,
                            $"应用实例 {app.AppInstanceId} 未绑定可用数据源，已跳过。"));
                }
                var items = await appDb.Queryable<WorkflowExecution>()
                    .Where(item => item.TenantIdValue == tenantId.Value)
                    .Take(200)
                    .ToListAsync(cancellationToken);
                return ResourceCenterPerAppLoadResult<WorkflowExecution>.Success(items);
            }
            finally
            {
                gate.Release();
            }
        }));

        await Task.WhenAll(tasks);
        var results = tasks.Select(task => task.Result).ToArray();
        var items = results
            .SelectMany(task => task.Items)
            .GroupBy(item => item.Id)
            .Select(group => group.First())
            .OrderByDescending(item => item.StartedAt)
            .Take(200)
            .ToArray();
        var warnings = results
            .Select(item => item.Warning)
            .Where(item => item is not null)
            .Select(item => item!)
            .ToArray();
        return new ResourceCenterAppLoadResult<WorkflowExecution>(items, warnings);
    }

    private async Task<ResourceCenterPerAppLoadResult<RuntimeRoute>> LoadMainDbRoutesAsync(
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var items = await _mainDb.Queryable<RuntimeRoute>()
            .Where(item => item.TenantIdValue == tenantId.Value)
            .ToListAsync(cancellationToken);
        return ResourceCenterPerAppLoadResult<RuntimeRoute>.Success(items);
    }

    private async Task<ResourceCenterPerAppLoadResult<WorkflowExecution>> LoadMainDbExecutionsAsync(
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var items = await _mainDb.Queryable<WorkflowExecution>()
            .Where(item => item.TenantIdValue == tenantId.Value)
            .Take(200)
            .ToListAsync(cancellationToken);
        return ResourceCenterPerAppLoadResult<WorkflowExecution>.Success(items);
    }

    private sealed record ResourceCenterPerAppLoadResult<T>(
        IReadOnlyList<T> Items,
        ResourceCenterAppLoadWarning? Warning)
    {
        public static ResourceCenterPerAppLoadResult<T> Success(IReadOnlyList<T> items)
            => new(items, null);

        public static ResourceCenterPerAppLoadResult<T> Skipped(ResourceCenterAppLoadWarning warning)
            => new(Array.Empty<T>(), warning);
    }

    private sealed record ResourceCenterAppLoadResult<T>(
        IReadOnlyList<T> Items,
        IReadOnlyList<ResourceCenterAppLoadWarning> Warnings);

    private sealed record ResourceCenterAppLoadWarning(
        long AppInstanceId,
        string AppName,
        string ErrorCode,
        string Message);
}

