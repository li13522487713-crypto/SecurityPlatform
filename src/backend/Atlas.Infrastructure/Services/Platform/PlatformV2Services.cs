using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.Audit.Entities;
using Atlas.Domain.LowCode.Entities;
using Atlas.Domain.LowCode.Enums;
using Atlas.Domain.Platform.Entities;
using Atlas.Domain.System.Entities;
using SqlSugar;

namespace Atlas.Infrastructure.Services.Platform;

public sealed class ApplicationCatalogQueryService : IApplicationCatalogQueryService
{
    private readonly IAppManifestQueryService _appManifestQueryService;

    public ApplicationCatalogQueryService(IAppManifestQueryService appManifestQueryService)
    {
        _appManifestQueryService = appManifestQueryService;
    }

    public async Task<PagedResult<ApplicationCatalogListItem>> QueryAsync(
        TenantId tenantId,
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _appManifestQueryService.QueryAsync(tenantId, request, cancellationToken);
        var items = result.Items
            .Select(item => new ApplicationCatalogListItem(
                item.Id,
                item.AppKey,
                item.Name,
                item.Status,
                item.Version,
                item.Description,
                item.Category,
                item.Icon,
                item.PublishedAt))
            .ToArray();

        return new PagedResult<ApplicationCatalogListItem>(items, result.Total, result.PageIndex, result.PageSize);
    }

    public async Task<ApplicationCatalogDetail?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken = default)
    {
        var item = await _appManifestQueryService.GetByIdAsync(tenantId, id, cancellationToken);
        if (item is null)
        {
            return null;
        }

        return new ApplicationCatalogDetail(
            item.Id,
            item.AppKey,
            item.Name,
            item.Status,
            item.Version,
            item.Description,
            item.Category,
            item.Icon,
            item.PublishedAt,
            null);
    }
}

public sealed class TenantApplicationQueryService : ITenantApplicationQueryService
{
    private readonly ISqlSugarClient _db;

    public TenantApplicationQueryService(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<PagedResult<TenantApplicationListItem>> QueryAsync(
        TenantId tenantId,
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;

        var query = _db.Queryable<TenantApplication>()
            .Where(item => item.TenantIdValue == tenantValue);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(item => item.AppKey.Contains(keyword) || item.Name.Contains(keyword));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(item => item.UpdatedAt)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        if (rows.Count == 0)
        {
            return await BuildFallbackResultAsync(tenantId, request, cancellationToken);
        }

        var catalogIds = rows.Select(item => item.CatalogId).Distinct().ToArray();
        var catalogNameDict = new Dictionary<long, string>();
        if (catalogIds.Length > 0)
        {
            var catalogs = await _db.Queryable<AppManifest>()
                .Where(item => item.TenantIdValue == tenantValue && catalogIds.Contains(item.Id))
                .Select(item => new { item.Id, item.Name })
                .ToListAsync(cancellationToken);
            catalogNameDict = catalogs.ToDictionary(item => item.Id, item => item.Name);
        }

        var items = rows.Select(item =>
        {
            catalogNameDict.TryGetValue(item.CatalogId, out var catalogName);
            return new TenantApplicationListItem(
                item.Id.ToString(),
                item.CatalogId.ToString(),
                catalogName ?? "Unknown",
                item.AppInstanceId.ToString(),
                item.AppKey,
                item.Name,
                item.Status.ToString(),
                item.OpenedAt.ToString("O"),
                item.DataSourceId?.ToString());
        }).ToArray();

        return new PagedResult<TenantApplicationListItem>(items, total, pageIndex, pageSize);
    }

    public async Task<TenantApplicationDetail?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var item = await _db.Queryable<TenantApplication>()
            .FirstAsync(row => row.TenantIdValue == tenantValue && row.Id == id, cancellationToken);
        if (item is null)
        {
            return await BuildFallbackDetailAsync(tenantId, id, cancellationToken);
        }

        var catalog = await _db.Queryable<AppManifest>()
            .Where(row => row.TenantIdValue == tenantValue && row.Id == item.CatalogId)
            .Select(row => new { row.Name })
            .FirstAsync(cancellationToken);

        return new TenantApplicationDetail(
            item.Id.ToString(),
            item.CatalogId.ToString(),
            catalog?.Name ?? "Unknown",
            item.AppInstanceId.ToString(),
            item.AppKey,
            item.Name,
            item.Status.ToString(),
            item.OpenedAt.ToString("O"),
            item.UpdatedAt.ToString("O"),
            item.DataSourceId?.ToString());
    }

    private async Task<PagedResult<TenantApplicationListItem>> BuildFallbackResultAsync(
        TenantId tenantId,
        PagedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantValue = tenantId.Value;
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;

        var appQuery = _db.Queryable<LowCodeApp>()
            .Where(item => item.TenantIdValue == tenantValue);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            appQuery = appQuery.Where(item => item.AppKey.Contains(keyword) || item.Name.Contains(keyword));
        }

        var total = await appQuery.CountAsync(cancellationToken);
        var apps = await appQuery
            .OrderByDescending(item => item.UpdatedAt)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        if (apps.Count == 0)
        {
            return new PagedResult<TenantApplicationListItem>([], 0, pageIndex, pageSize);
        }

        var appKeys = apps.Select(item => item.AppKey).Distinct().ToArray();
        var catalogs = await _db.Queryable<AppManifest>()
            .Where(item => item.TenantIdValue == tenantValue && appKeys.Contains(item.AppKey))
            .Select(item => new { item.Id, item.AppKey, item.Name })
            .ToListAsync(cancellationToken);
        var catalogByAppKey = catalogs
            .GroupBy(item => item.AppKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.Id).First(), StringComparer.OrdinalIgnoreCase);

        var items = apps.Select(app =>
        {
            catalogByAppKey.TryGetValue(app.AppKey, out var catalog);
            return new TenantApplicationListItem(
                app.Id.ToString(),
                catalog?.Id.ToString() ?? string.Empty,
                catalog?.Name ?? "Unknown",
                app.Id.ToString(),
                app.AppKey,
                app.Name,
                MapTenantApplicationStatus(app.Status).ToString(),
                app.CreatedAt.ToString("O"),
                app.DataSourceId?.ToString());
        }).ToArray();

        return new PagedResult<TenantApplicationListItem>(items, total, pageIndex, pageSize);
    }

    private async Task<TenantApplicationDetail?> BuildFallbackDetailAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken)
    {
        var tenantValue = tenantId.Value;
        var app = await _db.Queryable<LowCodeApp>()
            .FirstAsync(item => item.TenantIdValue == tenantValue && item.Id == id, cancellationToken);
        if (app is null)
        {
            return null;
        }

        var catalog = await _db.Queryable<AppManifest>()
            .Where(item => item.TenantIdValue == tenantValue && item.AppKey == app.AppKey)
            .OrderByDescending(item => item.Id)
            .Select(item => new { item.Id, item.Name })
            .FirstAsync(cancellationToken);

        return new TenantApplicationDetail(
            app.Id.ToString(),
            catalog?.Id.ToString() ?? string.Empty,
            catalog?.Name ?? "Unknown",
            app.Id.ToString(),
            app.AppKey,
            app.Name,
            MapTenantApplicationStatus(app.Status).ToString(),
            app.CreatedAt.ToString("O"),
            app.UpdatedAt.ToString("O"),
            app.DataSourceId?.ToString());
    }

    private static TenantApplicationStatus MapTenantApplicationStatus(LowCodeAppStatus status)
    {
        return status switch
        {
            LowCodeAppStatus.Disabled => TenantApplicationStatus.Disabled,
            LowCodeAppStatus.Archived => TenantApplicationStatus.Archived,
            _ => TenantApplicationStatus.Active
        };
    }
}

public sealed class TenantAppInstanceQueryService : ITenantAppInstanceQueryService
{
    private readonly ILowCodeAppQueryService _lowCodeAppQueryService;
    private readonly ISqlSugarClient _db;

    public TenantAppInstanceQueryService(
        ILowCodeAppQueryService lowCodeAppQueryService,
        ISqlSugarClient db)
    {
        _lowCodeAppQueryService = lowCodeAppQueryService;
        _db = db;
    }

    public async Task<PagedResult<TenantAppInstanceListItem>> QueryAsync(
        TenantId tenantId,
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _lowCodeAppQueryService.QueryAsync(request, tenantId, null, cancellationToken);
        var items = result.Items
            .Select(item => new TenantAppInstanceListItem(
                item.Id,
                item.AppKey,
                item.Name,
                item.Status,
                item.Version,
                item.Description,
                item.Category,
                item.Icon,
                item.PublishedAt?.ToString("O")))
            .ToArray();

        return new PagedResult<TenantAppInstanceListItem>(items, result.Total, result.PageIndex, result.PageSize);
    }

    public async Task<TenantAppInstanceDetail?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken = default)
    {
        var item = await _lowCodeAppQueryService.GetByIdAsync(tenantId, id, cancellationToken);
        if (item is null)
        {
            return null;
        }

        return new TenantAppInstanceDetail(
            item.Id,
            item.AppKey,
            item.Name,
            item.Status,
            item.Version,
            item.Description,
            item.Category,
            item.Icon,
            item.PublishedAt?.ToString("O"),
            item.DataSourceId);
    }

    public async Task<IReadOnlyList<TenantAppDataSourceBinding>> GetDataSourceBindingsAsync(
        TenantId tenantId,
        IReadOnlyCollection<long>? appInstanceIds,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var appQuery = _db.Queryable<LowCodeApp>()
            .Where(app => app.TenantIdValue == tenantValue);
        if (appInstanceIds is { Count: > 0 })
        {
            var appIds = appInstanceIds.ToArray();
            appQuery = appQuery.Where(app => appIds.Contains(app.Id));
        }

        var apps = await appQuery
            .OrderByDescending(app => app.Id)
            .ToListAsync(cancellationToken);
        if (apps.Count == 0)
        {
            return [];
        }

        var dataSourceIds = apps
            .Where(app => app.DataSourceId.HasValue)
            .Select(app => app.DataSourceId!.Value)
            .Distinct()
            .ToArray();
        var tenantIdText = tenantId.ToString();
        var dataSourceDict = new Dictionary<long, TenantDataSource>();
        if (dataSourceIds.Length > 0)
        {
            var tenantDataSources = await _db.Queryable<TenantDataSource>()
                .Where(ds => ds.TenantIdValue == tenantIdText && dataSourceIds.Contains(ds.Id))
                .ToListAsync(cancellationToken);
            dataSourceDict = tenantDataSources.ToDictionary(ds => ds.Id);
        }

        return apps
            .Select(app =>
            {
                var dataSource = app.DataSourceId.HasValue && dataSourceDict.TryGetValue(app.DataSourceId.Value, out var item)
                    ? item
                    : null;
                return new TenantAppDataSourceBinding(
                    app.Id.ToString(),
                    app.DataSourceId?.ToString(),
                    dataSource?.Name,
                    dataSource?.DbType,
                    dataSource?.IsActive,
                    dataSource?.LastTestedAt?.ToString("O"));
            })
            .ToArray();
    }
}

public sealed class TenantAppInstanceCommandService : ITenantAppInstanceCommandService
{
    private readonly ILowCodeAppCommandService _commandService;
    private readonly ILowCodeAppQueryService _queryService;

    public TenantAppInstanceCommandService(
        ILowCodeAppCommandService commandService,
        ILowCodeAppQueryService queryService)
    {
        _commandService = commandService;
        _queryService = queryService;
    }

    public Task<long> CreateAsync(
        TenantId tenantId,
        long userId,
        LowCodeAppCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        return _commandService.CreateAsync(tenantId, userId, request, cancellationToken);
    }

    public Task UpdateAsync(
        TenantId tenantId,
        long userId,
        long id,
        LowCodeAppUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        return _commandService.UpdateAsync(tenantId, userId, id, request, cancellationToken);
    }

    public Task PublishAsync(
        TenantId tenantId,
        long userId,
        long id,
        CancellationToken cancellationToken = default)
    {
        return _commandService.PublishAsync(tenantId, userId, id, cancellationToken);
    }

    public Task DeleteAsync(
        TenantId tenantId,
        long userId,
        long id,
        CancellationToken cancellationToken = default)
    {
        return _commandService.DeleteAsync(tenantId, userId, id, cancellationToken);
    }

    public Task<LowCodeAppExportPackage?> ExportAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken = default)
    {
        return _queryService.ExportAsync(tenantId, id, cancellationToken);
    }

    public Task<LowCodeAppImportResult> ImportAsync(
        TenantId tenantId,
        long userId,
        LowCodeAppImportRequest request,
        CancellationToken cancellationToken = default)
    {
        return _commandService.ImportAsync(tenantId, userId, request, cancellationToken);
    }
}

public sealed class RuntimeContextQueryService : IRuntimeContextQueryService
{
    private readonly ISqlSugarClient _db;

    public RuntimeContextQueryService(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<PagedResult<RuntimeContextListItem>> QueryAsync(
        TenantId tenantId,
        PagedRequest request,
        string? appKey = null,
        string? pageKey = null,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;
        var query = _db.Queryable<RuntimeRoute>()
            .Where(route => route.TenantIdValue == tenantValue);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(route => route.AppKey.Contains(keyword) || route.PageKey.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(appKey))
        {
            var appKeyValue = appKey.Trim();
            query = query.Where(route => route.AppKey == appKeyValue);
        }

        if (!string.IsNullOrWhiteSpace(pageKey))
        {
            var pageKeyValue = pageKey.Trim();
            query = query.Where(route => route.PageKey == pageKeyValue);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(route => route.Id)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        var items = rows
            .Select(route => new RuntimeContextListItem(
                route.Id.ToString(),
                route.AppKey,
                route.PageKey,
                route.SchemaVersion,
                route.EnvironmentCode,
                route.IsActive))
            .ToArray();

        return new PagedResult<RuntimeContextListItem>(items, total, pageIndex, pageSize);
    }

    public async Task<RuntimeContextDetail?> GetByRouteAsync(
        TenantId tenantId,
        string appKey,
        string pageKey,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var route = await _db.Queryable<RuntimeRoute>()
            .FirstAsync(
                x => x.TenantIdValue == tenantValue && x.AppKey == appKey && x.PageKey == pageKey,
                cancellationToken);
        if (route is null)
        {
            return null;
        }

        return new RuntimeContextDetail(
            route.Id.ToString(),
            route.AppKey,
            route.PageKey,
            route.SchemaVersion,
            route.EnvironmentCode,
            route.IsActive);
    }
}

public sealed class RuntimeExecutionQueryService : IRuntimeExecutionQueryService
{
    private readonly ISqlSugarClient _db;

    public RuntimeExecutionQueryService(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<PagedResult<RuntimeExecutionListItem>> QueryAsync(
        TenantId tenantId,
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;
        var query = _db.Queryable<WorkflowExecution>()
            .Where(execution => execution.TenantIdValue == tenantValue);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(execution => execution.StartedAt)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        var items = rows.Select(execution => new RuntimeExecutionListItem(
            execution.Id.ToString(),
            execution.WorkflowId.ToString(),
            execution.Status.ToString(),
            execution.StartedAt.ToString("O"),
            execution.CompletedAt?.ToString("O"),
            execution.ErrorMessage)).ToArray();

        return new PagedResult<RuntimeExecutionListItem>(items, total, pageIndex, pageSize);
    }

    public async Task<RuntimeExecutionDetail?> GetByIdAsync(
        TenantId tenantId,
        long executionId,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var execution = await _db.Queryable<WorkflowExecution>()
            .FirstAsync(item => item.TenantIdValue == tenantValue && item.Id == executionId, cancellationToken);
        if (execution is null)
        {
            return null;
        }

        return new RuntimeExecutionDetail(
            execution.Id.ToString(),
            execution.WorkflowId.ToString(),
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
        var executionIdText = executionId.ToString();
        var workflowExecutionTarget = $"WorkflowExecution:{executionIdText}";
        var runtimeExecutionTarget = $"RuntimeExecution:{executionIdText}";
        var query = _db.Queryable<AuditRecord>()
            .Where(item => item.TenantIdValue == tenantValue
                && (item.Target == executionIdText
                    || item.Target == workflowExecutionTarget
                    || item.Target == runtimeExecutionTarget));
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
}

public sealed class ResourceCenterQueryService : IResourceCenterQueryService
{
    private readonly ISqlSugarClient _db;

    public ResourceCenterQueryService(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ResourceCenterGroupItem>> GetGroupsAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenantIdValue = tenantId.Value;
        var tenantIdText = tenantId.ToString();

        var catalogsTask = _db.Queryable<AppManifest>()
            .Where(item => item.TenantIdValue == tenantIdValue)
            .OrderByDescending(item => item.UpdatedAt)
            .ToListAsync(cancellationToken);
        var appInstancesTask = _db.Queryable<LowCodeApp>()
            .Where(item => item.TenantIdValue == tenantIdValue)
            .OrderByDescending(item => item.UpdatedAt)
            .ToListAsync(cancellationToken);
        var dataSourcesTask = _db.Queryable<TenantDataSource>()
            .Where(item => item.TenantIdValue == tenantIdText)
            .OrderByDescending(item => item.UpdatedAt)
            .ToListAsync(cancellationToken);
        await Task.WhenAll(catalogsTask, appInstancesTask, dataSourcesTask);

        var catalogItems = catalogsTask.Result
            .Select(item => new ResourceCenterGroupEntry(
                item.Id.ToString(),
                item.Name,
                "ApplicationCatalog",
                item.Status.ToString(),
                item.Description))
            .ToArray();
        var appInstanceItems = appInstancesTask.Result
            .Select(item => new ResourceCenterGroupEntry(
                item.Id.ToString(),
                item.Name,
                "TenantAppInstance",
                item.Status.ToString(),
                item.Description))
            .ToArray();
        var dataSourceItems = dataSourcesTask.Result
            .Select(item => new ResourceCenterGroupEntry(
                item.Id.ToString(),
                item.Name,
                "TenantDataSource",
                item.IsActive ? "Active" : "Disabled",
                item.LastTestMessage))
            .ToArray();

        return
        [
            new ResourceCenterGroupItem("catalogs", "应用目录", catalogItems.Length, catalogItems),
            new ResourceCenterGroupItem("instances", "租户应用实例", appInstanceItems.Length, appInstanceItems),
            new ResourceCenterGroupItem("datasources", "租户数据源", dataSourceItems.Length, dataSourceItems)
        ];
    }

    public async Task<ResourceCenterDataSourceConsumptionResponse> GetDataSourceConsumptionAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenantIdValue = tenantId.Value;
        var tenantIdText = tenantId.ToString();

        var dataSourcesTask = _db.Queryable<TenantDataSource>()
            .Where(item => item.TenantIdValue == tenantIdText)
            .OrderByDescending(item => item.UpdatedAt)
            .ToListAsync(cancellationToken);
        var appInstancesTask = _db.Queryable<LowCodeApp>()
            .Where(item => item.TenantIdValue == tenantIdValue)
            .OrderByDescending(item => item.UpdatedAt)
            .ToListAsync(cancellationToken);
        await Task.WhenAll(dataSourcesTask, appInstancesTask);

        var dataSources = dataSourcesTask.Result;
        var appInstances = appInstancesTask.Result;

        var appInstanceById = appInstances.ToDictionary(item => item.Id);
        var appInstancesByDataSourceId = appInstances
            .Where(item => item.DataSourceId.HasValue)
            .GroupBy(item => item.DataSourceId!.Value)
            .ToDictionary(group => group.Key, group => group.ToArray());

        TenantDataSourceConsumptionItem MapDataSource(TenantDataSource dataSource)
        {
            var boundApps = appInstancesByDataSourceId.TryGetValue(dataSource.Id, out var items)
                ? items.Select(MapAppConsumer).ToArray()
                : [];

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
                dataSource.LastTestedAt?.ToString("O"),
                dataSource.LastTestMessage);
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
            .Where(item => !item.DataSourceId.HasValue)
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

    private static TenantAppConsumerItem MapAppConsumer(LowCodeApp app)
    {
        return new TenantAppConsumerItem(
            app.Id.ToString(),
            app.AppKey,
            app.Name,
            app.Status.ToString());
    }
}

public sealed class ReleaseCenterQueryService : IReleaseCenterQueryService
{
    private readonly ISqlSugarClient _db;

    public ReleaseCenterQueryService(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<PagedResult<ReleaseCenterListItem>> QueryAsync(
        TenantId tenantId,
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;

        var manifestList = await _db.Queryable<AppManifest>()
            .Where(item => item.TenantIdValue == tenantValue)
            .Select(item => new { item.Id, item.Name, item.AppKey })
            .ToListAsync(cancellationToken);
        var manifestDict = manifestList.ToDictionary(item => item.Id);

        var query = _db.Queryable<AppRelease>()
            .Where(item => item.TenantIdValue == tenantValue);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(item => item.ReleaseNote.Contains(keyword));
        }

        var total = await query.CountAsync(cancellationToken);
        var releases = await query
            .OrderByDescending(item => item.ReleasedAt)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        var items = releases
            .Select(item =>
            {
                var manifest = manifestDict.TryGetValue(item.ManifestId, out var manifestValue)
                    ? manifestValue
                    : null;
                return new ReleaseCenterListItem(
                    item.Id.ToString(),
                    item.ManifestId.ToString(),
                    manifest?.Name ?? "Unknown",
                    manifest?.AppKey ?? string.Empty,
                    item.Version,
                    item.Status.ToString(),
                    item.ReleasedAt.ToString("O"),
                    item.ReleaseNote);
            })
            .ToArray();

        return new PagedResult<ReleaseCenterListItem>(items, total, pageIndex, pageSize);
    }

    public async Task<ReleaseCenterDetail?> GetByIdAsync(
        TenantId tenantId,
        long releaseId,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var release = await _db.Queryable<AppRelease>()
            .FirstAsync(item => item.TenantIdValue == tenantValue && item.Id == releaseId, cancellationToken);
        if (release is null)
        {
            return null;
        }

        var manifest = await _db.Queryable<AppManifest>()
            .FirstAsync(item => item.TenantIdValue == tenantValue && item.Id == release.ManifestId, cancellationToken);
        return new ReleaseCenterDetail(
            release.Id.ToString(),
            release.ManifestId.ToString(),
            manifest?.Name ?? "Unknown",
            manifest?.AppKey ?? string.Empty,
            release.Version,
            release.Status.ToString(),
            release.ReleasedAt.ToString("O"),
            release.ReleaseNote,
            release.SnapshotJson);
    }
}

public sealed class CozeMappingQueryService : ICozeMappingQueryService
{
    private readonly ISqlSugarClient _db;

    public CozeMappingQueryService(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<CozeLayerMappingOverview> GetOverviewAsync(
        TenantId tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var catalogsCountTask = _db.Queryable<AppManifest>()
            .Where(item => item.TenantIdValue == tenantValue)
            .CountAsync(cancellationToken);
        var appInstancesCountTask = _db.Queryable<LowCodeApp>()
            .Where(item => item.TenantIdValue == tenantValue)
            .CountAsync(cancellationToken);
        var releasesCountTask = _db.Queryable<AppRelease>()
            .Where(item => item.TenantIdValue == tenantValue)
            .CountAsync(cancellationToken);
        var contextsCountTask = _db.Queryable<RuntimeRoute>()
            .Where(item => item.TenantIdValue == tenantValue)
            .CountAsync(cancellationToken);
        var executionsCountTask = _db.Queryable<WorkflowExecution>()
            .Where(item => item.TenantIdValue == tenantValue)
            .CountAsync(cancellationToken);
        var auditCountTask = _db.Queryable<AuditRecord>()
            .Where(item => item.TenantIdValue == tenantValue)
            .CountAsync(cancellationToken);
        await Task.WhenAll(catalogsCountTask, appInstancesCountTask, releasesCountTask, contextsCountTask, executionsCountTask, auditCountTask);

        var layers = new[]
        {
            new CozeLayerMappingItem("L1", "应用目录层(ApplicationCatalog)", catalogsCountTask.Result, "平台侧目录定义"),
            new CozeLayerMappingItem("L2", "租户实例层(TenantAppInstance)", appInstancesCountTask.Result, "租户开通后的实例"),
            new CozeLayerMappingItem("L3", "发布层(ReleaseCenter)", releasesCountTask.Result, "发布版本与回滚点"),
            new CozeLayerMappingItem("L4", "运行上下文层(RuntimeContext)", contextsCountTask.Result, "运行路由与页面上下文"),
            new CozeLayerMappingItem("L5", "执行层(RuntimeExecution)", executionsCountTask.Result, "运行执行记录与状态"),
            new CozeLayerMappingItem("L6", "审计层(AuditTrail)", auditCountTask.Result, "操作与执行追溯证据")
        };

        return new CozeLayerMappingOverview(layers);
    }
}

public sealed class DebugLayerQueryService : IDebugLayerQueryService
{
    public Task<DebugLayerEmbedMetadata> GetEmbedMetadataAsync(
        TenantId tenantId,
        string appId,
        long? projectId,
        bool projectScopeEnabled,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var resources = new[]
        {
            new DebugLayerResourceItem("workflow-executions", "运行执行观测", "apps:view", "查看执行状态、输入输出与错误消息"),
            new DebugLayerResourceItem("runtime-audit-trails", "运行审计追溯", "audit:view", "查看与执行ID关联的审计轨迹"),
            new DebugLayerResourceItem("rollback-ops", "回滚操作入口", "apps:update", "触发发布版本回滚操作")
        };

        return Task.FromResult(new DebugLayerEmbedMetadata(
            tenantId.ToString(),
            appId,
            projectId?.ToString(),
            projectScopeEnabled,
            resources));
    }
}
