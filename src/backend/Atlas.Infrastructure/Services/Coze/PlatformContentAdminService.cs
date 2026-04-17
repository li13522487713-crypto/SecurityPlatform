using Atlas.Application.Coze.Abstractions;
using Atlas.Application.Coze.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.Coze;

/// <summary>
/// 平台运营内容 CRUD 实现。
///
/// 安全基线：
/// - 所有 Slot 必须在 <see cref="PlatformContentSlots.All"/> 白名单内，阻断随意写入。
/// - ContentJson 长度软上限 32 KB，避免误写超大 payload 影响首页渲染。
/// - Slot + ContentKey 不强制唯一（允许同一 Slot 多条），由运营人员控制 OrderIndex。
/// </summary>
public sealed class PlatformContentAdminService : IPlatformContentAdminService
{
    private const int MaxContentJsonLength = 32 * 1024;

    private readonly PlatformContentRepository _repository;
    private readonly IIdGeneratorAccessor _idGenerator;

    public PlatformContentAdminService(
        PlatformContentRepository repository,
        IIdGeneratorAccessor idGenerator)
    {
        _repository = repository;
        _idGenerator = idGenerator;
    }

    public async Task<IReadOnlyList<PlatformContentItemDto>> ListAsync(
        TenantId tenantId,
        string? slot,
        bool onlyActive,
        CancellationToken cancellationToken)
    {
        var entities = await _repository.ListAsync(tenantId, slot, onlyActive, cancellationToken);
        return entities.Select(MapToDto).ToArray();
    }

    public async Task<string> CreateAsync(
        TenantId tenantId,
        PlatformContentCreateRequest request,
        CancellationToken cancellationToken)
    {
        ValidateSlot(request.Slot);
        ValidateContentJson(request.ContentJson);

        var entity = new PlatformContent(
            tenantId,
            slot: request.Slot.Trim().ToLowerInvariant(),
            contentKey: request.ContentKey.Trim(),
            contentJson: request.ContentJson,
            tag: request.Tag?.Trim() ?? string.Empty,
            orderIndex: request.OrderIndex,
            publishedAt: request.PublishedAt ?? DateTimeOffset.UtcNow,
            id: _idGenerator.NextId());

        // 默认创建即上架；若客户端显式传 IsActive=false 则下架保存。
        if (request.IsActive == false)
        {
            entity.Update(
                entity.ContentJson,
                entity.Tag,
                entity.OrderIndex,
                isActive: false,
                entity.PublishedAt);
        }

        await _repository.AddAsync(entity, cancellationToken);
        return entity.Id.ToString();
    }

    public async Task UpdateAsync(
        TenantId tenantId,
        string id,
        PlatformContentUpdateRequest request,
        CancellationToken cancellationToken)
    {
        ValidateContentJson(request.ContentJson);

        var entity = await LoadOrThrowAsync(tenantId, id, cancellationToken);
        entity.Update(
            contentJson: request.ContentJson,
            tag: request.Tag?.Trim() ?? string.Empty,
            orderIndex: request.OrderIndex,
            isActive: request.IsActive,
            publishedAt: request.PublishedAt ?? entity.PublishedAt);
        await _repository.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeleteAsync(
        TenantId tenantId,
        string id,
        CancellationToken cancellationToken)
    {
        var entity = await LoadOrThrowAsync(tenantId, id, cancellationToken);
        await _repository.DeleteAsync(entity, cancellationToken);
    }

    private async Task<PlatformContent> LoadOrThrowAsync(
        TenantId tenantId,
        string id,
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(id, out var parsed))
        {
            throw new BusinessException(ErrorCodes.NotFound, "PlatformContentNotFound");
        }

        var entity = await _repository.FindAsync(tenantId, parsed, cancellationToken);
        if (entity is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "PlatformContentNotFound");
        }
        return entity;
    }

    private static void ValidateSlot(string slot)
    {
        if (string.IsNullOrWhiteSpace(slot) || !PlatformContentSlots.All.Contains(slot.Trim().ToLowerInvariant()))
        {
            throw new BusinessException(ErrorCodes.ValidationError, "PlatformContentSlotInvalid");
        }
    }

    private static void ValidateContentJson(string contentJson)
    {
        if (string.IsNullOrWhiteSpace(contentJson))
        {
            throw new BusinessException(ErrorCodes.ValidationError, "PlatformContentJsonRequired");
        }
        if (contentJson.Length > MaxContentJsonLength)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "PlatformContentJsonTooLong");
        }
    }

    private static PlatformContentItemDto MapToDto(PlatformContent entity)
    {
        return new PlatformContentItemDto(
            Id: entity.Id.ToString(),
            Slot: entity.Slot,
            ContentKey: entity.ContentKey,
            ContentJson: entity.ContentJson,
            Tag: string.IsNullOrEmpty(entity.Tag) ? null : entity.Tag,
            OrderIndex: entity.OrderIndex,
            IsActive: entity.IsActive,
            PublishedAt: entity.PublishedAt,
            CreatedAt: new DateTimeOffset(DateTime.SpecifyKind(entity.CreatedAt, DateTimeKind.Utc)),
            UpdatedAt: entity.UpdatedAt is null
                ? null
                : new DateTimeOffset(DateTime.SpecifyKind(entity.UpdatedAt.Value, DateTimeKind.Utc)));
    }
}
