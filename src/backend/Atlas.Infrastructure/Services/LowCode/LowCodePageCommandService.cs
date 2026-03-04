using Atlas.Application.LowCode.Abstractions;
using Atlas.Application.LowCode.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;
using Atlas.Domain.LowCode.Entities;
using Atlas.Domain.LowCode.Enums;

namespace Atlas.Infrastructure.Services.LowCode;

public sealed class LowCodePageCommandService : ILowCodePageCommandService
{
    private readonly ILowCodePageRepository _pageRepository;
    private readonly ILowCodePageVersionRepository _pageVersionRepository;
    private readonly ILowCodeAppRepository _appRepository;
    private readonly IIdGeneratorAccessor _idGenerator;

    public LowCodePageCommandService(
        ILowCodePageRepository pageRepository,
        ILowCodePageVersionRepository pageVersionRepository,
        ILowCodeAppRepository appRepository,
        IIdGeneratorAccessor idGenerator)
    {
        _pageRepository = pageRepository;
        _pageVersionRepository = pageVersionRepository;
        _appRepository = appRepository;
        _idGenerator = idGenerator;
    }

    public async Task<long> CreateAsync(
        TenantId tenantId, long userId, long appId, LowCodePageCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var app = await _appRepository.GetByIdAsync(tenantId, appId, cancellationToken)
            ?? throw new InvalidOperationException($"应用 ID={appId} 不存在");

        if (await _pageRepository.ExistsByKeyAsync(tenantId, appId, request.PageKey, cancellationToken: cancellationToken))
        {
            throw new InvalidOperationException($"页面标识 '{request.PageKey}' 在该应用中已存在");
        }

        var pageType = Enum.Parse<LowCodePageType>(request.PageType, ignoreCase: true);
        var id = _idGenerator.NextId();
        var now = DateTimeOffset.UtcNow;

        var entity = new LowCodePage(
            tenantId, appId, request.PageKey, request.Name,
            pageType, request.SchemaJson, request.RoutePath,
            request.Description, request.Icon, request.SortOrder,
            request.ParentPageId, userId, id, now);

        if (!string.IsNullOrWhiteSpace(request.PermissionCode))
        {
            entity.SetPermission(request.PermissionCode, userId, now);
        }

        if (!string.IsNullOrWhiteSpace(request.DataTableKey))
        {
            entity.BindDataTable(request.DataTableKey, userId, now);
        }

        await _pageRepository.InsertAsync(entity, cancellationToken);
        return id;
    }

    public async Task UpdateAsync(
        TenantId tenantId, long userId, long id, LowCodePageUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _pageRepository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new InvalidOperationException($"页面 ID={id} 不存在");

        var pageType = Enum.Parse<LowCodePageType>(request.PageType, ignoreCase: true);
        var now = DateTimeOffset.UtcNow;

        entity.Update(
            request.Name, pageType, request.SchemaJson, request.RoutePath,
            request.Description, request.Icon, request.SortOrder,
            request.ParentPageId, userId, now);

        entity.SetPermission(request.PermissionCode, userId, now);
        entity.BindDataTable(request.DataTableKey, userId, now);

        await _pageRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task UpdateSchemaAsync(
        TenantId tenantId, long userId, long id, LowCodePageSchemaUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _pageRepository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new InvalidOperationException($"页面 ID={id} 不存在");

        var now = DateTimeOffset.UtcNow;
        entity.UpdateSchema(request.SchemaJson, userId, now);

        await _pageRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task PublishAsync(
        TenantId tenantId, long userId, long id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _pageRepository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new InvalidOperationException($"页面 ID={id} 不存在");

        var now = DateTimeOffset.UtcNow;
        entity.Publish(userId, now);

        await _pageVersionRepository.InsertAsync(
            new LowCodePageVersion(
                tenantId,
                entity.Id,
                entity.AppId,
                entity.Version,
                entity.PageKey,
                entity.Name,
                entity.PageType,
                entity.SchemaJson,
                entity.RoutePath,
                entity.Description,
                entity.Icon,
                entity.SortOrder,
                entity.ParentPageId,
                entity.PermissionCode,
                entity.DataTableKey,
                userId,
                _idGenerator.NextId(),
                now),
            cancellationToken);

        await _pageRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task UnpublishAsync(
        TenantId tenantId, long userId, long id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _pageRepository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new InvalidOperationException($"页面 ID={id} 不存在");

        var now = DateTimeOffset.UtcNow;
        entity.Unpublish(userId, now);

        await _pageRepository.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeleteAsync(
        TenantId tenantId, long userId, long id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _pageRepository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new InvalidOperationException($"页面 ID={id} 不存在");

        await _pageVersionRepository.DeleteByPageIdAsync(tenantId, entity.Id, cancellationToken);
        await _pageRepository.DeleteAsync(id, cancellationToken);
    }

    public async Task RollbackAsync(
        TenantId tenantId,
        long userId,
        long id,
        long versionId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _pageRepository.GetByIdAsync(tenantId, id, cancellationToken)
            ?? throw new InvalidOperationException($"页面 ID={id} 不存在");
        var version = await _pageVersionRepository.GetByIdAsync(tenantId, versionId, cancellationToken)
            ?? throw new InvalidOperationException($"页面版本 ID={versionId} 不存在");
        if (version.PageId != id)
        {
            throw new InvalidOperationException("目标版本不属于当前页面。");
        }

        var now = DateTimeOffset.UtcNow;
        entity.RollbackToVersion(
            version.Name,
            version.PageType,
            version.SchemaJson,
            version.RoutePath,
            version.Description,
            version.Icon,
            version.SortOrder,
            version.ParentPageId,
            version.PermissionCode,
            version.DataTableKey,
            userId,
            now);

        await _pageRepository.UpdateAsync(entity, cancellationToken);
        await _pageVersionRepository.InsertAsync(
            new LowCodePageVersion(
                tenantId,
                entity.Id,
                entity.AppId,
                entity.Version,
                entity.PageKey,
                entity.Name,
                entity.PageType,
                entity.SchemaJson,
                entity.RoutePath,
                entity.Description,
                entity.Icon,
                entity.SortOrder,
                entity.ParentPageId,
                entity.PermissionCode,
                entity.DataTableKey,
                userId,
                _idGenerator.NextId(),
                now),
            cancellationToken);
    }
}
