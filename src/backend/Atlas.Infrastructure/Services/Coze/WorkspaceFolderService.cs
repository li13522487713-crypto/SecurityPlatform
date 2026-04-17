using System.Text.Json;
using Atlas.Application.Coze.Abstractions;
using Atlas.Application.Coze.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.Coze;

public sealed class WorkspaceFolderService : IWorkspaceFolderService
{
    private static readonly HashSet<string> AllowedItemTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "agent",
        "app",
        "project"
    };

    private readonly WorkspaceFolderRepository _repository;
    private readonly WorkspaceFolderItemRepository _itemRepository;
    private readonly IIdGeneratorAccessor _idGenerator;

    public WorkspaceFolderService(
        WorkspaceFolderRepository repository,
        WorkspaceFolderItemRepository itemRepository,
        IIdGeneratorAccessor idGenerator)
    {
        _repository = repository;
        _itemRepository = itemRepository;
        _idGenerator = idGenerator;
    }

    public async Task<PagedResult<WorkspaceFolderListItem>> ListAsync(
        TenantId tenantId,
        string workspaceId,
        string? keyword,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken)
    {
        var pageIndex = Math.Max(1, pagedRequest.PageIndex);
        var pageSize = Math.Clamp(pagedRequest.PageSize, 1, 100);

        var (entities, total) = await _repository.SearchAsync(
            tenantId,
            workspaceId,
            keyword,
            pageIndex,
            pageSize,
            cancellationToken);

        // 批量统计每个文件夹的 ItemCount（一次查库，避免循环内查库）。
        var folderIds = entities.Select(e => e.Id).ToArray();
        var counts = await _itemRepository.CountByFolderIdsAsync(
            tenantId,
            workspaceId,
            folderIds,
            cancellationToken);

        return new PagedResult<WorkspaceFolderListItem>(
            entities.Select(entity => ToDto(entity, counts.TryGetValue(entity.Id, out var c) ? c : entity.ItemCount)).ToArray(),
            total,
            pageIndex,
            pageSize);
    }

    public async Task<string> CreateAsync(
        TenantId tenantId,
        string workspaceId,
        CurrentUserInfo currentUser,
        WorkspaceFolderCreateRequest request,
        CancellationToken cancellationToken)
    {
        ValidateName(request.Name);

        var displayName = string.IsNullOrWhiteSpace(currentUser.DisplayName)
            ? currentUser.Username ?? string.Empty
            : currentUser.DisplayName;

        var entity = new WorkspaceFolder(
            tenantId,
            workspaceId,
            request.Name.Trim(),
            request.Description?.Trim() ?? string.Empty,
            currentUser.UserId,
            displayName,
            _idGenerator.NextId());

        await _repository.AddAsync(entity, cancellationToken);
        return entity.Id.ToString();
    }

    public async Task UpdateAsync(
        TenantId tenantId,
        string workspaceId,
        string folderId,
        WorkspaceFolderUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await LoadOrThrowAsync(tenantId, workspaceId, folderId, cancellationToken);

        var nextName = string.IsNullOrWhiteSpace(request.Name) ? entity.Name : request.Name.Trim();
        var nextDescription = request.Description ?? entity.Description ?? string.Empty;
        ValidateName(nextName);

        entity.Rename(nextName, nextDescription);
        await _repository.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeleteAsync(
        TenantId tenantId,
        string workspaceId,
        string folderId,
        CancellationToken cancellationToken)
    {
        var entity = await LoadOrThrowAsync(tenantId, workspaceId, folderId, cancellationToken);
        // 先清理关联，再删 folder 自身，避免出现孤儿引用。
        await _itemRepository.DeleteByFolderAsync(tenantId, workspaceId, entity.Id, cancellationToken);
        await _repository.DeleteAsync(entity, cancellationToken);
    }

    public async Task MoveItemAsync(
        TenantId tenantId,
        string workspaceId,
        string folderId,
        WorkspaceFolderItemMoveRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ItemId))
        {
            throw new BusinessException(ErrorCodes.ValidationError, "ItemIdRequired");
        }
        var itemType = (request.ItemType ?? string.Empty).Trim().ToLowerInvariant();
        if (!AllowedItemTypes.Contains(itemType))
        {
            throw new BusinessException(ErrorCodes.ValidationError, "ItemTypeInvalid");
        }

        var entity = await LoadOrThrowAsync(tenantId, workspaceId, folderId, cancellationToken);

        // 一个 (workspaceId, itemType, itemId) 只允许属于一个文件夹；先查后写。
        var existing = await _itemRepository.FindAssignmentAsync(
            tenantId,
            workspaceId,
            itemType,
            request.ItemId.Trim(),
            cancellationToken);

        if (existing is null)
        {
            var assignment = new WorkspaceFolderItem(
                tenantId,
                workspaceId,
                entity.Id,
                itemType,
                request.ItemId.Trim(),
                _idGenerator.NextId());
            await _itemRepository.AddAsync(assignment, cancellationToken);
        }
        else if (existing.FolderId != entity.Id)
        {
            // 已属于其它文件夹：删旧 + 加新（保持单一归属）。
            await _itemRepository.DeleteAsync(existing, cancellationToken);
            var assignment = new WorkspaceFolderItem(
                tenantId,
                workspaceId,
                entity.Id,
                itemType,
                request.ItemId.Trim(),
                _idGenerator.NextId());
            await _itemRepository.AddAsync(assignment, cancellationToken);
        }
    }

    private async Task<WorkspaceFolder> LoadOrThrowAsync(
        TenantId tenantId,
        string workspaceId,
        string folderId,
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(folderId, out var id))
        {
            throw new BusinessException(ErrorCodes.NotFound, "FolderNotFound");
        }
        var entity = await _repository.FindAsync(tenantId, workspaceId, id, cancellationToken);
        if (entity is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "FolderNotFound");
        }
        return entity;
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new BusinessException(ErrorCodes.ValidationError, "FolderNameRequired");
        }
        if (name.Length > 40)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "FolderNameTooLong");
        }
    }

    internal static WorkspaceFolderListItem ToDto(WorkspaceFolder entity, int itemCount)
    {
        return new WorkspaceFolderListItem(
            Id: entity.Id.ToString(),
            WorkspaceId: entity.WorkspaceId,
            Name: entity.Name,
            Description: string.IsNullOrEmpty(entity.Description) ? null : entity.Description,
            ItemCount: itemCount,
            CreatedByDisplayName: entity.CreatedByDisplayName,
            CreatedAt: new DateTimeOffset(DateTime.SpecifyKind(entity.CreatedAt, DateTimeKind.Utc)),
            UpdatedAt: new DateTimeOffset(DateTime.SpecifyKind(entity.UpdatedAt ?? entity.CreatedAt, DateTimeKind.Utc)));
    }
}

public sealed class WorkspacePublishChannelService : IWorkspacePublishChannelService
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "web-sdk",
        "open-api",
        "wechat",
        "feishu",
        "lark",
        "custom"
    };

    private static readonly HashSet<string> AllowedTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        "agent",
        "app",
        "workflow"
    };

    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "active",
        "inactive",
        "pending"
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly WorkspacePublishChannelRepository _repository;
    private readonly IIdGeneratorAccessor _idGenerator;

    public WorkspacePublishChannelService(
        WorkspacePublishChannelRepository repository,
        IIdGeneratorAccessor idGenerator)
    {
        _repository = repository;
        _idGenerator = idGenerator;
    }

    public async Task<PagedResult<WorkspacePublishChannelDto>> ListAsync(
        TenantId tenantId,
        string workspaceId,
        string? keyword,
        PagedRequest pagedRequest,
        CancellationToken cancellationToken)
    {
        var pageIndex = Math.Max(1, pagedRequest.PageIndex);
        var pageSize = Math.Clamp(pagedRequest.PageSize, 1, 100);

        var (entities, total) = await _repository.SearchAsync(
            tenantId,
            workspaceId,
            keyword,
            pageIndex,
            pageSize,
            cancellationToken);

        return new PagedResult<WorkspacePublishChannelDto>(
            entities.Select(ToDto).ToArray(),
            total,
            pageIndex,
            pageSize);
    }

    public async Task<string> CreateAsync(
        TenantId tenantId,
        string workspaceId,
        WorkspacePublishChannelCreateRequest request,
        CancellationToken cancellationToken)
    {
        var rawName = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawName))
        {
            throw new BusinessException(ErrorCodes.ValidationError, "ChannelNameRequired");
        }
        var rawType = request.Type?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!AllowedTypes.Contains(rawType))
        {
            throw new BusinessException(ErrorCodes.ValidationError, "ChannelTypeInvalid");
        }

        var sanitizedTargets = (request.SupportedTargets ?? Array.Empty<string>())
            .Where(t => AllowedTargets.Contains(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var entity = new WorkspacePublishChannel(
            tenantId,
            workspaceId,
            rawName,
            rawType,
            request.Description?.Trim() ?? string.Empty,
            JsonSerializer.Serialize(sanitizedTargets, JsonOptions),
            _idGenerator.NextId());

        await _repository.AddAsync(entity, cancellationToken);
        return entity.Id.ToString();
    }

    public async Task UpdateAsync(
        TenantId tenantId,
        string workspaceId,
        string channelId,
        WorkspacePublishChannelUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await LoadOrThrowAsync(tenantId, workspaceId, channelId, cancellationToken);

        string? supportedTargetsJson = null;
        if (request.SupportedTargets is not null)
        {
            var sanitized = request.SupportedTargets
                .Where(t => AllowedTargets.Contains(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            supportedTargetsJson = JsonSerializer.Serialize(sanitized, JsonOptions);
        }

        if (request.Status is not null && !AllowedStatuses.Contains(request.Status))
        {
            throw new BusinessException(ErrorCodes.ValidationError, "ChannelStatusInvalid");
        }

        entity.Update(request.Name, request.Description, request.Status, supportedTargetsJson);
        await _repository.UpdateAsync(entity, cancellationToken);
    }

    public async Task ReauthorizeAsync(
        TenantId tenantId,
        string workspaceId,
        string channelId,
        CancellationToken cancellationToken)
    {
        var entity = await LoadOrThrowAsync(tenantId, workspaceId, channelId, cancellationToken);
        entity.MarkAuthorized();
        await _repository.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeleteAsync(
        TenantId tenantId,
        string workspaceId,
        string channelId,
        CancellationToken cancellationToken)
    {
        var entity = await LoadOrThrowAsync(tenantId, workspaceId, channelId, cancellationToken);
        await _repository.DeleteAsync(entity, cancellationToken);
    }

    private async Task<WorkspacePublishChannel> LoadOrThrowAsync(
        TenantId tenantId,
        string workspaceId,
        string channelId,
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(channelId, out var id))
        {
            throw new BusinessException(ErrorCodes.NotFound, "ChannelNotFound");
        }
        var entity = await _repository.FindAsync(tenantId, workspaceId, id, cancellationToken);
        if (entity is null)
        {
            throw new BusinessException(ErrorCodes.NotFound, "ChannelNotFound");
        }
        return entity;
    }

    internal static WorkspacePublishChannelDto ToDto(WorkspacePublishChannel entity)
    {
        IReadOnlyList<string> supportedTargets;
        try
        {
            supportedTargets = JsonSerializer.Deserialize<string[]>(entity.SupportedTargetsJson, JsonOptions)
                ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            supportedTargets = Array.Empty<string>();
        }

        return new WorkspacePublishChannelDto(
            Id: entity.Id.ToString(),
            WorkspaceId: entity.WorkspaceId,
            Name: entity.Name,
            Type: entity.ChannelType,
            Status: entity.Status,
            AuthStatus: entity.AuthStatus,
            Description: string.IsNullOrEmpty(entity.Description) ? null : entity.Description,
            SupportedTargets: supportedTargets,
            LastSyncAt: entity.LastSyncAt is null
                ? null
                : new DateTimeOffset(DateTime.SpecifyKind(entity.LastSyncAt.Value, DateTimeKind.Utc)),
            CreatedAt: new DateTimeOffset(DateTime.SpecifyKind(entity.CreatedAt, DateTimeKind.Utc)));
    }
}
