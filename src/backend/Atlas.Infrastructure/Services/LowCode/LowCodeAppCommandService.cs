using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using Atlas.Domain.LowCode.Enums;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.Options;
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
    private readonly ISqlSugarClient _db;
    private readonly TenantDataSourceRepository _tenantDataSourceRepository;
    private readonly DatabaseEncryptionOptions _databaseEncryptionOptions;

    public LowCodeAppCommandService(
        ILowCodeAppRepository appRepository,
        ILowCodePageRepository pageRepository,
        ILowCodeAppVersionRepository versionRepository,
        ILowCodePageVersionRepository pageVersionRepository,
        IIdGeneratorAccessor idGenerator,
        ISqlSugarClient db,
        TenantDataSourceRepository tenantDataSourceRepository,
        IOptions<DatabaseEncryptionOptions> databaseEncryptionOptions)
    {
        _appRepository = appRepository;
        _pageRepository = pageRepository;
        _versionRepository = versionRepository;
        _pageVersionRepository = pageVersionRepository;
        _idGenerator = idGenerator;
        _db = db;
        _tenantDataSourceRepository = tenantDataSourceRepository;
        _databaseEncryptionOptions = databaseEncryptionOptions.Value;
    }

    public async Task<long> CreateAsync(
        TenantId tenantId, long userId, LowCodeAppCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await _appRepository.ExistsByKeyAsync(tenantId, request.AppKey, cancellationToken: cancellationToken))
        {
            throw new InvalidOperationException($"应用标识 '{request.AppKey}' 已存在");
        }

        var id = _idGenerator.NextId();
        var now = DateTimeOffset.UtcNow;

        var entity = new LowCodeApp(
            tenantId, request.AppKey, request.Name,
            request.Description, request.Category, request.Icon,
            request.DataSourceId,
            request.UseSharedUsers,
            request.UseSharedRoles,
            request.UseSharedDepartments,
            userId, id, now);

        await _appRepository.InsertAsync(entity, cancellationToken);
        return id;
    }

    public async Task UpdateAsync(
        TenantId tenantId, long userId, long id, LowCodeAppUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _appRepository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new InvalidOperationException($"应用 ID={id} 不存在");

        var now = DateTimeOffset.UtcNow;
        entity.Update(request.Name, request.Description, request.Category, request.Icon, userId, now);

        await _appRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task UpdateSharingPolicyAsync(
        TenantId tenantId,
        long userId,
        long appId,
        AppSharingPolicyDto request,
        CancellationToken cancellationToken = default)
    {
        var app = await _appRepository.GetByIdAsync(tenantId, appId, cancellationToken)
            ?? throw new InvalidOperationException($"应用 ID={appId} 不存在");

        if ((!request.UseSharedUsers || !request.UseSharedRoles || !request.UseSharedDepartments) && !app.DataSourceId.HasValue)
        {
            throw new InvalidOperationException("启用独立基础数据策略前必须先绑定应用数据源。");
        }

        app.UpdateSharingPolicy(
            request.UseSharedUsers,
            request.UseSharedRoles,
            request.UseSharedDepartments,
            userId,
            DateTimeOffset.UtcNow);

        await _appRepository.UpdateAsync(app, cancellationToken);
    }

    public async Task UpdateEntityAliasesAsync(
        TenantId tenantId,
        long appId,
        AppEntityAliasUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var app = await _appRepository.GetByIdAsync(tenantId, appId, cancellationToken)
            ?? throw new InvalidOperationException($"应用 ID={appId} 不存在");

        var normalizedAliases = request.Aliases
            .GroupBy(x => x.EntityType.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .Select(alias => new AppEntityAlias(
                app.Id,
                alias.EntityType.Trim().ToLowerInvariant(),
                alias.SingularAlias.Trim(),
                string.IsNullOrWhiteSpace(alias.PluralAlias) ? null : alias.PluralAlias.Trim(),
                _idGenerator.NextId()))
            .ToList();

        var result = await _db.Ado.UseTranAsync(async () =>
        {
            await _db.Deleteable<AppEntityAlias>()
                .Where(x => x.AppId == app.Id)
                .ExecuteCommandAsync(cancellationToken);

            if (normalizedAliases.Count > 0)
            {
                await _db.Insertable(normalizedAliases).ExecuteCommandAsync(cancellationToken);
            }
        });

        if (!result.IsSuccess)
        {
            throw result.ErrorException ?? new InvalidOperationException("更新应用实体别名失败");
        }
    }

    public async Task<bool> TestAppDataSourceAsync(
        TenantId tenantId,
        long appId,
        CancellationToken cancellationToken = default)
    {
        var app = await _appRepository.GetByIdAsync(tenantId, appId, cancellationToken)
            ?? throw new InvalidOperationException($"应用 ID={appId} 不存在");

        if (!app.DataSourceId.HasValue)
        {
            return false;
        }

        var dataSource = await _tenantDataSourceRepository.FindByIdAsync(tenantId.Value.ToString(), app.DataSourceId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"数据源 ID={app.DataSourceId.Value} 不存在");

        string connectionString;
        try
        {
            connectionString = _databaseEncryptionOptions.Enabled
                ? TenantDbConnectionFactory.Decrypt(dataSource.EncryptedConnectionString, _databaseEncryptionOptions.Key)
                : dataSource.EncryptedConnectionString;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"数据源 ID={dataSource.Id} 连接字符串解密失败，请检查加密密钥配置。", ex);
        }

        var success = await TenantDbConnectionFactory.TestConnectionAsync(connectionString, dataSource.DbType, cancellationToken);
        dataSource.RecordTestResult(success);
        await _tenantDataSourceRepository.UpdateAsync(dataSource, cancellationToken);
        return success;
    }

    public async Task PublishAsync(
        TenantId tenantId, long userId, long id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _appRepository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new InvalidOperationException($"应用 ID={id} 不存在");
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
            throw result.ErrorException ?? new InvalidOperationException("发布应用失败");
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
            ?? throw new InvalidOperationException($"应用 ID={id} 不存在");
        var targetVersion = await _versionRepository.GetByIdAsync(tenantId, id, versionId, cancellationToken)
            ?? throw new InvalidOperationException($"应用版本 ID={versionId} 不存在");
        var snapshot = DeserializeSnapshot(targetVersion.SnapshotJson);
        if (!string.Equals(snapshot.App.AppKey, app.AppKey, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("版本快照与当前应用不匹配");
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
            throw result.ErrorException ?? new InvalidOperationException("回滚应用失败");
        }

        return rollbackVersion;
    }

    public async Task DisableAsync(
        TenantId tenantId, long userId, long id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _appRepository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new InvalidOperationException($"应用 ID={id} 不存在");

        var now = DateTimeOffset.UtcNow;
        entity.Disable(userId, now);

        await _appRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task EnableAsync(
        TenantId tenantId, long userId, long id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _appRepository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new InvalidOperationException($"应用 ID={id} 不存在");

        var now = DateTimeOffset.UtcNow;
        entity.Enable(userId, now);

        await _appRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeleteAsync(
        TenantId tenantId, long userId, long id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _appRepository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new InvalidOperationException($"应用 ID={id} 不存在");

        await _pageVersionRepository.DeleteByAppIdAsync(tenantId, id, cancellationToken);
        await _pageRepository.DeleteByAppIdAsync(tenantId, id, cancellationToken);
        await _appRepository.DeleteAsync(id, cancellationToken);
    }

    public async Task<LowCodeAppImportResult> ImportAsync(
        TenantId tenantId,
        long userId,
        LowCodeAppImportRequest request,
        CancellationToken cancellationToken = default)
    {
        var package = request.Package ?? throw new InvalidOperationException("导入包不能为空。");
        if (string.IsNullOrWhiteSpace(package.AppKey) || string.IsNullOrWhiteSpace(package.Name))
        {
            throw new InvalidOperationException("导入包缺少应用标识或名称。");
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
            true,
            true,
            true,
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

        await _appRepository.InsertAsync(app, cancellationToken);

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
            throw new InvalidOperationException("应用版本快照无效");
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
