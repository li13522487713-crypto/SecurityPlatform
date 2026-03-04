using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using Atlas.Domain.LowCode.Enums;
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
    private readonly IIdGeneratorAccessor _idGenerator;
    private readonly ISqlSugarClient _db;

    public LowCodeAppCommandService(
        ILowCodeAppRepository appRepository,
        ILowCodePageRepository pageRepository,
        ILowCodeAppVersionRepository versionRepository,
        IIdGeneratorAccessor idGenerator,
        ISqlSugarClient db)
    {
        _appRepository = appRepository;
        _pageRepository = pageRepository;
        _versionRepository = versionRepository;
        _idGenerator = idGenerator;
        _db = db;
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

        await _pageRepository.DeleteByAppIdAsync(tenantId, id, cancellationToken);
        await _appRepository.DeleteAsync(id, cancellationToken);
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

    private static LowCodePageType ParsePageType(string pageType)
    {
        if (Enum.TryParse<LowCodePageType>(pageType, true, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"页面类型 '{pageType}' 不支持");
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
