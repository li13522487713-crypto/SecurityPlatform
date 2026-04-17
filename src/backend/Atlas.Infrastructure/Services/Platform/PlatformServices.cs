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
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.Identity.Entities;
using Atlas.Domain.Platform.Entities;
using Atlas.Domain.System.Entities;
using SqlSugar;
using System.Security.Cryptography;
using System.Text;
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

    public Task<WorkspaceOverviewResponse> GetWorkspaceOverviewAsync(TenantId tenantId, long id, CancellationToken cancellationToken = default)
    {
        _ = tenantId;
        _ = id;
        _ = cancellationToken;
        return Task.FromResult(new WorkspaceOverviewResponse(0, 0, 0, 0));
    }

    public Task<PagedResult<object>> GetWorkspacePagesAsync(TenantId tenantId, long id, PagedRequest request, CancellationToken cancellationToken = default)
    {
        _ = tenantId;
        _ = id;
        _ = cancellationToken;
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;
        return Task.FromResult(new PagedResult<object>(Array.Empty<object>(), 0, pageIndex, pageSize));
    }

    public Task<PagedResult<object>> GetWorkspaceFormsAsync(TenantId tenantId, long id, PagedRequest request, CancellationToken cancellationToken = default)
    {
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;
        return Task.FromResult(new PagedResult<object>(Array.Empty<object>(), 0, pageIndex, pageSize));
    }

    public Task<PagedResult<object>> GetWorkspaceFlowsAsync(TenantId tenantId, long id, PagedRequest request, CancellationToken cancellationToken = default)
    {
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;
        return Task.FromResult(new PagedResult<object>(Array.Empty<object>(), 0, pageIndex, pageSize));
    }

    public Task<PagedResult<object>> GetWorkspaceDataAsync(TenantId tenantId, long id, PagedRequest request, CancellationToken cancellationToken = default)
    {
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = request.PageSize <= 0 ? 10 : request.PageSize;
        return Task.FromResult(new PagedResult<object>(Array.Empty<object>(), 0, pageIndex, pageSize));
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
        _ = appDbScopeFactory;
        return await mainDb.Queryable<RuntimeRoute>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.IsActive)
            .CountAsync(cancellationToken);
    }
}

public sealed class AppManifestCommandService : IAppManifestCommandService
{
    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGenerator;
    private readonly IAppBootstrapService _appBootstrapService;

    public AppManifestCommandService(ISqlSugarClient db, IIdGeneratorAccessor idGenerator, IAppBootstrapService appBootstrapService)
    {
        _db = db;
        _idGenerator = idGenerator;
        _appBootstrapService = appBootstrapService;
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

        await _appBootstrapService.BootstrapAsync(tenantId, entity.Id, userId, cancellationToken);

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
    private readonly IIdGeneratorAccessor _idGenerator;

    public AppReleaseCommandService(ISqlSugarClient db, IIdGeneratorAccessor idGenerator)
    {
        _db = db;
        _idGenerator = idGenerator;
    }

    public async Task<ReleasePreCheckResult> PreCheckAsync(TenantId tenantId, long manifestId, CancellationToken cancellationToken = default)
    {
        const int pageCount = 0;
        const int tableCount = 0;
        var runtimeDb = await ResolveRuntimeDbByManifestIdAsync(tenantId, manifestId, cancellationToken);
        var routeCount = await runtimeDb.Queryable<RuntimeRoute>()
            .CountAsync(x => x.TenantIdValue == tenantId.Value && x.ManifestId == manifestId, cancellationToken);

        if (routeCount == 0 && tableCount == 0)
        {
            return ReleasePreCheckResult.Fail("应用尚未配置任何运行时路由，无法发布空版本。", pageCount, tableCount, routeCount);
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
        var pageSnapshots = runtimeRoutes
            .GroupBy(r => r.PageKey, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Select(r => new ReleasePageSnapshotInfo(
                r.PageKey,
                r.PageKey,
                $"/r/{r.AppKey}/{r.PageKey}",
                null,
                0))
            .ToArray();
        var orchestrationPlanRows = await _db.Queryable<OrchestrationPlan>()
            .Where(item =>
                item.TenantIdValue == tenantId.Value
                && item.AppInstanceId == manifestId
                && item.Status != OrchestrationPlanStatus.Archived)
            .OrderBy(item => item.UpdatedAt)
            .ToListAsync(cancellationToken);
        var orchestrationPlans = orchestrationPlanRows
            .Select(item => new ReleaseOrchestrationPlanInfo(
                item.Id,
                item.PlanKey,
                item.PlanName,
                item.Status.ToString(),
                item.PublishedVersion,
                item.NodeGraphJson,
                item.RuntimePolicyJson,
                item.UpdatedAt))
            .ToArray();

        var pageCount = pageSnapshots.Length;
        const int tableCount = 0;

        if (runtimeRoutes.Count == 0 && tableCount == 0)
        {
            throw new InvalidOperationException("应用尚未配置任何运行时路由或数据表，无法发布空版本。");
        }

        var now = DateTimeOffset.UtcNow;
        manifest.Publish(userId, now);
        var snapshotJson = BuildReleaseSnapshotJson(
            manifest, runtimeRoutes, pageCount, tableCount, now);
        var navigationProjectionSnapshotJson = BuildNavigationSnapshotJson(runtimeRoutes, pageSnapshots, now);
        var exposurePolicy = await _db.Queryable<AppExposurePolicy>()
            .FirstAsync(item => item.TenantIdValue == tenantId.Value && item.AppInstanceId == manifestId, cancellationToken);
        var exposureCatalogSnapshotJson = BuildExposureCatalogSnapshotJson(exposurePolicy, now);
        var release = new AppRelease(tenantId, _idGenerator.NextId(), manifestId, manifest.Version, snapshotJson, userId, now);
        release.SetNavigationSnapshot(navigationProjectionSnapshotJson);
        release.SetExposureCatalogSnapshot(exposureCatalogSnapshotJson);
        release.MarkReleased(releaseNote);

        var runtimeManifestSetJson = BuildReleaseBundleRuntimeManifestSetJson(manifest, runtimeRoutes, pageSnapshots, now);
        var orchestrationPlanSetJson = BuildReleaseBundleOrchestrationPlanSetJson(manifest, orchestrationPlans, now);
        var toolReleaseRefsJson = BuildReleaseBundleToolReleaseRefsJson(manifest, now);
        var knowledgeSnapshotRefsJson = BuildReleaseBundleKnowledgeSnapshotRefsJson(manifest, now);
        var resourceBindingSnapshotJson = BuildReleaseBundleResourceBindingSnapshotJson(manifest, exposurePolicy, now);
        var signatureJson = BuildReleaseBundleSignatureJson(
            runtimeManifestSetJson,
            orchestrationPlanSetJson,
            toolReleaseRefsJson,
            knowledgeSnapshotRefsJson,
            resourceBindingSnapshotJson,
            navigationProjectionSnapshotJson,
            exposureCatalogSnapshotJson,
            now);

        var releaseBundle = new ReleaseBundle(
            tenantId,
            _idGenerator.NextId(),
            release.Id,
            manifestId,
            $"bundle-{manifest.Version}",
            BuildReleaseBundleUnifiedModelJson(
                manifest,
                release,
                runtimeManifestSetJson,
                orchestrationPlanSetJson,
                toolReleaseRefsJson,
                knowledgeSnapshotRefsJson,
                resourceBindingSnapshotJson,
                navigationProjectionSnapshotJson,
                exposureCatalogSnapshotJson,
                signatureJson,
                now),
            BuildReleaseBundleRuntimeProjectionJson(runtimeRoutes, pageSnapshots, now),
            runtimeManifestSetJson,
            orchestrationPlanSetJson,
            toolReleaseRefsJson,
            knowledgeSnapshotRefsJson,
            resourceBindingSnapshotJson,
            navigationProjectionSnapshotJson,
            exposureCatalogSnapshotJson,
            signatureJson,
            userId,
            now);

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
            await _db.Insertable(releaseBundle).ExecuteCommandAsync(cancellationToken);
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

    private Task<ISqlSugarClient> ResolveRuntimeDbByManifestIdAsync(
        TenantId tenantId,
        long manifestId,
        CancellationToken cancellationToken)
    {
        _ = tenantId;
        _ = manifestId;
        _ = cancellationToken;
        return Task.FromResult(_db);
    }

    private Task<ISqlSugarClient> ResolveRuntimeDbByAppKeyAsync(
        TenantId tenantId,
        string appKey,
        CancellationToken cancellationToken)
    {
        _ = tenantId;
        _ = appKey;
        _ = cancellationToken;
        return Task.FromResult(_db);
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

    private static string BuildNavigationSnapshotJson(
        IReadOnlyCollection<RuntimeRoute> runtimeRoutes,
        IReadOnlyCollection<ReleasePageSnapshotInfo> pageSnapshots,
        DateTimeOffset generatedAt)
    {
        var pageMap = BuildPageSnapshotMap(pageSnapshots);
        var snapshot = new
        {
            generatedAt = generatedAt.ToString("O"),
            items = runtimeRoutes
                .Select(item =>
                {
                    var hasPage = pageMap.TryGetValue(item.PageKey, out var page);
                    var routePath = hasPage && !string.IsNullOrWhiteSpace(page.RoutePath)
                        ? page.RoutePath
                        : $"/r/{item.AppKey}/{item.PageKey}";
                    return new
                    {
                        item.AppKey,
                        item.PageKey,
                        item.SchemaVersion,
                        item.IsActive,
                        title = hasPage ? page.Name : item.PageKey,
                        routePath,
                        icon = hasPage ? page.Icon : null,
                        sortOrder = hasPage ? page.SortOrder : 0
                    };
                })
                .OrderBy(item => item.sortOrder)
                .ThenBy(item => item.PageKey, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
        return JsonSerializer.Serialize(snapshot);
    }

    private static string BuildExposureCatalogSnapshotJson(
        AppExposurePolicy? exposurePolicy,
        DateTimeOffset generatedAt)
    {
        if (exposurePolicy is null)
        {
            return JsonSerializer.Serialize(new
            {
                generatedAt = generatedAt.ToString("O"),
                appInstanceId = string.Empty,
                exposedDataSets = Array.Empty<string>(),
                allowedCommands = Array.Empty<string>(),
                maskPolicies = "{}"
            });
        }

        return JsonSerializer.Serialize(new
        {
            generatedAt = generatedAt.ToString("O"),
            appInstanceId = exposurePolicy.AppInstanceId.ToString(),
            exposedDataSets = exposurePolicy.ExposedDataSetsJson,
            allowedCommands = exposurePolicy.AllowedCommandsJson,
            maskPolicies = exposurePolicy.MaskPoliciesJson
        });
    }

    private static string BuildReleaseBundleUnifiedModelJson(
        AppManifest manifest,
        AppRelease release,
        string runtimeManifestSetJson,
        string orchestrationPlanSetJson,
        string toolReleaseRefsJson,
        string knowledgeSnapshotRefsJson,
        string resourceBindingSnapshotJson,
        string navigationProjectionSnapshotJson,
        string exposureCatalogSnapshotJson,
        string signatureJson,
        DateTimeOffset generatedAt)
    {
        var unified = new
        {
            generatedAt = generatedAt.ToString("O"),
            manifest = new
            {
                manifest.Id,
                manifest.AppKey,
                manifest.Name,
                manifest.Version
            },
            release = new
            {
                release.Id,
                release.ManifestId,
                release.Version,
                release.Status
            },
            release.SnapshotJson,
            snapshots = new
            {
                runtimeManifestSet = runtimeManifestSetJson,
                orchestrationPlanSet = orchestrationPlanSetJson,
                toolReleaseRefs = toolReleaseRefsJson,
                knowledgeSnapshotRefs = knowledgeSnapshotRefsJson,
                resourceBindingSnapshot = resourceBindingSnapshotJson,
                navigationProjectionSnapshot = navigationProjectionSnapshotJson,
                exposureCatalogSnapshot = exposureCatalogSnapshotJson
            },
            signature = signatureJson
        };
        return JsonSerializer.Serialize(unified);
    }

    private static string BuildReleaseBundleRuntimeProjectionJson(
        IReadOnlyCollection<RuntimeRoute> runtimeRoutes,
        IReadOnlyCollection<ReleasePageSnapshotInfo> pageSnapshots,
        DateTimeOffset generatedAt)
    {
        var pageMap = BuildPageSnapshotMap(pageSnapshots);
        var runtimeProjection = new
        {
            generatedAt = generatedAt.ToString("O"),
            routes = runtimeRoutes
                .Select(item =>
                {
                    var hasPage = pageMap.TryGetValue(item.PageKey, out var page);
                    var routePath = hasPage && !string.IsNullOrWhiteSpace(page.RoutePath)
                        ? page.RoutePath
                        : $"/r/{item.AppKey}/{item.PageKey}";
                    return new
                    {
                        item.Id,
                        item.AppKey,
                        item.PageKey,
                        item.SchemaVersion,
                        item.IsActive,
                        item.EnvironmentCode,
                        title = hasPage ? page.Name : item.PageKey,
                        routePath,
                        icon = hasPage ? page.Icon : null,
                        sortOrder = hasPage ? page.SortOrder : 0
                    };
                })
                .OrderBy(item => item.sortOrder)
                .ThenBy(item => item.PageKey, StringComparer.OrdinalIgnoreCase)
                .ToArray()
        };
        return JsonSerializer.Serialize(runtimeProjection);
    }

    private static string BuildReleaseBundleRuntimeManifestSetJson(
        AppManifest manifest,
        IReadOnlyCollection<RuntimeRoute> runtimeRoutes,
        IReadOnlyCollection<ReleasePageSnapshotInfo> pageSnapshots,
        DateTimeOffset generatedAt)
    {
        var pageMap = BuildPageSnapshotMap(pageSnapshots);
        var payload = new
        {
            generatedAt = generatedAt.ToString("O"),
            manifest = new
            {
                manifest.Id,
                manifest.AppKey,
                manifest.Name,
                manifest.Version,
                manifest.Status
            },
            routes = runtimeRoutes
                .OrderBy(item => item.PageKey, StringComparer.OrdinalIgnoreCase)
                .Select(item =>
                {
                    var hasPage = pageMap.TryGetValue(item.PageKey, out var page);
                    return new
                    {
                        item.AppKey,
                        item.PageKey,
                        item.SchemaVersion,
                        item.IsActive,
                        item.EnvironmentCode,
                        title = hasPage ? page.Name : item.PageKey,
                        routePath = hasPage && !string.IsNullOrWhiteSpace(page.RoutePath)
                            ? page.RoutePath
                            : $"/r/{item.AppKey}/{item.PageKey}"
                    };
                })
                .ToArray()
        };
        return JsonSerializer.Serialize(payload);
    }

    private static string BuildReleaseBundleOrchestrationPlanSetJson(
        AppManifest manifest,
        IReadOnlyCollection<ReleaseOrchestrationPlanInfo> orchestrationPlans,
        DateTimeOffset generatedAt)
    {
        var payload = new
        {
            generatedAt = generatedAt.ToString("O"),
            manifestId = manifest.Id.ToString(),
            appKey = manifest.AppKey,
            plans = orchestrationPlans
                .OrderBy(item => item.PlanKey, StringComparer.OrdinalIgnoreCase)
                .Select(item => new
                {
                    id = item.Id.ToString(),
                    item.PlanKey,
                    item.PlanName,
                    item.Status,
                    item.PublishedVersion,
                    item.NodeGraphJson,
                    item.RuntimePolicyJson,
                    updatedAt = item.UpdatedAt.ToString("O")
                })
                .ToArray()
        };
        return JsonSerializer.Serialize(payload);
    }

    private static string BuildReleaseBundleToolReleaseRefsJson(
        AppManifest manifest,
        DateTimeOffset generatedAt)
    {
        var payload = new
        {
            generatedAt = generatedAt.ToString("O"),
            manifestId = manifest.Id.ToString(),
            appKey = manifest.AppKey,
            toolReleaseRefs = Array.Empty<object>()
        };
        return JsonSerializer.Serialize(payload);
    }

    private static string BuildReleaseBundleKnowledgeSnapshotRefsJson(
        AppManifest manifest,
        DateTimeOffset generatedAt)
    {
        var payload = new
        {
            generatedAt = generatedAt.ToString("O"),
            manifestId = manifest.Id.ToString(),
            appKey = manifest.AppKey,
            knowledgeSnapshotRefs = Array.Empty<object>()
        };
        return JsonSerializer.Serialize(payload);
    }

    private static string BuildReleaseBundleResourceBindingSnapshotJson(
        AppManifest manifest,
        AppExposurePolicy? exposurePolicy,
        DateTimeOffset generatedAt)
    {
        var payload = new
        {
            generatedAt = generatedAt.ToString("O"),
            manifestId = manifest.Id.ToString(),
            appKey = manifest.AppKey,
            dataSourceId = manifest.DataSourceId?.ToString(),
            binding = new
            {
                exposedDataSets = exposurePolicy?.ExposedDataSetsJson ?? "[]",
                allowedCommands = exposurePolicy?.AllowedCommandsJson ?? "[]"
            }
        };
        return JsonSerializer.Serialize(payload);
    }

    private static string BuildReleaseBundleSignatureJson(
        string runtimeManifestSetJson,
        string orchestrationPlanSetJson,
        string toolReleaseRefsJson,
        string knowledgeSnapshotRefsJson,
        string resourceBindingSnapshotJson,
        string navigationProjectionSnapshotJson,
        string exposureCatalogSnapshotJson,
        DateTimeOffset generatedAt)
    {
        var source = string.Join(
            "\n",
            runtimeManifestSetJson,
            orchestrationPlanSetJson,
            toolReleaseRefsJson,
            knowledgeSnapshotRefsJson,
            resourceBindingSnapshotJson,
            navigationProjectionSnapshotJson,
            exposureCatalogSnapshotJson);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        var checksum = Convert.ToHexString(bytes);
        return JsonSerializer.Serialize(new
        {
            generatedAt = generatedAt.ToString("O"),
            algorithm = "SHA256",
            checksum
        });
    }

    private static Dictionary<string, (string Name, string? RoutePath, string? Icon, int SortOrder)> BuildPageSnapshotMap(
        IReadOnlyCollection<ReleasePageSnapshotInfo> pageSnapshots)
    {
        return pageSnapshots
            .GroupBy(item => item.PageKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToDictionary(
                item => item.PageKey,
                item => (item.Name, item.RoutePath, item.Icon, item.SortOrder),
                StringComparer.OrdinalIgnoreCase);
    }

    private sealed record ReleasePageSnapshotInfo(
        string PageKey,
        string Name,
        string? RoutePath,
        string? Icon,
        int SortOrder);

    private sealed record ReleaseOrchestrationPlanInfo(
        long Id,
        string PlanKey,
        string PlanName,
        string Status,
        int PublishedVersion,
        string NodeGraphJson,
        string RuntimePolicyJson,
        DateTimeOffset UpdatedAt);
}

public sealed class RuntimeRouteQueryService : IRuntimeRouteQueryService
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ISqlSugarClient _mainDb;
    private readonly IApprovalOperationService _approvalOperationService;

    public RuntimeRouteQueryService(
        ISqlSugarClient db,
        IApprovalOperationService approvalOperationService)
    {
        _mainDb = db;
        _approvalOperationService = approvalOperationService;
    }

    public async Task<RuntimePageResponse?> GetRuntimePageAsync(TenantId tenantId, string appKey, string pageKey, CancellationToken cancellationToken = default)
    {
        var bundle = await GetActiveReleaseBundleByAppKeyAsync(tenantId, appKey, cancellationToken);
        if (bundle is not null)
        {
            var route = ParseRuntimeProjectionRoutes(bundle.RuntimeProjectionJson)
                .FirstOrDefault(item =>
                    string.Equals(item.AppKey, appKey, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(item.PageKey, pageKey, StringComparison.OrdinalIgnoreCase)
                    && item.IsActive);
            return route is null
                ? null
                : new RuntimePageResponse(route.AppKey, route.PageKey, route.SchemaVersion, route.IsActive);
        }

        var draftContext = await GetDraftRuntimeContextByAppKeyAsync(tenantId, appKey, cancellationToken)
            ?? throw new BusinessException(
                ErrorCodes.NotFound,
                $"应用 {appKey} 不存在。");
        return await GetDraftRuntimePageAsync(tenantId, draftContext.AppId, appKey, pageKey, cancellationToken);
    }

    public async Task<RuntimePageResponse?> GetRuntimePageAsync(
        TenantId tenantId,
        long appId,
        string appKey,
        string pageKey,
        CancellationToken cancellationToken = default)
    {
        if (appId > 0)
        {
            await EnsureRuntimeReadableAsync(tenantId, appId, cancellationToken);
        }

        return await GetRuntimePageAsync(tenantId, appKey, pageKey, cancellationToken);
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
        var bundle = await GetActiveReleaseBundleByAppKeyAsync(tenantId, appKey, cancellationToken);
        if (bundle is not null)
        {
            var releaseItems = ParseNavigationProjectionItems(bundle.NavigationProjectionSnapshotJson)
                .Where(item =>
                    string.Equals(item.AppKey, appKey, StringComparison.OrdinalIgnoreCase)
                    && item.IsActive)
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.PageKey, StringComparer.OrdinalIgnoreCase)
                .Select(item => new RuntimeMenuItem(
                    item.PageKey,
                    string.IsNullOrWhiteSpace(item.Title) ? item.PageKey : item.Title,
                    string.IsNullOrWhiteSpace(item.RoutePath) ? $"/r/{item.AppKey}/{item.PageKey}" : item.RoutePath,
                    item.Icon,
                    item.SortOrder))
                .ToArray();
            return new RuntimeMenuResponse(appKey, releaseItems);
        }

        var draftContext = await GetDraftRuntimeContextByAppKeyAsync(tenantId, appKey, cancellationToken)
            ?? throw new BusinessException(
                ErrorCodes.NotFound,
                $"应用 {appKey} 不存在。");
        var runtimeDb = await ResolveRuntimeDbByAppIdAsync(tenantId, draftContext.AppId, cancellationToken);
        var runtimeRoutes = await runtimeDb.Queryable<RuntimeRoute>()
            .Where(item =>
                item.TenantIdValue == tenantId.Value
                && item.ManifestId == draftContext.AppId
                && item.IsActive)
            .ToListAsync(cancellationToken);
        var items = runtimeRoutes
            .OrderBy(item => item.PageKey, StringComparer.OrdinalIgnoreCase)
            .Select(item => new RuntimeMenuItem(
                item.PageKey,
                item.PageKey,
                $"/r/{item.AppKey}/{item.PageKey}",
                null,
                0))
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.PageKey, StringComparer.OrdinalIgnoreCase)
            .ToArray();
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

    private async Task<ReleaseBundle?> GetActiveReleaseBundleByAppKeyAsync(
        TenantId tenantId,
        string appKey,
        CancellationToken cancellationToken)
    {
        var manifest = await _mainDb.Queryable<AppManifest>()
            .FirstAsync(
                item => item.TenantIdValue == tenantId.Value && item.AppKey == appKey,
                cancellationToken);
        if (manifest is null)
        {
            return null;
        }

        var activeRelease = await _mainDb.Queryable<AppRelease>()
            .Where(item =>
                item.TenantIdValue == tenantId.Value
                && item.ManifestId == manifest.Id
                && item.Status == AppReleaseStatus.Released)
            .OrderByDescending(item => item.ReleasedAt)
            .FirstAsync(cancellationToken);
        if (activeRelease is null)
        {
            return null;
        }

        return await _mainDb.Queryable<ReleaseBundle>()
            .FirstAsync(
                item => item.TenantIdValue == tenantId.Value && item.ReleaseId == activeRelease.Id,
                cancellationToken);
    }

    private async Task<AppManifest?> GetManifestByAppKeyAsync(
        TenantId tenantId,
        string appKey,
        CancellationToken cancellationToken)
    {
        return await _mainDb.Queryable<AppManifest>()
            .FirstAsync(
                item => item.TenantIdValue == tenantId.Value && item.AppKey == appKey,
                cancellationToken);
    }

    private async Task<DraftRuntimeContext?> GetDraftRuntimeContextByAppKeyAsync(
        TenantId tenantId,
        string appKey,
        CancellationToken cancellationToken)
    {
        var manifest = await GetManifestByAppKeyAsync(tenantId, appKey, cancellationToken);
        return manifest is null ? null : new DraftRuntimeContext(manifest.Id, appKey);
    }

    private async Task<RuntimePageResponse?> GetDraftRuntimePageAsync(
        TenantId tenantId,
        long appId,
        string appKey,
        string pageKey,
        CancellationToken cancellationToken)
    {
        var runtimeDb = await ResolveRuntimeDbByAppIdAsync(tenantId, appId, cancellationToken);
        var route = await runtimeDb.Queryable<RuntimeRoute>()
            .FirstAsync(
                item =>
                    item.TenantIdValue == tenantId.Value
                    && item.ManifestId == appId
                    && item.AppKey == appKey
                    && item.PageKey == pageKey
                    && item.IsActive,
                cancellationToken);
        return route is null
            ? null
            : new RuntimePageResponse(route.AppKey, route.PageKey, route.SchemaVersion, route.IsActive);
    }

    private Task<ISqlSugarClient> ResolveRuntimeDbByManifestIdAsync(
        TenantId tenantId,
        long manifestId,
        CancellationToken cancellationToken)
    {
        _ = tenantId;
        _ = manifestId;
        _ = cancellationToken;
        return Task.FromResult(_mainDb);
    }

    private static IReadOnlyList<RuntimeProjectionRouteSnapshot> ParseRuntimeProjectionRoutes(string? runtimeProjectionJson)
    {
        if (string.IsNullOrWhiteSpace(runtimeProjectionJson))
        {
            return Array.Empty<RuntimeProjectionRouteSnapshot>();
        }

        var snapshot = JsonSerializer.Deserialize<RuntimeProjectionSnapshot>(runtimeProjectionJson, SnapshotJsonOptions);
        return snapshot?.Routes ?? Array.Empty<RuntimeProjectionRouteSnapshot>();
    }

    private static IReadOnlyList<NavigationProjectionItemSnapshot> ParseNavigationProjectionItems(string? navigationProjectionSnapshotJson)
    {
        if (string.IsNullOrWhiteSpace(navigationProjectionSnapshotJson))
        {
            return Array.Empty<NavigationProjectionItemSnapshot>();
        }

        var snapshot = JsonSerializer.Deserialize<NavigationProjectionSnapshot>(navigationProjectionSnapshotJson, SnapshotJsonOptions);
        return snapshot?.Items ?? Array.Empty<NavigationProjectionItemSnapshot>();
    }

    private Task<ISqlSugarClient> ResolveRuntimeDbByAppKeyAsync(
        TenantId tenantId,
        string appKey,
        CancellationToken cancellationToken)
    {
        _ = tenantId;
        _ = appKey;
        _ = cancellationToken;
        return Task.FromResult(_mainDb);
    }

    private Task<ISqlSugarClient> ResolveRuntimeDbByAppIdAsync(
        TenantId tenantId,
        long appId,
        CancellationToken cancellationToken)
    {
        _ = tenantId;
        _ = appId;
        _ = cancellationToken;
        return Task.FromResult(_mainDb);
    }

    private Task EnsureRuntimeReadableAsync(
        TenantId tenantId,
        long appInstanceId,
        CancellationToken cancellationToken)
    {
        _ = tenantId;
        _ = appInstanceId;
        _ = cancellationToken;
        return Task.CompletedTask;
    }

    private sealed class RuntimeProjectionSnapshot
    {
        public RuntimeProjectionRouteSnapshot[] Routes { get; init; } = Array.Empty<RuntimeProjectionRouteSnapshot>();
    }

    private sealed class RuntimeProjectionRouteSnapshot
    {
        public string AppKey { get; init; } = string.Empty;
        public string PageKey { get; init; } = string.Empty;
        public int SchemaVersion { get; init; }
        public bool IsActive { get; init; }
    }

    private sealed class NavigationProjectionSnapshot
    {
        public NavigationProjectionItemSnapshot[] Items { get; init; } = Array.Empty<NavigationProjectionItemSnapshot>();
    }

    private sealed class NavigationProjectionItemSnapshot
    {
        public string AppKey { get; init; } = string.Empty;
        public string PageKey { get; init; } = string.Empty;
        public bool IsActive { get; init; }
        public string Title { get; init; } = string.Empty;
        public string RoutePath { get; init; } = string.Empty;
        public string? Icon { get; init; }
        public int SortOrder { get; init; }
    }

    private sealed record DraftRuntimeContext(
        long AppId,
        string AppKey);
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
