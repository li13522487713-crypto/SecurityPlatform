using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using Atlas.Domain.LowCode.Enums;

namespace Atlas.Infrastructure.Services.LowCode;

public sealed class LowCodeAppCommandService : ILowCodeAppCommandService
{
    private readonly ILowCodeAppRepository _appRepository;
    private readonly ILowCodePageRepository _pageRepository;
    private readonly ILowCodePageVersionRepository _pageVersionRepository;
    private readonly IIdGeneratorAccessor _idGenerator;

    public LowCodeAppCommandService(
        ILowCodeAppRepository appRepository,
        ILowCodePageRepository pageRepository,
        ILowCodePageVersionRepository pageVersionRepository,
        IIdGeneratorAccessor idGenerator)
    {
        _appRepository = appRepository;
        _pageRepository = pageRepository;
        _pageVersionRepository = pageVersionRepository;
        _idGenerator = idGenerator;
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

    public async Task PublishAsync(
        TenantId tenantId, long userId, long id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _appRepository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new InvalidOperationException($"应用 ID={id} 不存在");

        var now = DateTimeOffset.UtcNow;
        entity.Publish(userId, now);

        await _appRepository.UpdateAsync(entity, cancellationToken);
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

        await _pageVersionRepository.DeleteByAppIdAsync(id, cancellationToken);
        await _pageRepository.DeleteByAppIdAsync(id, cancellationToken);
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
            await _pageVersionRepository.DeleteByAppIdAsync(existing.Id, cancellationToken);
            await _pageRepository.DeleteByAppIdAsync(existing.Id, cancellationToken);
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
            var sourcePageId = ResolveSourcePageId(version.PageId);
            if (sourcePageId is null)
            {
                continue;
            }

            var mappedPageId = ResolveMappedPageId(sourcePageId.Value, pageIdMap);
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

    private static string BuildSourcePageKey(string? sourceId, int index)
    {
        var normalized = ResolveSourcePageId(sourceId);
        return normalized.HasValue ? normalized.Value.ToString() : $"index-{index}";
    }

    private static long? ResolveSourcePageId(string? sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            return null;
        }

        return long.TryParse(sourceId, out var parsed) ? parsed : null;
    }

    private static long? ResolveMappedPageId(long sourcePageId, IReadOnlyDictionary<string, long> pageIdMap)
    {
        var key = sourcePageId.ToString();
        return pageIdMap.TryGetValue(key, out var mapped) ? mapped : null;
    }

    private static long? ResolveParentPageId(string? parentSourceId, IReadOnlyDictionary<string, long> pageIdMap)
    {
        var sourceParentId = ResolveSourcePageId(parentSourceId);
        if (!sourceParentId.HasValue)
        {
            return null;
        }

        return pageIdMap.TryGetValue(sourceParentId.Value.ToString(), out var mapped) ? mapped : null;
    }
}
