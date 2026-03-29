using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Application.Approval.Abstractions;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Approval.Entities;
using Atlas.Domain.Approval.Enums;
using Atlas.Domain.Audit.Entities;
using Atlas.Domain.Identity.Entities;
using Atlas.Domain.LowCode.Entities;
using Atlas.Domain.Platform.Entities;
using Atlas.Domain.System.Entities;
using SqlSugar;
using System.Text.Json;

namespace Atlas.Infrastructure.Services.Platform;

public sealed class PlatformQueryService : IPlatformQueryService
{
    private readonly ISqlSugarClient _db;
    private readonly Atlas.Infrastructure.Services.IAppDbScopeFactory _appDbScopeFactory;

    public PlatformQueryService(
        ISqlSugarClient db,
        Atlas.Infrastructure.Services.IAppDbScopeFactory appDbScopeFactory)
    {
        _db = db;
        _appDbScopeFactory = appDbScopeFactory;
    }

    public PlatformQueryService(ISqlSugarClient db)
        : this(db, new Atlas.Infrastructure.Services.MainOnlyAppDbScopeFactory(db))
    {
    }

    public async Task<PlatformOverviewResponse> GetOverviewAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var appCount = await _db.Queryable<AppManifest>()
            .Where(x => x.TenantIdValue == tenantValue).CountAsync(cancellationToken);
        var releaseCount = await _db.Queryable<AppRelease>()
            .Where(x => x.TenantIdValue == tenantValue).CountAsync(cancellationToken);
        var activeRouteCount = await PlatformRuntimeAggregationHelper.CountActiveRuntimeRoutesAsync(
            _db,
            _appDbScopeFactory,
            tenantId,
            cancellationToken);
        var policyCount = await _db.Queryable<ToolAuthorizationPolicy>()
            .Where(x => x.TenantIdValue == tenantValue).CountAsync(cancellationToken);
        var licenseCount = await _db.Queryable<LicenseGrant>().CountAsync(cancellationToken);
        return new PlatformOverviewResponse(appCount, releaseCount, activeRouteCount, policyCount, licenseCount);
    }

    public async Task<PlatformResourcesResponse> GetResourcesAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var dbConnectionString = _db.CurrentConnectionConfig.ConnectionString;
        var dbFile = ParseSqliteDataSource(dbConnectionString);
        long dbSize = 0;
        if (!string.IsNullOrWhiteSpace(dbFile) && File.Exists(dbFile))
        {
            dbSize = new FileInfo(dbFile).Length;
        }

        var activeSessionCount = await _db.Queryable<Atlas.Domain.Identity.Entities.AuthSession>()
            .CountAsync(x => x.TenantIdValue == tenantValue && x.RevokedAt == null && x.ExpiresAt > DateTimeOffset.UtcNow, cancellationToken);
        var apiCallCount = await _db.Queryable<Atlas.Domain.Audit.Entities.AuditRecord>()
            .Where(x => x.TenantIdValue == tenantValue).CountAsync(cancellationToken);
        var routeCount = await PlatformRuntimeAggregationHelper.CountActiveRuntimeRoutesAsync(
            _db,
            _appDbScopeFactory,
            tenantId,
            cancellationToken);

        var items = new List<PlatformResourceItem>
        {
            new("database-size", dbSize.ToString(), "bytes", "ok"),
            new("active-sessions", activeSessionCount.ToString(), "count", "ok"),
            new("audit-api-calls", apiCallCount.ToString(), "count", "ok"),
            new("runtime-routes", routeCount.ToString(), "count", "ok")
        };
        return new PlatformResourcesResponse(items);
    }

    public async Task<PagedResult<AppReleaseResponse>> GetReleasesAsync(TenantId tenantId, PagedRequest request, CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;
        var query = _db.Queryable<AppRelease>()
            .Where(x => x.TenantIdValue == tenantValue)
            .OrderByDescending(x => x.ReleasedAt);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.ToPageListAsync(pageIndex, pageSize, cancellationToken);
        var items = rows.Select(x => new AppReleaseResponse(
            x.Id.ToString(),
            x.ManifestId.ToString(),
            x.Version,
            x.Status.ToString(),
            x.ReleasedAt.ToString("O"),
            x.ReleaseNote)).ToArray();
        return new PagedResult<AppReleaseResponse>(items, total, pageIndex, pageSize);
    }

    private static string ParseSqliteDataSource(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return string.Empty;
        }

        const string key = "Data Source=";
        var index = connectionString.IndexOf(key, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return string.Empty;
        }

        var value = connectionString[(index + key.Length)..];
        var semicolon = value.IndexOf(';');
        if (semicolon >= 0)
        {
            value = value[..semicolon];
        }

        return value.Trim();
    }
}

public sealed class AppManifestQueryService : IAppManifestQueryService
{
    private readonly ISqlSugarClient _db;

    public AppManifestQueryService(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<PagedResult<AppManifestResponse>> QueryAsync(
        TenantId tenantId,
        PagedRequest request,
        string? status = null,
        string? category = null,
        string? appKey = null,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;
        var query = _db.Queryable<AppManifest>()
            .Where(item => item.TenantIdValue == tenantValue);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(x => x.Name.Contains(keyword) || x.AppKey.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<AppManifestStatus>(status, true, out var statusValue))
        {
            query = query.Where(item => item.Status == statusValue);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var categoryValue = category.Trim();
            query = query.Where(item => item.Category.Contains(categoryValue));
        }

        if (!string.IsNullOrWhiteSpace(appKey))
        {
            var appKeyValue = appKey.Trim();
            query = query.Where(item => item.AppKey.Contains(appKeyValue));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(x => x.UpdatedAt).ToPageListAsync(pageIndex, pageSize, cancellationToken);
        var items = rows.Select(MapManifest).ToArray();
        return new PagedResult<AppManifestResponse>(items, total, pageIndex, pageSize);
    }

    public async Task<AppManifestResponse?> GetByIdAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Queryable<AppManifest>()
            .FirstAsync(x => x.Id == id && x.TenantIdValue == tenantId.Value, cancellationToken);
        return entity is null ? null : MapManifest(entity);
    }

    public async Task<WorkspaceOverviewResponse> GetWorkspaceOverviewAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default)
    {
        var pageCount = await _db.Queryable<Atlas.Domain.LowCode.Entities.LowCodePage>().CountAsync(x => x.AppId == id, cancellationToken);
        var tableCount = await _db.Queryable<Atlas.Domain.DynamicTables.Entities.DynamicTable>()
            .CountAsync(x => x.AppId == id, cancellationToken);
        var tableKeys = await _db.Queryable<Atlas.Domain.DynamicTables.Entities.DynamicTable>()
            .Where(x => x.AppId == id)
            .Select(x => x.TableKey)
            .ToListAsync(cancellationToken);
        var formCount = tableKeys.Count == 0
            ? 0
            : await _db.Queryable<Atlas.Domain.LowCode.Entities.FormDefinition>()
                .CountAsync(x => x.DataTableKey != null && SqlFunc.ContainsArray(tableKeys.ToArray(), x.DataTableKey!), cancellationToken);
        var flowCount = await _db.Queryable<Atlas.Domain.DynamicTables.Entities.DynamicTable>()
            .Where(table => table.AppId == id && table.ApprovalFlowDefinitionId != null)
            .Select(table => table.ApprovalFlowDefinitionId!.Value)
            .Distinct()
            .CountAsync(cancellationToken);
        return new WorkspaceOverviewResponse(pageCount, formCount, flowCount, tableCount);
    }

    public async Task<PagedResult<object>> GetWorkspacePagesAsync(TenantId tenantId, long id, PagedRequest request, CancellationToken cancellationToken = default)
    {
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;
        var query = _db.Queryable<LowCodePage>().Where(x => x.AppId == id);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(x => x.Name.Contains(keyword) || x.PageKey.Contains(keyword));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(x => x.SortOrder).ToPageListAsync(pageIndex, pageSize, cancellationToken);
        var items = rows.Select(x => (object)new
        {
            Id = x.Id.ToString(),
            x.PageKey,
            x.Name,
            x.RoutePath,
            x.Icon,
            x.SortOrder,
            x.Version,
            x.IsPublished
        }).ToArray();
        return new PagedResult<object>(items, total, pageIndex, pageSize);
    }

    public async Task<PagedResult<object>> GetWorkspaceFormsAsync(TenantId tenantId, long id, PagedRequest request, CancellationToken cancellationToken = default)
    {
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;
        var tableKeys = await _db.Queryable<Atlas.Domain.DynamicTables.Entities.DynamicTable>()
            .Where(x => x.AppId == id)
            .Select(x => x.TableKey)
            .ToListAsync(cancellationToken);
        if (tableKeys.Count == 0)
        {
            return new PagedResult<object>(Array.Empty<object>(), 0, pageIndex, pageSize);
        }

        var tableKeyArray = tableKeys.Distinct().ToArray();
        var query = _db.Queryable<FormDefinition>()
            .Where(x => x.DataTableKey != null && SqlFunc.ContainsArray(tableKeyArray, x.DataTableKey!));
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(form => form.Name.Contains(keyword));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(form => form.UpdatedAt).ToPageListAsync(pageIndex, pageSize, cancellationToken);
        var items = rows.Select(x => (object)new
        {
            Id = x.Id.ToString(),
            x.Name,
            x.Category,
            x.DataTableKey,
            x.Version,
            Status = x.Status.ToString(),
            UpdatedAt = x.UpdatedAt.ToString("O")
        }).ToArray();
        return new PagedResult<object>(items, total, pageIndex, pageSize);
    }

    public async Task<PagedResult<object>> GetWorkspaceFlowsAsync(TenantId tenantId, long id, PagedRequest request, CancellationToken cancellationToken = default)
    {
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;
        var appFlowIds = await _db.Queryable<Atlas.Domain.DynamicTables.Entities.DynamicTable>()
            .Where(x => x.AppId == id && x.ApprovalFlowDefinitionId != null)
            .Select(x => x.ApprovalFlowDefinitionId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (appFlowIds.Count == 0)
        {
            return new PagedResult<object>(Array.Empty<object>(), 0, pageIndex, pageSize);
        }

        var query = _db.Queryable<Atlas.Domain.Approval.Entities.ApprovalFlowDefinition>()
            .Where(x => SqlFunc.ContainsArray(appFlowIds.ToArray(), x.Id));
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(x => x.Name.Contains(keyword));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(x => x.PublishedAt).ToPageListAsync(pageIndex, pageSize, cancellationToken);
        var items = rows.Select(x => (object)new
        {
            Id = x.Id.ToString(),
            x.Name,
            x.Category,
            x.Version,
            Status = x.Status.ToString(),
            UpdatedAt = x.PublishedAt?.ToString("O") ?? string.Empty
        }).ToArray();
        return new PagedResult<object>(items, total, pageIndex, pageSize);
    }

    public async Task<PagedResult<object>> GetWorkspaceDataAsync(TenantId tenantId, long id, PagedRequest request, CancellationToken cancellationToken = default)
    {
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;
        var query = _db.Queryable<Atlas.Domain.DynamicTables.Entities.DynamicTable>().Where(x => x.AppId == id);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim();
            query = query.Where(x => x.TableKey.Contains(keyword) || x.DisplayName.Contains(keyword));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(x => x.UpdatedAt).ToPageListAsync(pageIndex, pageSize, cancellationToken);
        var items = rows.Select(x => (object)new
        {
            Id = x.Id.ToString(),
            x.TableKey,
            x.DisplayName,
            x.DbType,
            x.Status,
            UpdatedAt = x.UpdatedAt.ToString("O")
        }).ToArray();
        return new PagedResult<object>(items, total, pageIndex, pageSize);
    }

    public async Task<WorkspacePermissionResponse> GetWorkspacePermissionsAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default)
    {
        var permissions = await _db.Queryable<Permission>()
            .Where(x => x.Code.StartsWith("apps:") || x.Code.StartsWith("approval:") || x.Code.StartsWith("dynamic:"))
            .OrderBy(x => x.Code)
            .ToListAsync(cancellationToken);
        var items = permissions.Select(x => new WorkspacePermissionItem(x.Code, x.Name)).ToArray();
        return new WorkspacePermissionResponse(items);
    }

    private static AppManifestResponse MapManifest(AppManifest entity)
        => new(
            entity.Id.ToString(),
            entity.AppKey,
            entity.Name,
            entity.Status.ToString(),
            entity.Version,
            entity.Description,
            entity.Category,
            entity.Icon,
            entity.PublishedAt?.ToString("O"));
}

internal static class PlatformRuntimeAggregationHelper
{
    public static async Task<int> CountActiveRuntimeRoutesAsync(
        ISqlSugarClient mainDb,
        Atlas.Infrastructure.Services.IAppDbScopeFactory appDbScopeFactory,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        var appIds = await mainDb.Queryable<LowCodeApp>()
            .Where(x => x.TenantIdValue == tenantId.Value)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var countTasks = new List<Task<List<long>>>
        {
            mainDb.Queryable<RuntimeRoute>()
                .Where(x => x.TenantIdValue == tenantId.Value && x.IsActive)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken)
        };
        countTasks.AddRange(appIds.Select(async appId =>
        {
            var appDb = await appDbScopeFactory.GetAppClientAsync(tenantId, appId, cancellationToken);
            return await appDb.Queryable<RuntimeRoute>()
                .Where(x => x.TenantIdValue == tenantId.Value && x.IsActive)
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);
        }));

        await Task.WhenAll(countTasks);
        return countTasks
            .SelectMany(task => task.Result)
            .Distinct()
            .Count();
    }
}

public sealed class AppManifestCommandService : IAppManifestCommandService
{
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGenerator;

    public AppManifestCommandService(ISqlSugarClient db, IIdGeneratorAccessor idGenerator)
    {
        _db = db;
        _idGenerator = idGenerator;
    }

    public async Task<long> CreateAsync(TenantId tenantId, long userId, AppManifestCreateRequest request, CancellationToken cancellationToken = default)
    {
        var exists = await _db.Queryable<AppManifest>()
            .AnyAsync(x => x.TenantIdValue == tenantId.Value && x.AppKey == request.AppKey, cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException($"应用标识 {request.AppKey} 已存在");
        }

        var entity = new AppManifest(tenantId, _idGenerator.NextId(), request.AppKey, request.Name, userId, DateTimeOffset.UtcNow);
        entity.Update(request.Name, request.Description, request.Category, request.Icon, request.DataSourceId, userId, DateTimeOffset.UtcNow);
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(TenantId tenantId, long userId, long id, AppManifestUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Queryable<AppManifest>()
            .FirstAsync(x => x.Id == id && x.TenantIdValue == tenantId.Value, cancellationToken)
            ?? throw new InvalidOperationException("应用不存在");
        entity.Update(request.Name, request.Description, request.Category, request.Icon, request.DataSourceId, userId, DateTimeOffset.UtcNow);
        await _db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task ArchiveAsync(TenantId tenantId, long userId, long id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Queryable<AppManifest>()
            .FirstAsync(x => x.Id == id && x.TenantIdValue == tenantId.Value, cancellationToken)
            ?? throw new InvalidOperationException("应用不存在");
        entity.Archive(userId, DateTimeOffset.UtcNow);
        await _db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }
}

public sealed class AppReleaseCommandService : IAppReleaseCommandService
{
    private readonly ISqlSugarClient _db;
    private readonly Atlas.Infrastructure.Services.IAppDbScopeFactory _appDbScopeFactory;
    private readonly IIdGeneratorAccessor _idGenerator;

    public AppReleaseCommandService(
        ISqlSugarClient db,
        Atlas.Infrastructure.Services.IAppDbScopeFactory appDbScopeFactory,
        IIdGeneratorAccessor idGenerator)
    {
        _db = db;
        _appDbScopeFactory = appDbScopeFactory;
        _idGenerator = idGenerator;
    }

    public AppReleaseCommandService(ISqlSugarClient db, IIdGeneratorAccessor idGenerator)
        : this(db, new Atlas.Infrastructure.Services.MainOnlyAppDbScopeFactory(db), idGenerator)
    {
    }

    public async Task<ReleasePreCheckResult> PreCheckAsync(TenantId tenantId, long manifestId, CancellationToken cancellationToken = default)
    {
        var pageCount = await _db.Queryable<Atlas.Domain.LowCode.Entities.LowCodePage>()
            .CountAsync(x => x.AppId == manifestId, cancellationToken);
        var tableCount = await _db.Queryable<Atlas.Domain.DynamicTables.Entities.DynamicTable>()
            .CountAsync(x => x.AppId == manifestId, cancellationToken);
        var runtimeDb = await ResolveRuntimeDbByManifestIdAsync(tenantId, manifestId, cancellationToken);
        var routeCount = await runtimeDb.Queryable<RuntimeRoute>()
            .CountAsync(x => x.TenantIdValue == tenantId.Value && x.ManifestId == manifestId, cancellationToken);

        if (pageCount == 0 && tableCount == 0)
        {
            return ReleasePreCheckResult.Fail("应用尚未配置任何页面或数据表，无法发布空版本。", pageCount, tableCount, routeCount);
        }

        return ReleasePreCheckResult.Pass(pageCount, tableCount, routeCount);
    }

    public async Task<long> CreateReleaseAsync(TenantId tenantId, long userId, long manifestId, string? releaseNote, CancellationToken cancellationToken = default)
    {
        var manifest = await _db.Queryable<AppManifest>()
            .FirstAsync(x => x.TenantIdValue == tenantId.Value && x.Id == manifestId, cancellationToken)
            ?? throw new InvalidOperationException("应用不存在");
        var runtimeDb = await ResolveRuntimeDbByAppKeyAsync(tenantId, manifest.AppKey, cancellationToken);
        var runtimeRoutes = await runtimeDb.Queryable<RuntimeRoute>()
            .Where(item => item.TenantIdValue == tenantId.Value && item.ManifestId == manifestId)
            .ToListAsync(cancellationToken);

        var pageCountTask = _db.Queryable<Atlas.Domain.LowCode.Entities.LowCodePage>()
            .CountAsync(x => x.AppId == manifestId, cancellationToken);
        var tableCountTask = _db.Queryable<Atlas.Domain.DynamicTables.Entities.DynamicTable>()
            .CountAsync(x => x.AppId == manifestId, cancellationToken);
        await Task.WhenAll(pageCountTask, tableCountTask);

        if (pageCountTask.Result == 0 && tableCountTask.Result == 0)
        {
            throw new InvalidOperationException("应用尚未配置任何页面或数据表，无法发布空版本。");
        }

        var now = DateTimeOffset.UtcNow;
        manifest.Publish(userId, now);
        var snapshotJson = BuildReleaseSnapshotJson(
            manifest, runtimeRoutes, pageCountTask.Result, tableCountTask.Result, now);
        var release = new AppRelease(tenantId, _idGenerator.NextId(), manifestId, manifest.Version, snapshotJson, userId, now);
        release.MarkReleased(releaseNote);

        var createAudit = new AuditRecord(
            tenantId,
            userId.ToString(),
            "release.create",
            "Success",
            $"AppManifest:{manifestId}/Version:{manifest.Version}",
            null,
            null);

        var transaction = await _db.Ado.UseTranAsync(async () =>
        {
            await _db.Updateable(manifest).ExecuteCommandAsync(cancellationToken);
            await _db.Insertable(release).ExecuteCommandAsync(cancellationToken);
            await _db.Insertable(createAudit).ExecuteCommandAsync(cancellationToken);
        });
        if (!transaction.IsSuccess)
        {
            throw transaction.ErrorException ?? new InvalidOperationException("创建发布记录失败");
        }

        return release.Id;
    }

    public async Task<ReleaseRollbackResult> RollbackAsync(
        TenantId tenantId,
        long userId,
        long manifestId,
        long releaseId,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var manifest = await _db.Queryable<AppManifest>()
            .FirstAsync(x => x.TenantIdValue == tenantValue && x.Id == manifestId, cancellationToken)
            ?? throw new InvalidOperationException("应用不存在");
        var rollbackTarget = await _db.Queryable<AppRelease>()
            .FirstAsync(x => x.TenantIdValue == tenantValue && x.Id == releaseId && x.ManifestId == manifestId, cancellationToken)
            ?? throw new InvalidOperationException("回滚目标发布记录不存在");
        var currentRelease = await _db.Queryable<AppRelease>()
            .Where(x => x.TenantIdValue == tenantValue && x.ManifestId == manifestId && x.Status == AppReleaseStatus.Released)
            .OrderByDescending(x => x.ReleasedAt)
            .FirstAsync(cancellationToken)
            ?? throw new InvalidOperationException("当前发布版本不存在");
        if (currentRelease.Id == rollbackTarget.Id)
        {
            return new ReleaseRollbackResult(
                manifestId.ToString(),
                rollbackTarget.Id.ToString(),
                rollbackTarget.Version,
                currentRelease.Id.ToString(),
                currentRelease.Version,
                false,
                0,
                "NoOp",
                "目标版本已是当前生效版本。");
        }

        var runtimeDb = await ResolveRuntimeDbByAppKeyAsync(tenantId, manifest.AppKey, cancellationToken);
        var runtimeRoutes = await runtimeDb.Queryable<RuntimeRoute>()
            .Where(x => x.TenantIdValue == tenantValue && x.ManifestId == manifestId)
            .ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        currentRelease.MarkRolledBack(rollbackTarget.Id);
        rollbackTarget.MarkReleased(rollbackTarget.ReleaseNote);
        manifest.SyncReleaseVersion(rollbackTarget.Version, userId, now);

        foreach (var route in runtimeRoutes)
        {
            route.RebindManifest(manifestId);
            route.Activate(rollbackTarget.Version, route.EnvironmentCode);
        }

        var rollbackAudit = new AuditRecord(
            tenantId,
            userId.ToString(),
            "release.rollback",
            "Success",
            $"Release:{rollbackTarget.Id}",
            null,
            null);
        var switchAudit = new AuditRecord(
            tenantId,
            userId.ToString(),
            "release.switch",
            "Success",
            $"Release:{currentRelease.Id}->{rollbackTarget.Id}",
            null,
            null);
        var routeAudit = new AuditRecord(
            tenantId,
            userId.ToString(),
            "runtime.route.rebind",
            "Success",
            $"AppManifest:{manifestId}/routes:{runtimeRoutes.Count}",
            null,
            null);

        var transaction = await _db.Ado.UseTranAsync(async () =>
        {
            await _db.Updateable(currentRelease).ExecuteCommandAsync(cancellationToken);
            await _db.Updateable(rollbackTarget).ExecuteCommandAsync(cancellationToken);
            await _db.Updateable(manifest).ExecuteCommandAsync(cancellationToken);
            await _db.Insertable(new[] { rollbackAudit, switchAudit, routeAudit }).ExecuteCommandAsync(cancellationToken);
        });

        if (!transaction.IsSuccess)
        {
            throw transaction.ErrorException ?? new InvalidOperationException("回滚发布记录失败");
        }
        if (runtimeRoutes.Count > 0)
        {
            await runtimeDb.Updateable(runtimeRoutes).ExecuteCommandAsync(cancellationToken);
        }

        return new ReleaseRollbackResult(
            manifestId.ToString(),
            rollbackTarget.Id.ToString(),
            rollbackTarget.Version,
            currentRelease.Id.ToString(),
            currentRelease.Version,
            true,
            runtimeRoutes.Count,
            "Switched",
            null);
    }

    private async Task<ISqlSugarClient> ResolveRuntimeDbByManifestIdAsync(
        TenantId tenantId,
        long manifestId,
        CancellationToken cancellationToken)
    {
        var manifest = await _db.Queryable<AppManifest>()
            .FirstAsync(x => x.TenantIdValue == tenantId.Value && x.Id == manifestId, cancellationToken);
        if (manifest is null)
        {
            return _db;
        }

        return await ResolveRuntimeDbByAppKeyAsync(tenantId, manifest.AppKey, cancellationToken);
    }

    private async Task<ISqlSugarClient> ResolveRuntimeDbByAppKeyAsync(
        TenantId tenantId,
        string appKey,
        CancellationToken cancellationToken)
    {
        var app = await _db.Queryable<LowCodeApp>()
            .FirstAsync(x => x.TenantIdValue == tenantId.Value && x.AppKey == appKey, cancellationToken);
        if (app is not null && app.Id > 0)
        {
            return await _appDbScopeFactory.GetAppClientAsync(tenantId, app.Id, cancellationToken);
        }

        return _db;
    }

    private static string BuildReleaseSnapshotJson(
        AppManifest manifest,
        IReadOnlyCollection<RuntimeRoute> runtimeRoutes,
        int pageCount,
        int tableCount,
        DateTimeOffset generatedAt)
    {
        var snapshot = new
        {
            summary = new
            {
                pageCount,
                tableCount,
                routeCount = runtimeRoutes.Count,
                activeRouteCount = runtimeRoutes.Count(r => r.IsActive),
                datasourceId = manifest.DataSourceId?.ToString()
            },
            manifest = new
            {
                manifest.Id,
                manifest.AppKey,
                manifest.Name,
                manifest.Description,
                manifest.Category,
                manifest.Icon,
                manifest.ConfigJson,
                manifest.DataSourceId,
                manifest.Version,
                Status = manifest.Status.ToString()
            },
            runtimeRoutes = runtimeRoutes
                .OrderBy(item => item.PageKey)
                .Select(item => new
                {
                    item.Id,
                    item.AppKey,
                    item.PageKey,
                    item.SchemaVersion,
                    item.IsActive,
                    item.EnvironmentCode
                })
                .ToArray(),
            generatedAt = generatedAt.ToString("O")
        };
        return JsonSerializer.Serialize(snapshot);
    }
}

public sealed class RuntimeRouteQueryService : IRuntimeRouteQueryService
{
    private static readonly HashSet<string> BlockingMigrationStatuses = new(StringComparer.Ordinal)
    {
        AppMigrationTaskStatuses.Pending,
        AppMigrationTaskStatuses.Prechecking,
        AppMigrationTaskStatuses.Running,
        AppMigrationTaskStatuses.Validating
    };

    private readonly ISqlSugarClient _mainDb;
    private readonly Atlas.Infrastructure.Services.IAppDbScopeFactory _appDbScopeFactory;
    private readonly IApprovalOperationService _approvalOperationService;

    public RuntimeRouteQueryService(
        ISqlSugarClient db,
        Atlas.Infrastructure.Services.IAppDbScopeFactory appDbScopeFactory,
        IApprovalOperationService approvalOperationService)
    {
        _mainDb = db;
        _appDbScopeFactory = appDbScopeFactory;
        _approvalOperationService = approvalOperationService;
    }

    public async Task<RuntimePageResponse?> GetRuntimePageAsync(TenantId tenantId, string appKey, string pageKey, CancellationToken cancellationToken = default)
    {
        var db = await ResolveRuntimeDbByAppKeyAsync(tenantId, appKey, cancellationToken);
        var route = await db.Queryable<RuntimeRoute>()
            .FirstAsync(
                x => x.TenantIdValue == tenantId.Value && x.AppKey == appKey && x.PageKey == pageKey && x.IsActive,
                cancellationToken);
        return route is null
            ? null
            : new RuntimePageResponse(route.AppKey, route.PageKey, route.SchemaVersion, route.IsActive);
    }

    public async Task<PagedResult<RuntimeTaskListItem>> GetRuntimeTasksAsync(
        TenantId tenantId,
        long userId,
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;
        var assignee = userId.ToString();
        var query = _mainDb.Queryable<ApprovalTask>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value &&
                x.AssigneeValue == assignee &&
                x.Status == ApprovalTaskStatus.Pending);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(x => x.CreatedAt).ToPageListAsync(pageIndex, pageSize, cancellationToken);
        var items = rows.Select(x => new RuntimeTaskListItem(
            x.Id.ToString(),
            "approval",
            x.Title,
            x.Status.ToString(),
            x.CreatedAt.ToString("O"))).ToArray();
        return new PagedResult<RuntimeTaskListItem>(items, total, pageIndex, pageSize);
    }

    public async Task<PagedResult<RuntimeTaskListItem>> GetRuntimeDoneTasksAsync(
        TenantId tenantId,
        long userId,
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;
        var assignee = userId.ToString();
        var query = _mainDb.Queryable<ApprovalTask>()
            .Where(x =>
                x.TenantIdValue == tenantId.Value &&
                x.AssigneeValue == assignee &&
                x.Status != ApprovalTaskStatus.Pending &&
                x.Status != ApprovalTaskStatus.Waiting &&
                x.Status != ApprovalTaskStatus.Claimed);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(x => x.DecisionAt).ToPageListAsync(pageIndex, pageSize, cancellationToken);
        var items = rows.Select(x => new RuntimeTaskListItem(
            x.Id.ToString(),
            "approval",
            x.Title,
            x.Status.ToString(),
            (x.DecisionAt ?? x.CreatedAt).ToString("O"))).ToArray();
        return new PagedResult<RuntimeTaskListItem>(items, total, pageIndex, pageSize);
    }

    public async Task<RuntimeMenuResponse> GetRuntimeMenuAsync(TenantId tenantId, string appKey, CancellationToken cancellationToken = default)
    {
        var db = await ResolveRuntimeDbByAppKeyAsync(tenantId, appKey, cancellationToken);
        var routes = await db.Queryable<RuntimeRoute>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppKey == appKey && x.IsActive)
            .OrderBy(x => x.PageKey)
            .ToListAsync(cancellationToken);
        if (routes.Count == 0)
        {
            return new RuntimeMenuResponse(appKey, Array.Empty<RuntimeMenuItem>());
        }

        var pageKeys = routes.Select(x => x.PageKey).Distinct().ToArray();
        var pages = await _mainDb.Queryable<LowCodePage>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId > 0 && SqlFunc.ContainsArray(pageKeys, x.PageKey))
            .ToListAsync(cancellationToken);
        var pageMap = pages.ToDictionary(x => x.PageKey, x => x, StringComparer.OrdinalIgnoreCase);
        var items = routes.Select(route =>
        {
            if (pageMap.TryGetValue(route.PageKey, out var page))
            {
                return new RuntimeMenuItem(
                    route.PageKey,
                    page.Name,
                    string.IsNullOrWhiteSpace(page.RoutePath) ? $"/r/{route.AppKey}/{route.PageKey}" : page.RoutePath!,
                    page.Icon,
                    page.SortOrder);
            }

            return new RuntimeMenuItem(
                route.PageKey,
                route.PageKey,
                $"/r/{route.AppKey}/{route.PageKey}",
                null,
                0);
        }).OrderBy(x => x.SortOrder).ThenBy(x => x.Title).ToArray();

        return new RuntimeMenuResponse(appKey, items);
    }

    public async Task<bool> ExecuteRuntimeTaskActionAsync(
        TenantId tenantId,
        long userId,
        long taskId,
        RuntimeTaskActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var task = await _mainDb.Queryable<ApprovalTask>()
            .FirstAsync(x => x.Id == taskId && x.TenantIdValue == tenantId.Value, cancellationToken);
        if (task is null)
        {
            return false;
        }

        var normalizedAction = request.Action.Trim().ToLowerInvariant();
        var operationType = normalizedAction switch
        {
            "approve" => ApprovalOperationType.Agree,
            "reject" => ApprovalOperationType.Disagree,
            "transfer" => ApprovalOperationType.Transfer,
            "delegate" => ApprovalOperationType.Delegate,
            "return" => ApprovalOperationType.BackToPrevModify,
            _ => throw new BusinessException(
                ErrorCodes.ValidationError,
                $"不支持的运行态任务动作: {request.Action}")
        };
        var opRequest = new Atlas.Application.Approval.Models.ApprovalOperationRequest
        {
            OperationType = operationType,
            Comment = request.Comment
        };
        await _approvalOperationService.ExecuteOperationAsync(
            tenantId,
            task.InstanceId,
            task.Id,
            userId,
            opRequest,
            cancellationToken);
        return true;
    }

    private async Task<ISqlSugarClient> ResolveRuntimeDbByAppKeyAsync(
        TenantId tenantId,
        string appKey,
        CancellationToken cancellationToken)
    {
        var app = await _mainDb.Queryable<LowCodeApp>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppKey == appKey)
            .FirstAsync(cancellationToken);
        if (app is not null && app.Id > 0)
        {
            await EnsureRuntimeReadableAsync(tenantId, app.Id, cancellationToken);
            return await _appDbScopeFactory.GetAppClientAsync(tenantId, app.Id, cancellationToken);
        }

        return _mainDb;
    }

    private async Task EnsureRuntimeReadableAsync(
        TenantId tenantId,
        long appInstanceId,
        CancellationToken cancellationToken)
    {
        var latestTask = await _mainDb.Queryable<AppMigrationTask>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.TenantAppInstanceId == appInstanceId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstAsync(cancellationToken);
        if (latestTask is null || !BlockingMigrationStatuses.Contains(latestTask.Status))
        {
            return;
        }

        throw new BusinessException(
            ErrorCodes.AppMigrationPending,
            "应用正在切库同步中，请稍后重试。");
    }
}

public sealed class AppDesignerSnapshotService : IAppDesignerSnapshotService
{
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGenerator;

    public AppDesignerSnapshotService(ISqlSugarClient db, IIdGeneratorAccessor idGenerator)
    {
        _db = db;
        _idGenerator = idGenerator;
    }

    public async Task<DesignerSnapshotResponse?> GetSnapshotAsync(
        TenantId tenantId,
        long manifestId,
        string type,
        long itemId,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var entity = await _db.Queryable<AppDesignerSnapshot>()
            .Where(x => x.TenantIdValue == tenantValue && x.ManifestId == manifestId
                && x.SnapshotType == type && x.ItemId == itemId)
            .OrderByDescending(x => x.Version)
            .FirstAsync(cancellationToken);
        return entity is null
            ? null
            : new DesignerSnapshotResponse(
                entity.ManifestId.ToString(),
                entity.SnapshotType,
                entity.ItemId.ToString(),
                entity.SchemaJson,
                entity.Version,
                entity.CreatedBy,
                entity.CreatedAt);
    }

    public async Task SaveSnapshotAsync(
        TenantId tenantId,
        long userId,
        long manifestId,
        string type,
        long itemId,
        string schemaJson,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var maxVersion = await _db.Queryable<AppDesignerSnapshot>()
            .Where(x => x.TenantIdValue == tenantValue && x.ManifestId == manifestId
                && x.SnapshotType == type && x.ItemId == itemId)
            .MaxAsync(x => x.Version, cancellationToken);
        var nextVersion = maxVersion + 1;
        var entity = new AppDesignerSnapshot(
            tenantId,
            _idGenerator.NextId(),
            manifestId,
            type,
            itemId,
            schemaJson,
            nextVersion,
            userId.ToString(),
            DateTimeOffset.UtcNow);
        await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DesignerSnapshotHistoryItem>> GetSnapshotHistoryAsync(
        TenantId tenantId,
        long manifestId,
        string type,
        long itemId,
        CancellationToken cancellationToken = default)
    {
        var tenantValue = tenantId.Value;
        var rows = await _db.Queryable<AppDesignerSnapshot>()
            .Where(x => x.TenantIdValue == tenantValue && x.ManifestId == manifestId
                && x.SnapshotType == type && x.ItemId == itemId)
            .OrderByDescending(x => x.Version)
            .ToListAsync(cancellationToken);
        return rows.Select(x => new DesignerSnapshotHistoryItem(
            x.Id.ToString(),
            x.Version,
            x.CreatedBy,
            x.CreatedAt)).ToArray();
    }
}
