using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Application.System.Abstractions;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using Atlas.Domain.LowCode.Enums;
using Atlas.Domain.Platform.Entities;
using Atlas.Domain.System.Entities;
using SqlSugar;
using System.Text.Json;

namespace Atlas.Infrastructure.Services.LowCode;

public sealed class LowCodeAppCommandService : ILowCodeAppCommandService
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILowCodeAppRepository _appRepository;
    private readonly ILowCodePageRepository _pageRepository;
    private readonly ILowCodeAppVersionRepository _versionRepository;
    private readonly ILowCodePageVersionRepository _pageVersionRepository;
    private readonly IIdGeneratorAccessor _idGenerator;
    private readonly IAppDataSourceProvisioner _appDataSourceProvisioner;
    private readonly ISqlSugarClient _db;

    public LowCodeAppCommandService(
        ILowCodeAppRepository appRepository,
        ILowCodePageRepository pageRepository,
        ILowCodeAppVersionRepository versionRepository,
        ILowCodePageVersionRepository pageVersionRepository,
        IIdGeneratorAccessor idGenerator,
        IAppDataSourceProvisioner appDataSourceProvisioner,
        ISqlSugarClient db)
    {
        _appRepository = appRepository;
        _pageRepository = pageRepository;
        _versionRepository = versionRepository;
        _pageVersionRepository = pageVersionRepository;
        _idGenerator = idGenerator;
        _appDataSourceProvisioner = appDataSourceProvisioner;
        _db = db;
    }

    public async Task<long> CreateAsync(
        TenantId tenantId, long userId, LowCodeAppCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await _appRepository.ExistsByKeyAsync(tenantId, request.AppKey, cancellationToken: cancellationToken))
        {
            throw new BusinessException("LowCodeAppKeyExists", ErrorCodes.Conflict);
        }

        if (request.DataSourceId.HasValue)
        {
            var hasDataSource = await _db.Queryable<TenantDataSource>()
                .AnyAsync(
                    x => x.TenantIdValue == tenantId.Value.ToString() && x.Id == request.DataSourceId.Value,
                    cancellationToken);
            if (!hasDataSource)
            {
                throw new BusinessException($"Data source id={request.DataSourceId.Value} not found.", ErrorCodes.NotFound);
            }
        }

        var id = _idGenerator.NextId();
        var now = DateTimeOffset.UtcNow;

        var entity = new LowCodeApp(
            tenantId, request.AppKey, request.Name,
            request.Description, request.Category, request.Icon,
            request.DataSourceId,
            userId, id, now);

        var result = await _db.Ado.UseTranAsync(async () =>
        {
            await _appRepository.InsertAsync(entity, cancellationToken);
            var manifest = await EnsureAppManifestAsync(
                tenantId,
                userId,
                request.AppKey,
                request.Name,
                request.Description,
                request.Category,
                request.Icon,
                request.DataSourceId,
                now,
                cancellationToken);
            await EnsureTenantApplicationAsync(
                tenantId,
                userId,
                manifest.Id,
                entity,
                TenantApplicationStatus.Active,
                now,
                cancellationToken);
        });

        if (!result.IsSuccess)
        {
            throw result.ErrorException ?? new BusinessException("LowCodeAppCreateFailed", ErrorCodes.ValidationError);
        }

        await _appDataSourceProvisioner.EnsureProvisionedAsync(
            tenantId,
            id,
            request.AppKey,
            userId,
            request.DataSourceId,
            cancellationToken);

        return id;
    }

    public async Task UpdateAsync(
        TenantId tenantId, long userId, long id, LowCodeAppUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _appRepository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("LowCodeAppNotFound", ErrorCodes.NotFound);

        var targetDataSourceId = entity.DataSourceId;
        if (request.UnbindDataSource)
        {
            targetDataSourceId = null;
        }
        else if (request.DataSourceId.HasValue)
        {
            targetDataSourceId = request.DataSourceId.Value;
        }

        if (targetDataSourceId.HasValue)
        {
            var hasDataSource = await _db.Queryable<TenantDataSource>()
                .AnyAsync(
                    x => x.TenantIdValue == tenantId.Value.ToString() && x.Id == targetDataSourceId.Value,
                    cancellationToken);
            if (!hasDataSource)
            {
                throw new BusinessException($"Data source id={targetDataSourceId.Value} not found.", ErrorCodes.NotFound);
            }
        }

        var now = DateTimeOffset.UtcNow;
        entity.Update(
            request.Name,
            request.Description,
            request.Category,
            request.Icon,
            targetDataSourceId,
            userId,
            now);

        var result = await _db.Ado.UseTranAsync(async () =>
        {
            await _appRepository.UpdateAsync(entity, cancellationToken);
            var manifest = await EnsureAppManifestAsync(
                tenantId,
                userId,
                entity.AppKey,
                entity.Name,
                entity.Description,
                entity.Category,
                entity.Icon,
                entity.DataSourceId,
                now,
                cancellationToken);
            await EnsureTenantApplicationAsync(
                tenantId,
                userId,
                manifest.Id,
                entity,
                MapTenantApplicationStatus(entity.Status),
                now,
                cancellationToken);
        });

        if (!result.IsSuccess)
        {
            throw result.ErrorException ?? new BusinessException("LowCodeAppUpdateFailed", ErrorCodes.ValidationError);
        }

        await _appDataSourceProvisioner.EnsureProvisionedAsync(
            tenantId,
            id,
            entity.AppKey,
            userId,
            targetDataSourceId,
            cancellationToken);
    }

    public async Task PublishAsync(
        TenantId tenantId, long userId, long id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _appRepository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("LowCodeAppNotFound", ErrorCodes.NotFound);
        var pages = await _pageRepository.GetByAppIdAsync(tenantId, id, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var version = await _versionRepository.GetLatestVersionAsync(tenantId, id, cancellationToken) + 1;
        entity.Publish(version, userId, now);

        var snapshotJson = BuildSnapshotJson(entity, pages);
        var versionEntity = new LowCodeAppVersion(
            tenantId,
            id,
            version,
            snapshotJson,
            "Publish",
            userId,
            _idGenerator.NextId(),
            now);

        var result = await _db.Ado.UseTranAsync(async () =>
        {
            await _appRepository.UpdateAsync(entity, cancellationToken);
            await _versionRepository.InsertAsync(versionEntity, cancellationToken);
        });

        if (!result.IsSuccess)
        {
            throw result.ErrorException ?? new BusinessException("LowCodeAppPublishFailed", ErrorCodes.ValidationError);
        }
    }

    public async Task<int> RollbackAsync(
        TenantId tenantId,
        long userId,
        long id,
        long versionId,
        CancellationToken cancellationToken = default)
    {
        var app = await _appRepository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("LowCodeAppNotFound", ErrorCodes.NotFound);
        var targetVersion = await _versionRepository.GetByIdAsync(tenantId, id, versionId, cancellationToken)
            ?? throw new BusinessException("LowCodeAppVersionNotFound", ErrorCodes.NotFound);
        var snapshot = DeserializeSnapshot(targetVersion.SnapshotJson);
        if (!string.Equals(snapshot.App.AppKey, app.AppKey, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException("LowCodeAppVersionMismatch", ErrorCodes.ValidationError);
        }

        var now = DateTimeOffset.UtcNow;
        var rollbackVersion = await _versionRepository.GetLatestVersionAsync(tenantId, id, cancellationToken) + 1;
        app.RestoreSnapshot(
            snapshot.App.Name,
            snapshot.App.Description,
            snapshot.App.Category,
            snapshot.App.Icon,
            snapshot.App.ConfigJson,
            rollbackVersion,
            userId,
            now);

        var restoredPages = new List<LowCodePage>(snapshot.Pages.Count);
        foreach (var snapshotPage in snapshot.Pages.OrderBy(x => x.SortOrder))
        {
            var pageType = ParsePageType(snapshotPage.PageType);
            var page = new LowCodePage(
                tenantId,
                app.Id,
                snapshotPage.PageKey,
                snapshotPage.Name,
                pageType,
                snapshotPage.SchemaJson,
                snapshotPage.RoutePath,
                snapshotPage.Description,
                snapshotPage.Icon,
                snapshotPage.SortOrder,
                snapshotPage.ParentPageId,
                userId,
                snapshotPage.Id > 0 ? snapshotPage.Id : _idGenerator.NextId(),
                now);

            page.RestoreSnapshot(
                snapshotPage.Name,
                pageType,
                snapshotPage.SchemaJson,
                snapshotPage.RoutePath,
                snapshotPage.Description,
                snapshotPage.Icon,
                snapshotPage.SortOrder,
                snapshotPage.ParentPageId,
                snapshotPage.Version,
                snapshotPage.IsPublished,
                snapshotPage.PermissionCode,
                snapshotPage.DataTableKey,
                userId,
                now);
            restoredPages.Add(page);
        }

        var rollbackSnapshotJson = BuildSnapshotJson(app, restoredPages);
        var rollbackVersionEntity = new LowCodeAppVersion(
            tenantId,
            id,
            rollbackVersion,
            rollbackSnapshotJson,
            "Rollback",
            userId,
            _idGenerator.NextId(),
            now,
            targetVersion.Id,
            $"Rollback to version {targetVersion.Version}");

        var result = await _db.Ado.UseTranAsync(async () =>
        {
            await _appRepository.UpdateAsync(app, cancellationToken);
            await _pageRepository.DeleteByAppIdAsync(tenantId, id, cancellationToken);
            foreach (var page in restoredPages)
            {
                await _pageRepository.InsertAsync(page, cancellationToken);
            }

            await _versionRepository.InsertAsync(rollbackVersionEntity, cancellationToken);
        });

        if (!result.IsSuccess)
        {
            throw result.ErrorException ?? new BusinessException("LowCodeAppRollbackFailed", ErrorCodes.ValidationError);
        }

        return rollbackVersion;
    }

    public async Task DisableAsync(
        TenantId tenantId, long userId, long id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _appRepository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("LowCodeAppNotFound", ErrorCodes.NotFound);

        var now = DateTimeOffset.UtcNow;
        entity.Disable(userId, now);

        var result = await _db.Ado.UseTranAsync(async () =>
        {
            await _appRepository.UpdateAsync(entity, cancellationToken);
            await SyncTenantApplicationStatusAsync(
                tenantId,
                userId,
                entity.Id,
                TenantApplicationStatus.Disabled,
                now,
                cancellationToken);
        });

        if (!result.IsSuccess)
        {
            throw result.ErrorException ?? new BusinessException("LowCodeAppDisableFailed", ErrorCodes.ValidationError);
        }
    }

    public async Task EnableAsync(
        TenantId tenantId, long userId, long id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _appRepository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("LowCodeAppNotFound", ErrorCodes.NotFound);

        var now = DateTimeOffset.UtcNow;
        entity.Enable(userId, now);

        var result = await _db.Ado.UseTranAsync(async () =>
        {
            await _appRepository.UpdateAsync(entity, cancellationToken);
            await SyncTenantApplicationStatusAsync(
                tenantId,
                userId,
                entity.Id,
                TenantApplicationStatus.Active,
                now,
                cancellationToken);
        });

        if (!result.IsSuccess)
        {
            throw result.ErrorException ?? new BusinessException("LowCodeAppEnableFailed", ErrorCodes.ValidationError);
        }
    }

    public async Task UpdateEntityAliasesAsync(
        TenantId tenantId,
        long userId,
        long id,
        LowCodeAppEntityAliasesUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = await _appRepository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("LowCodeAppNotFound", ErrorCodes.NotFound);

        var now = DateTimeOffset.UtcNow;
        var tenantIdValue = tenantId.Value;
        var normalizedItems = request.Items
            .Where(x => !string.IsNullOrWhiteSpace(x.EntityType))
            .Select(x => new LowCodeAppEntityAliasItem(
                x.EntityType.Trim(),
                x.SingularAlias.Trim(),
                x.PluralAlias.Trim()))
            .GroupBy(x => x.EntityType, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .ToArray();

        var existingAliases = await _db.Queryable<AppEntityAlias>()
            .Where(x => x.TenantIdValue == tenantIdValue && x.AppId == id)
            .ToListAsync(cancellationToken);
        var existingMap = existingAliases.ToDictionary(x => x.EntityType, StringComparer.OrdinalIgnoreCase);

        var aliasesToInsert = new List<AppEntityAlias>();
        var aliasesToUpdate = new List<AppEntityAlias>();
        foreach (var item in normalizedItems)
        {
            if (existingMap.TryGetValue(item.EntityType, out var existing))
            {
                existing.UpdateAlias(item.SingularAlias, item.PluralAlias, userId, now);
                aliasesToUpdate.Add(existing);
                existingMap.Remove(item.EntityType);
                continue;
            }

            aliasesToInsert.Add(new AppEntityAlias(
                tenantId,
                id,
                item.EntityType,
                item.SingularAlias,
                item.PluralAlias,
                userId,
                _idGenerator.NextId(),
                now));
        }

        var removeIds = existingMap.Values.Select(x => x.Id).ToArray();
        var result = await _db.Ado.UseTranAsync(async () =>
        {
            if (aliasesToInsert.Count > 0)
            {
                await _db.Insertable(aliasesToInsert).ExecuteCommandAsync(cancellationToken);
            }

            if (aliasesToUpdate.Count > 0)
            {
                await _db.Updateable(aliasesToUpdate)
                    .WhereColumns(x => new { x.Id })
                    .UpdateColumns(x => new
                    {
                        x.SingularAlias,
                        x.PluralAlias,
                        x.UpdatedBy,
                        x.UpdatedAt
                    })
                    .ExecuteCommandAsync(cancellationToken);
            }

            if (removeIds.Length > 0)
            {
                await _db.Deleteable<AppEntityAlias>()
                    .Where(x => x.TenantIdValue == tenantIdValue && x.AppId == id && SqlFunc.ContainsArray(removeIds, x.Id))
                    .ExecuteCommandAsync(cancellationToken);
            }
        });

        if (!result.IsSuccess)
        {
            throw result.ErrorException ?? new BusinessException("LowCodeAppEntityAliasUpdateFailed", ErrorCodes.ValidationError);
        }
    }

    public async Task DeleteAsync(
        TenantId tenantId, long userId, long id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _appRepository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException("LowCodeAppNotFound", ErrorCodes.NotFound);

        await _pageVersionRepository.DeleteByAppIdAsync(tenantId, id, cancellationToken);
        await _pageRepository.DeleteByAppIdAsync(tenantId, id, cancellationToken);
        await _db.Deleteable<AppEntityAlias>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == id)
            .ExecuteCommandAsync(cancellationToken);
        await _db.Deleteable<TenantApplication>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.AppInstanceId == id)
            .ExecuteCommandAsync(cancellationToken);
        await _appRepository.DeleteAsync(id, cancellationToken);
    }

    public async Task<LowCodeAppImportResult> ImportAsync(
        TenantId tenantId,
        long userId,
        LowCodeAppImportRequest request,
        CancellationToken cancellationToken = default)
    {
        var package = request.Package ?? throw new BusinessException("LowCodeImportPackageEmpty", ErrorCodes.ValidationError);
        if (string.IsNullOrWhiteSpace(package.AppKey) || string.IsNullOrWhiteSpace(package.Name))
        {
            throw new BusinessException("LowCodeImportPackageMissingInfo", ErrorCodes.ValidationError);
        }

        var strategy = NormalizeConflictStrategy(request.ConflictStrategy);
        var targetAppKey = package.AppKey.Trim();
        var existing = await _appRepository.GetByKeyAsync(tenantId, targetAppKey, cancellationToken);
        var overwritten = false;

        if (existing is not null && string.Equals(strategy, "Skip", StringComparison.OrdinalIgnoreCase))
        {
            return new LowCodeAppImportResult(
                existing.Id.ToString(),
                existing.AppKey,
                true,
                false,
                0,
                0);
        }

        if (existing is not null && string.Equals(strategy, "Overwrite", StringComparison.OrdinalIgnoreCase))
        {
            await _pageVersionRepository.DeleteByAppIdAsync(tenantId, existing.Id, cancellationToken);
            await _pageRepository.DeleteByAppIdAsync(tenantId, existing.Id, cancellationToken);
            await _db.Deleteable<AppEntityAlias>()
                .Where(x => x.TenantIdValue == tenantId.Value && x.AppId == existing.Id)
                .ExecuteCommandAsync(cancellationToken);
            await _appRepository.DeleteAsync(existing.Id, cancellationToken);
            overwritten = true;
            existing = null;
        }

        if (existing is not null && string.Equals(strategy, "Rename", StringComparison.OrdinalIgnoreCase))
        {
            targetAppKey = await ResolveAvailableAppKeyAsync(
                tenantId,
                package.AppKey.Trim(),
                request.KeySuffix,
                cancellationToken);
        }

        var now = DateTimeOffset.UtcNow;
        var appId = _idGenerator.NextId();
        var app = new LowCodeApp(
            tenantId,
            targetAppKey,
            package.Name,
            package.Description,
            package.Category,
            package.Icon,
            null,
            userId,
            appId,
            now);
        if (!string.IsNullOrWhiteSpace(package.ConfigJson))
        {
            app.UpdateConfig(package.ConfigJson!, userId, now);
        }
        if (string.Equals(package.Status, LowCodeAppStatus.Published.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            app.Publish(userId, now);
        }
        else if (string.Equals(package.Status, LowCodeAppStatus.Disabled.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            app.Disable(userId, now);
        }
        else if (string.Equals(package.Status, LowCodeAppStatus.Archived.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            app.Archive(userId, now);
        }

        var manifest = await EnsureAppManifestAsync(
            tenantId,
            userId,
            targetAppKey,
            package.Name,
            package.Description,
            package.Category,
            package.Icon,
            null,
            now,
            cancellationToken);
        await _appRepository.InsertAsync(app, cancellationToken);
        await EnsureTenantApplicationAsync(
            tenantId,
            userId,
            manifest.Id,
            app,
            MapTenantApplicationStatus(app.Status),
            now,
            cancellationToken);

        var pagePackages = package.Pages ?? Array.Empty<LowCodeAppExportPagePackage>();
        var pageIdMap = pagePackages
            .Select((item, index) => new { item.Id, NewId = _idGenerator.NextId(), Index = index })
            .ToDictionary(x => BuildSourcePageKey(x.Id, x.Index), x => x.NewId, StringComparer.OrdinalIgnoreCase);

        var pageEntities = new List<LowCodePage>(pagePackages.Count);
        for (var i = 0; i < pagePackages.Count; i++)
        {
            var item = pagePackages[i];
            var sourceKey = BuildSourcePageKey(item.Id, i);
            var pageId = pageIdMap[sourceKey];
            var parentPageId = ResolveParentPageId(item.ParentPageId, pageIdMap);
            var pageType = ParsePageType(item.PageType);
            var pageEntity = new LowCodePage(
                tenantId,
                appId,
                item.PageKey,
                item.Name,
                pageType,
                item.SchemaJson,
                item.RoutePath,
                item.Description,
                item.Icon,
                item.SortOrder,
                parentPageId,
                userId,
                pageId,
                now);
            pageEntity.SetPermission(item.PermissionCode, userId, now);
            pageEntity.BindDataTable(item.DataTableKey, userId, now);
            if (item.IsPublished)
            {
                pageEntity.Publish(userId, now);
            }

            pageEntities.Add(pageEntity);
        }

        await _pageRepository.AddRangeAsync(pageEntities, cancellationToken);

        var pageEntityMap = pageEntities.ToDictionary(x => x.Id);
        var versionPackages = package.PageVersions ?? Array.Empty<LowCodeAppExportPageVersionPackage>();
        var versionEntities = new List<LowCodePageVersion>(versionPackages.Count);
        foreach (var version in versionPackages)
        {
            var mappedPageId = ResolveMappedPageId(version.PageId, pageIdMap);
            if (!mappedPageId.HasValue || !pageEntityMap.TryGetValue(mappedPageId.Value, out var pageEntity))
            {
                continue;
            }

            versionEntities.Add(new LowCodePageVersion(
                tenantId,
                pageEntity.Id,
                appId,
                version.SnapshotVersion,
                pageEntity.PageKey,
                version.Name,
                ParsePageType(version.PageType),
                version.SchemaJson,
                version.RoutePath,
                version.Description,
                version.Icon,
                version.SortOrder,
                ResolveParentPageId(version.ParentPageId, pageIdMap),
                version.PermissionCode,
                version.DataTableKey,
                userId,
                _idGenerator.NextId(),
                now));
        }

        await _pageVersionRepository.AddRangeAsync(versionEntities, cancellationToken);

        await _appDataSourceProvisioner.EnsureProvisionedAsync(
            tenantId,
            appId,
            targetAppKey,
            userId,
            preferredDataSourceId: null,
            cancellationToken);

        return new LowCodeAppImportResult(
            appId.ToString(),
            targetAppKey,
            false,
            overwritten,
            pageEntities.Count,
            versionEntities.Count);
    }

    private static string NormalizeConflictStrategy(string? strategy)
    {
        if (string.Equals(strategy, "Skip", StringComparison.OrdinalIgnoreCase))
        {
            return "Skip";
        }

        if (string.Equals(strategy, "Overwrite", StringComparison.OrdinalIgnoreCase))
        {
            return "Overwrite";
        }

        return "Rename";
    }

    private async Task<string> ResolveAvailableAppKeyAsync(
        TenantId tenantId,
        string appKey,
        string? keySuffix,
        CancellationToken cancellationToken)
    {
        var suffix = string.IsNullOrWhiteSpace(keySuffix) ? "import" : keySuffix.Trim();
        var candidate = $"{appKey}-{suffix}";
        var index = 1;
        while (await _appRepository.ExistsByKeyAsync(tenantId, candidate, cancellationToken: cancellationToken))
        {
            candidate = $"{appKey}-{suffix}-{index}";
            index++;
        }

        return candidate;
    }

    private static LowCodePageType ParsePageType(string pageType)
    {
        return Enum.TryParse<LowCodePageType>(pageType, true, out var result)
            ? result
            : LowCodePageType.Blank;
    }

    /// <summary>
    /// 构建用于 pageIdMap 的源页面键。支持数字 ID 或 GUID 等任意字符串格式。
    /// </summary>
    private static string BuildSourcePageKey(string? sourceId, int index)
    {
        return !string.IsNullOrWhiteSpace(sourceId) ? sourceId.Trim() : $"index-{index}";
    }

    private static long? ResolveMappedPageId(string? sourcePageId, IReadOnlyDictionary<string, long> pageIdMap)
    {
        if (string.IsNullOrWhiteSpace(sourcePageId))
        {
            return null;
        }

        var key = sourcePageId.Trim();
        return pageIdMap.TryGetValue(key, out var mapped) ? mapped : null;
    }

    private static long? ResolveParentPageId(string? parentSourceId, IReadOnlyDictionary<string, long> pageIdMap)
    {
        if (string.IsNullOrWhiteSpace(parentSourceId))
        {
            return null;
        }

        var key = parentSourceId.Trim();
        return pageIdMap.TryGetValue(key, out var mapped) ? mapped : null;
    }

    private static LowCodeAppSnapshot DeserializeSnapshot(string snapshotJson)
    {
        var snapshot = JsonSerializer.Deserialize<LowCodeAppSnapshot>(snapshotJson, SnapshotJsonOptions);
        if (snapshot is null)
        {
            throw new BusinessException("LowCodeAppVersionSnapshotInvalid", ErrorCodes.ValidationError);
        }

        return snapshot;
    }

    private static string BuildSnapshotJson(LowCodeApp app, IReadOnlyList<LowCodePage> pages)
    {
        var snapshot = new LowCodeAppSnapshot(
            new AppSnapshot(
                app.AppKey,
                app.Name,
                app.Description,
                app.Category,
                app.Icon,
                app.ConfigJson),
            pages
                .Select(page => new PageSnapshot(
                    page.Id,
                    page.PageKey,
                    page.Name,
                    page.PageType.ToString(),
                    page.SchemaJson,
                    page.RoutePath,
                    page.Description,
                    page.Icon,
                    page.SortOrder,
                    page.ParentPageId,
                    page.Version,
                    page.IsPublished,
                    page.PermissionCode,
                    page.DataTableKey))
                .OrderBy(x => x.SortOrder)
                .ToList());

        return JsonSerializer.Serialize(snapshot, SnapshotJsonOptions);
    }

    private async Task<AppManifest> EnsureAppManifestAsync(
        TenantId tenantId,
        long userId,
        string appKey,
        string name,
        string? description,
        string? category,
        string? icon,
        long? dataSourceId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var manifest = await _db.Queryable<AppManifest>()
            .FirstAsync(
                x => x.TenantIdValue == tenantId.Value && x.AppKey == appKey,
                cancellationToken);

        if (manifest is null)
        {
            manifest = new AppManifest(tenantId, _idGenerator.NextId(), appKey, name, userId, now);
            manifest.Update(name, description, category, icon, dataSourceId, userId, now);
            await _db.Insertable(manifest).ExecuteCommandAsync(cancellationToken);
            return manifest;
        }

        manifest.Update(name, description, category, icon, dataSourceId, userId, now);
        await _db.Updateable(manifest).ExecuteCommandAsync(cancellationToken);
        return manifest;
    }

    private async Task EnsureTenantApplicationAsync(
        TenantId tenantId,
        long userId,
        long catalogId,
        LowCodeApp app,
        TenantApplicationStatus status,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var relation = await _db.Queryable<TenantApplication>()
            .FirstAsync(
                row => row.TenantIdValue == tenantId.Value && row.AppInstanceId == app.Id,
                cancellationToken);
        if (relation is null)
        {
            relation = new TenantApplication(
                tenantId,
                _idGenerator.NextId(),
                catalogId,
                app.Id,
                app.AppKey,
                app.Name,
                app.DataSourceId,
                userId,
                now);
            relation.SyncWithInstance(
                catalogId,
                app.Id,
                app.AppKey,
                app.Name,
                app.DataSourceId,
                status,
                userId,
                now);
            await _db.Insertable(relation).ExecuteCommandAsync(cancellationToken);
            return;
        }

        relation.SyncWithInstance(
            catalogId,
            app.Id,
            app.AppKey,
            app.Name,
            app.DataSourceId,
            status,
            userId,
            now);
        await _db.Updateable(relation).ExecuteCommandAsync(cancellationToken);
    }

    private async Task SyncTenantApplicationStatusAsync(
        TenantId tenantId,
        long userId,
        long appInstanceId,
        TenantApplicationStatus status,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var relation = await _db.Queryable<TenantApplication>()
            .FirstAsync(
                row => row.TenantIdValue == tenantId.Value && row.AppInstanceId == appInstanceId,
                cancellationToken);
        if (relation is null)
        {
            return;
        }

        if (status == TenantApplicationStatus.Disabled)
        {
            relation.Disable(userId, now);
        }
        else if (status == TenantApplicationStatus.Active)
        {
            relation.Enable(userId, now);
        }
        else
        {
            relation.SyncWithInstance(
                relation.CatalogId,
                relation.AppInstanceId,
                relation.AppKey,
                relation.Name,
                relation.DataSourceId,
                status,
                userId,
                now);
        }

        await _db.Updateable(relation).ExecuteCommandAsync(cancellationToken);
    }

    private static TenantApplicationStatus MapTenantApplicationStatus(LowCodeAppStatus appStatus)
    {
        return appStatus switch
        {
            LowCodeAppStatus.Disabled => TenantApplicationStatus.Disabled,
            LowCodeAppStatus.Archived => TenantApplicationStatus.Archived,
            _ => TenantApplicationStatus.Active
        };
    }

    private sealed record LowCodeAppSnapshot(AppSnapshot App, IReadOnlyList<PageSnapshot> Pages);

    private sealed record AppSnapshot(
        string AppKey,
        string Name,
        string? Description,
        string? Category,
        string? Icon,
        string? ConfigJson);

    private sealed record PageSnapshot(
        long Id,
        string PageKey,
        string Name,
        string PageType,
        string SchemaJson,
        string? RoutePath,
        string? Description,
        string? Icon,
        int SortOrder,
        long? ParentPageId,
        int Version,
        bool IsPublished,
        string? PermissionCode,
        string? DataTableKey);
}
