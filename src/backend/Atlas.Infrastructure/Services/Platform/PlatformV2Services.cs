using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.Identity;
using Atlas.Application.LowCode.Models;
using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Application.System.Abstractions;
using Atlas.Application.System.Models;
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
using SqlSugar;
using TenantAppDataSourceBindingDto = Atlas.Application.Platform.Models.TenantAppDataSourceBinding;
using TenantAppDataSourceBindingEntity = Atlas.Domain.System.Entities.TenantAppDataSourceBinding;

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
        string? status = null,
        string? category = null,
        string? appKey = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _appManifestQueryService.QueryAsync(
            tenantId,
            request,
            status,
            category,
            appKey,
            cancellationToken);
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
        string? status = null,
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

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TenantApplicationStatus>(status, true, out var statusValue))
        {
            query = query.Where(item => item.Status == statusValue);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(item => item.UpdatedAt)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);
        if (rows.Count == 0)
        {
            return await BuildFallbackResultAsync(tenantId, request, status, cancellationToken);
        }

        var catalogIds = rows.Select(item => item.CatalogId).Distinct().ToArray();
        var catalogNameDict = new Dictionary<long, string>();
        if (catalogIds.Length > 0)
        {
            var catalogs = await _db.Queryable<AppManifest>()
                .Where(item => item.TenantIdValue == tenantValue && SqlFunc.ContainsArray(catalogIds, item.Id))
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
        string? status,
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

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TenantApplicationStatus>(status, true, out var statusValue))
        {
            if (statusValue == TenantApplicationStatus.Provisioning)
            {
                return new PagedResult<TenantApplicationListItem>(Array.Empty<TenantApplicationListItem>(), 0, pageIndex, pageSize);
            }

            var appStatus = statusValue switch
            {
                TenantApplicationStatus.Disabled => LowCodeAppStatus.Disabled,
                TenantApplicationStatus.Archived => LowCodeAppStatus.Archived,
                _ => LowCodeAppStatus.Published
            };
            appQuery = appQuery.Where(item => item.Status == appStatus);
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
            .Where(item => item.TenantIdValue == tenantValue && SqlFunc.ContainsArray(appKeys, item.AppKey))
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
    private readonly ITenantDataSourceService _tenantDataSourceService;
    private readonly ISqlSugarClient _db;

    public TenantAppInstanceQueryService(
        ILowCodeAppQueryService lowCodeAppQueryService,
        ITenantDataSourceService tenantDataSourceService,
        ISqlSugarClient db)
    {
        _lowCodeAppQueryService = lowCodeAppQueryService;
        _tenantDataSourceService = tenantDataSourceService;
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

    public Task<LowCodeAppSharingPolicy?> GetSharingPolicyAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken = default)
    {
        return _lowCodeAppQueryService.GetSharingPolicyAsync(tenantId, id, cancellationToken);
    }

    public Task<IReadOnlyList<LowCodeAppEntityAliasItem>> GetEntityAliasesAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken = default)
    {
        return _lowCodeAppQueryService.GetEntityAliasesAsync(tenantId, id, cancellationToken);
    }

    public Task<LowCodeAppDataSourceInfo?> GetDataSourceInfoAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken = default)
    {
        return _lowCodeAppQueryService.GetDataSourceInfoAsync(tenantId, id, cancellationToken);
    }

    public async Task<TestConnectionResult> TestDataSourceAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken = default)
    {
        var dataSourceInfo = await _lowCodeAppQueryService.GetDataSourceInfoAsync(tenantId, id, cancellationToken);
        if (dataSourceInfo is null)
        {
            return new TestConnectionResult(false, "应用不存在");
        }

        if (!long.TryParse(dataSourceInfo.DataSourceId, out var dataSourceId))
        {
            return new TestConnectionResult(false, "应用未绑定数据源");
        }

        return await _tenantDataSourceService.TestConnectionByDataSourceIdAsync(
            tenantId.Value.ToString(),
            dataSourceId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<TenantAppDataSourceBindingDto>> GetDataSourceBindingsAsync(
        TenantId tenantId,
        IReadOnlyCollection<long>? appInstanceIds,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var appQuery = _db.Queryable<LowCodeApp>()
            .Where(app => app.TenantIdValue == tenantValue);
        if (appInstanceIds is { Count: > 0 })
        {
            var filterAppIds = appInstanceIds.ToArray();
            appQuery = appQuery.Where(app => SqlFunc.ContainsArray(filterAppIds, app.Id));
        }

        var apps = await appQuery
            .OrderByDescending(app => app.Id)
            .ToListAsync(cancellationToken);
        if (apps.Count == 0)
        {
            return [];
        }

        var appIds = apps.Select(app => app.Id).ToArray();
        var bindings = await _db.Queryable<TenantAppDataSourceBindingEntity>()
            .Where(binding => binding.TenantIdValue == tenantValue && SqlFunc.ContainsArray(appIds, binding.TenantAppInstanceId))
            .ToListAsync(cancellationToken);
        var preferredBindingsByAppId = bindings
            .GroupBy(binding => binding.TenantAppInstanceId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(binding => binding.IsActive)
                    .ThenBy(binding => binding.BindingType)
                    .ThenByDescending(binding => binding.UpdatedAt ?? binding.BoundAt)
                    .First());

        var legacyDataSourceIds = apps
            .Where(app => app.DataSourceId.HasValue && !preferredBindingsByAppId.ContainsKey(app.Id))
            .Select(app => app.DataSourceId!.Value);
        var dataSourceIds = preferredBindingsByAppId.Values
            .Select(binding => binding.DataSourceId)
            .Concat(legacyDataSourceIds)
            .Distinct()
            .ToArray();
        var tenantIdText = tenantValue.ToString();
        var dataSourceDict = new Dictionary<long, TenantDataSource>();
        if (dataSourceIds.Length > 0)
        {
            var tenantDataSources = await _db.Queryable<TenantDataSource>()
                .Where(ds => ds.TenantIdValue == tenantIdText && SqlFunc.ContainsArray(dataSourceIds, ds.Id))
                .ToListAsync(cancellationToken);
            dataSourceDict = tenantDataSources.ToDictionary(ds => ds.Id);
        }

        return apps
            .Select(app =>
            {
                if (preferredBindingsByAppId.TryGetValue(app.Id, out var binding))
                {
                    var dataSource = dataSourceDict.TryGetValue(binding.DataSourceId, out var bindingDataSource)
                        ? bindingDataSource
                        : null;
                    return new TenantAppDataSourceBindingDto(
                        app.Id.ToString(),
                        binding.DataSourceId.ToString(),
                        dataSource?.Name,
                        dataSource?.DbType,
                        dataSource?.IsActive,
                        dataSource?.LastTestedAt?.ToString("O"),
                        binding.Id.ToString(),
                        binding.BindingType.ToString(),
                        binding.IsActive,
                        binding.BoundAt.ToString("O"),
                        "BindingTable");
                }

                if (app.DataSourceId.HasValue)
                {
                    var dataSource = dataSourceDict.TryGetValue(app.DataSourceId.Value, out var legacyDataSource)
                        ? legacyDataSource
                        : null;
                    return new TenantAppDataSourceBindingDto(
                        app.Id.ToString(),
                        app.DataSourceId.Value.ToString(),
                        dataSource?.Name,
                        dataSource?.DbType,
                        dataSource?.IsActive,
                        dataSource?.LastTestedAt?.ToString("O"),
                        null,
                        TenantAppDataSourceBindingType.Primary.ToString(),
                        null,
                        null,
                        "LegacyLowCodeApp.DataSourceId");
                }

                return new TenantAppDataSourceBindingDto(
                    app.Id.ToString(),
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    "Unbound");
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

    public Task UpdateSharingPolicyAsync(
        TenantId tenantId,
        long userId,
        long id,
        LowCodeAppSharingPolicyUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        return _commandService.UpdateSharingPolicyAsync(tenantId, userId, id, request, cancellationToken);
    }

    public Task UpdateEntityAliasesAsync(
        TenantId tenantId,
        long userId,
        long id,
        LowCodeAppEntityAliasesUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        return _commandService.UpdateEntityAliasesAsync(tenantId, userId, id, request, cancellationToken);
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

    public async Task<RuntimeContextDetail?> GetByIdAsync(
        TenantId tenantId,
        long id,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var route = await _db.Queryable<RuntimeRoute>()
            .FirstAsync(
                x => x.TenantIdValue == tenantValue && x.Id == id,
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

        var query = _db.Queryable<WorkflowExecution>()
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
        var execution = await _db.Queryable<WorkflowExecution>()
            .FirstAsync(item => item.TenantIdValue == tenantValue && item.Id == executionId, cancellationToken);
        var targetSet = BuildAuditTargetSet(executionId, execution);
        var auditTargets = targetSet.ToArray();
        var query = _db.Queryable<AuditRecord>()
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
        var tenantIdText = tenantIdValue.ToString();

        var dataSourcesTask = _db.Queryable<TenantDataSource>()
            .Where(item => item.TenantIdValue == tenantIdText)
            .OrderByDescending(item => item.UpdatedAt)
            .ToListAsync(cancellationToken);
        var appInstancesTask = _db.Queryable<LowCodeApp>()
            .Where(item => item.TenantIdValue == tenantIdValue)
            .OrderByDescending(item => item.UpdatedAt)
            .ToListAsync(cancellationToken);
        var bindingsTask = _db.Queryable<TenantAppDataSourceBindingEntity>()
            .Where(item => item.TenantIdValue == tenantIdValue)
            .OrderByDescending(item => item.UpdatedAt ?? item.BoundAt)
            .ToListAsync(cancellationToken);
        await Task.WhenAll(dataSourcesTask, appInstancesTask, bindingsTask);

        var dataSources = dataSourcesTask.Result;
        var appInstances = appInstancesTask.Result;
        var bindings = bindingsTask.Result;

        var appInstanceById = appInstances.ToDictionary(item => item.Id);
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
                .Where(item => item.DataSourceId.HasValue && !bindingAppSet.Contains(item.Id))
                .Select(item => new DataSourceBindingRelation(
                    $"legacy-{item.Id}-{item.DataSourceId!.Value}",
                    item.Id,
                    item.DataSourceId!.Value,
                    TenantAppDataSourceBindingType.Primary.ToString(),
                    true,
                    null,
                    null,
                    "LegacyLowCodeApp.DataSourceId")));

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
            .Where(item => !activeBoundAppSet.Contains(item.Id))
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

    private sealed record DataSourceBindingRelation(
        string BindingId,
        long TenantAppInstanceId,
        long DataSourceId,
        string BindingType,
        bool IsActive,
        DateTimeOffset? BoundAt,
        DateTimeOffset? UpdatedAt,
        string Source);
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
        string? status = null,
        string? appKey = null,
        long? manifestId = null,
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

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<AppReleaseStatus>(status, true, out var statusValue))
        {
            query = query.Where(item => item.Status == statusValue);
        }

        if (manifestId.HasValue && manifestId.Value > 0)
        {
            query = query.Where(item => item.ManifestId == manifestId.Value);
        }

        if (!string.IsNullOrWhiteSpace(appKey))
        {
            var appKeyValue = appKey.Trim();
            var manifestIds = manifestList
                .Where(item => item.AppKey.Contains(appKeyValue, StringComparison.OrdinalIgnoreCase))
                .Select(item => item.Id)
                .ToArray();
            if (manifestIds.Length == 0)
            {
                return new PagedResult<ReleaseCenterListItem>(Array.Empty<ReleaseCenterListItem>(), 0, pageIndex, pageSize);
            }

            query = query.Where(item => SqlFunc.ContainsArray(manifestIds, item.ManifestId));
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
    private readonly ISqlSugarClient _db;

    public DebugLayerQueryService(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<DebugLayerEmbedMetadata> GetEmbedMetadataAsync(
        TenantId tenantId,
        long userId,
        string appId,
        long? projectId,
        bool projectScopeEnabled,
        CancellationToken cancellationToken = default)
    {
        var roleIds = await _db.Queryable<UserRole>()
            .Where(item => item.TenantIdValue == tenantId.Value && item.UserId == userId)
            .Select(item => item.RoleId)
            .ToListAsync(cancellationToken);
        var grantedPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (roleIds.Count > 0)
        {
            var roleIdArray = roleIds.Distinct().ToArray();
            var permissionIds = await _db.Queryable<RolePermission>()
                .Where(item => item.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(roleIdArray, item.RoleId))
                .Select(item => item.PermissionId)
                .Distinct()
                .ToListAsync(cancellationToken);
            if (permissionIds.Count > 0)
            {
                var permissionIdArray = permissionIds.ToArray();
                var permissionCodes = await _db.Queryable<Permission>()
                    .Where(item => item.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(permissionIdArray, item.Id))
                    .Select(item => item.Code)
                    .ToListAsync(cancellationToken);
                grantedPermissions = permissionCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
        }

        var resourceDefinitions = new[]
        {
            new DebugLayerResourceItem("workflow-executions", "运行执行观测", PermissionCodes.DebugView, "查看执行状态、输入输出与错误消息"),
            new DebugLayerResourceItem("runtime-audit-trails", "运行审计追溯", PermissionCodes.DebugRun, "查看与执行ID关联的审计轨迹"),
            new DebugLayerResourceItem("rollback-ops", "回滚操作入口", PermissionCodes.DebugManage, "触发发布版本回滚操作")
        };
        var resources = resourceDefinitions
            .Where(item => grantedPermissions.Contains(item.RequiredPermission))
            .ToArray();

        return new DebugLayerEmbedMetadata(
            tenantId.ToString(),
            appId,
            projectId?.ToString(),
            projectScopeEnabled,
            resources);
    }
}
