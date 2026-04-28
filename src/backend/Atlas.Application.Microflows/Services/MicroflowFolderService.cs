using System.Text.RegularExpressions;
using Atlas.Application.Microflows.Abstractions;
using Atlas.Application.Microflows.Contracts;
using Atlas.Application.Microflows.Exceptions;
using Atlas.Application.Microflows.Infrastructure;
using Atlas.Application.Microflows.Models;
using Atlas.Application.Microflows.Repositories;
using Atlas.Domain.Microflows.Entities;

namespace Atlas.Application.Microflows.Services;

public sealed class MicroflowFolderService : IMicroflowFolderService
{
    private const int MaxDepth = 8;
    private static readonly Regex FolderNameRegex = new("^[A-Za-z][A-Za-z0-9_ -]*$", RegexOptions.Compiled);

    private readonly IMicroflowFolderRepository _folderRepository;
    private readonly IMicroflowResourceRepository _resourceRepository;
    private readonly IMicroflowRequestContextAccessor _requestContextAccessor;
    private readonly IMicroflowClock _clock;
    private readonly IMicroflowStorageTransaction _transaction;

    public MicroflowFolderService(
        IMicroflowFolderRepository folderRepository,
        IMicroflowResourceRepository resourceRepository,
        IMicroflowRequestContextAccessor requestContextAccessor,
        IMicroflowClock clock,
        IMicroflowStorageTransaction transaction)
    {
        _folderRepository = folderRepository;
        _resourceRepository = resourceRepository;
        _requestContextAccessor = requestContextAccessor;
        _clock = clock;
        _transaction = transaction;
    }

    public async Task<IReadOnlyList<MicroflowFolderDto>> ListAsync(string? workspaceId, string moduleId, CancellationToken cancellationToken)
    {
        var context = _requestContextAccessor.Current;
        var module = RequireModuleId(moduleId);
        var rows = await _folderRepository.ListByModuleAsync(workspaceId ?? context.WorkspaceId, context.TenantId, module, cancellationToken);
        return rows.Select(MicroflowFolderMapper.ToDto).ToArray();
    }

    public async Task<IReadOnlyList<MicroflowFolderTreeNodeDto>> GetTreeAsync(string? workspaceId, string moduleId, CancellationToken cancellationToken)
    {
        var context = _requestContextAccessor.Current;
        var module = RequireModuleId(moduleId);
        var rows = await _folderRepository.ListByModuleAsync(workspaceId ?? context.WorkspaceId, context.TenantId, module, cancellationToken);
        var childrenByParentId = rows
            .GroupBy(static folder => string.IsNullOrWhiteSpace(folder.ParentFolderId) ? string.Empty : folder.ParentFolderId!)
            .ToDictionary(static group => group.Key, static group => group.OrderBy(static folder => folder.Name, StringComparer.OrdinalIgnoreCase).ToArray(), StringComparer.Ordinal);

        return BuildTree(string.Empty, childrenByParentId);
    }

    public async Task<MicroflowFolderDto> CreateAsync(CreateMicroflowFolderRequestDto request, CancellationToken cancellationToken)
    {
        var context = _requestContextAccessor.Current;
        var moduleId = RequireModuleId(request.ModuleId);
        var name = ValidateName(request.Name);
        var workspaceId = request.WorkspaceId ?? context.WorkspaceId;
        var parent = await ResolveParentAsync(request.ParentFolderId, workspaceId, context.TenantId, moduleId, cancellationToken);
        var depth = parent is null ? 1 : parent.Depth + 1;
        EnsureDepth(depth);
        await EnsureNameAvailableAsync(workspaceId, context.TenantId, moduleId, parent?.Id, name, null, cancellationToken);

        var now = _clock.UtcNow;
        var entity = new MicroflowFolderEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            WorkspaceId = workspaceId,
            TenantId = context.TenantId,
            ModuleId = moduleId,
            ParentFolderId = parent?.Id,
            Name = name,
            Path = BuildPath(parent?.Path, name),
            Depth = depth,
            CreatedBy = context.UserId,
            CreatedAt = now,
            UpdatedBy = context.UserId,
            UpdatedAt = now
        };

        await _folderRepository.InsertAsync(entity, cancellationToken);
        return MicroflowFolderMapper.ToDto(entity);
    }

    public async Task<MicroflowFolderDto> RenameAsync(string id, RenameMicroflowFolderRequestDto request, CancellationToken cancellationToken)
    {
        var folder = await LoadFolderAsync(id, cancellationToken);
        var context = _requestContextAccessor.Current;
        EnsureContext(folder, context);
        var name = ValidateName(request.Name);
        await EnsureNameAvailableAsync(folder.WorkspaceId, folder.TenantId, folder.ModuleId, folder.ParentFolderId, name, folder.Id, cancellationToken);
        var allFolders = await _folderRepository.ListByModuleAsync(folder.WorkspaceId, folder.TenantId, folder.ModuleId, cancellationToken);
        var folderById = allFolders.ToDictionary(static item => item.Id, StringComparer.Ordinal);
        var oldPath = folder.Path;
        folder.Name = name;
        folder.Path = BuildPath(GetParentPath(folder.ParentFolderId, folderById), name);
        folder.UpdatedBy = context.UserId;
        folder.UpdatedAt = _clock.UtcNow;
        folderById[folder.Id] = folder;

        RecalculateDescendantPaths(folder, allFolders, folderById);
        await PersistFolderPathChangesAsync(folder, oldPath, allFolders, cancellationToken);
        return MicroflowFolderMapper.ToDto(folder);
    }

    public async Task<MicroflowFolderDto> MoveAsync(string id, MoveMicroflowFolderRequestDto request, CancellationToken cancellationToken)
    {
        var folder = await LoadFolderAsync(id, cancellationToken);
        var context = _requestContextAccessor.Current;
        EnsureContext(folder, context);
        var parent = await ResolveParentAsync(request.ParentFolderId, folder.WorkspaceId, folder.TenantId, folder.ModuleId, cancellationToken);
        if (parent is not null && IsDescendantOrSelf(parent, folder))
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowFolderCycle, "不能将文件夹移动到自身或子文件夹下。", 409);
        }

        var depthDelta = (parent?.Depth + 1 ?? 1) - folder.Depth;
        var allFolders = await _folderRepository.ListByModuleAsync(folder.WorkspaceId, folder.TenantId, folder.ModuleId, cancellationToken);
        var descendants = GetDescendants(folder, allFolders);
        if (descendants.Any(item => item.Depth + depthDelta > MaxDepth))
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowFolderDepthExceeded, $"微流文件夹最多支持 {MaxDepth} 级。", 422);
        }

        await EnsureNameAvailableAsync(folder.WorkspaceId, folder.TenantId, folder.ModuleId, parent?.Id, folder.Name, folder.Id, cancellationToken);
        var oldPath = folder.Path;
        folder.ParentFolderId = parent?.Id;
        folder.Depth = parent is null ? 1 : parent.Depth + 1;
        folder.Path = BuildPath(parent?.Path, folder.Name);
        folder.UpdatedBy = context.UserId;
        folder.UpdatedAt = _clock.UtcNow;
        var folderById = allFolders.ToDictionary(static item => item.Id, StringComparer.Ordinal);
        folderById[folder.Id] = folder;
        RecalculateDescendantPaths(folder, allFolders, folderById);
        await PersistFolderPathChangesAsync(folder, oldPath, allFolders, cancellationToken);
        return MicroflowFolderMapper.ToDto(folder);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var folder = await LoadFolderAsync(id, cancellationToken);
        EnsureContext(folder, _requestContextAccessor.Current);
        var allFolders = await _folderRepository.ListByModuleAsync(folder.WorkspaceId, folder.TenantId, folder.ModuleId, cancellationToken);
        if (allFolders.Any(item => string.Equals(item.ParentFolderId, folder.Id, StringComparison.Ordinal)))
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowFolderNotEmpty, "该微流文件夹包含子文件夹，不能删除。", 409);
        }

        var resources = await _resourceRepository.ListByFolderIdAsync(folder.Id, cancellationToken);
        if (resources.Count > 0)
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowFolderNotEmpty, "该微流文件夹下仍有微流，不能删除。", 409);
        }

        await _folderRepository.DeleteAsync(folder.Id, cancellationToken);
    }

    private async Task PersistFolderPathChangesAsync(
        MicroflowFolderEntity root,
        string oldRootPath,
        IReadOnlyList<MicroflowFolderEntity> allFolders,
        CancellationToken cancellationToken)
    {
        var descendants = GetDescendants(oldRootPath, root.Id, allFolders);
        var affectedFolders = descendants.Prepend(root).ToArray();
        var folderIds = affectedFolders.Select(static folder => folder.Id).ToArray();
        var affectedResources = await _resourceRepository.ListByFolderIdsAsync(folderIds, cancellationToken);
        var pathByFolderId = affectedFolders.ToDictionary(static folder => folder.Id, static folder => folder.Path, StringComparer.Ordinal);
        foreach (var resource in affectedResources)
        {
            if (!string.IsNullOrWhiteSpace(resource.FolderId) && pathByFolderId.TryGetValue(resource.FolderId, out var nextPath))
            {
                resource.FolderPath = nextPath;
            }
        }

        await _transaction.ExecuteAsync(async () =>
        {
            await _folderRepository.UpdateManyAsync(affectedFolders, cancellationToken);
            await _resourceRepository.UpdateManyAsync(affectedResources, cancellationToken);
        }, cancellationToken);
    }

    private static IReadOnlyList<MicroflowFolderTreeNodeDto> BuildTree(
        string parentId,
        IReadOnlyDictionary<string, MicroflowFolderEntity[]> childrenByParentId)
    {
        if (!childrenByParentId.TryGetValue(parentId, out var children))
        {
            return Array.Empty<MicroflowFolderTreeNodeDto>();
        }

        return children
            .Select(child => MicroflowFolderMapper.ToTreeNode(child, BuildTree(child.Id, childrenByParentId)))
            .ToArray();
    }

    private static void RecalculateDescendantPaths(
        MicroflowFolderEntity root,
        IReadOnlyList<MicroflowFolderEntity> allFolders,
        IReadOnlyDictionary<string, MicroflowFolderEntity> folderById)
    {
        var descendants = GetDescendants(root, allFolders);
        foreach (var descendant in descendants.OrderBy(static item => item.Depth))
        {
            descendant.Path = BuildPath(GetParentPath(descendant.ParentFolderId, folderById), descendant.Name);
            descendant.Depth = descendant.Path.Count(static ch => ch == '/') + 1;
        }
    }

    private static IReadOnlyList<MicroflowFolderEntity> GetDescendants(MicroflowFolderEntity root, IReadOnlyList<MicroflowFolderEntity> allFolders)
    {
        return GetDescendants(root.Path, root.Id, allFolders);
    }

    private static IReadOnlyList<MicroflowFolderEntity> GetDescendants(string rootPath, string rootId, IReadOnlyList<MicroflowFolderEntity> allFolders)
    {
        return allFolders
            .Where(item => item.Id != rootId && item.Path.StartsWith(rootPath + "/", StringComparison.Ordinal))
            .ToArray();
    }

    private static bool IsDescendantOrSelf(MicroflowFolderEntity candidateParent, MicroflowFolderEntity folder)
    {
        return candidateParent.Id == folder.Id
            || candidateParent.Path.StartsWith(folder.Path + "/", StringComparison.Ordinal);
    }

    private static string? GetParentPath(string? parentFolderId, IReadOnlyDictionary<string, MicroflowFolderEntity> folderById)
    {
        return !string.IsNullOrWhiteSpace(parentFolderId) && folderById.TryGetValue(parentFolderId, out var parent)
            ? parent.Path
            : null;
    }

    private async Task<MicroflowFolderEntity?> ResolveParentAsync(
        string? parentFolderId,
        string? workspaceId,
        string? tenantId,
        string moduleId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(parentFolderId))
        {
            return null;
        }

        var parent = await LoadFolderAsync(parentFolderId, cancellationToken);
        if (!string.Equals(parent.WorkspaceId, workspaceId, StringComparison.Ordinal)
            || !string.Equals(parent.TenantId, tenantId, StringComparison.Ordinal)
            || !string.Equals(parent.ModuleId, moduleId, StringComparison.Ordinal))
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowFolderNotFound, "父级微流文件夹不存在或不属于当前模块。", 404);
        }

        return parent;
    }

    private async Task<MicroflowFolderEntity> LoadFolderAsync(string id, CancellationToken cancellationToken)
    {
        var folder = await _folderRepository.GetByIdAsync(id, cancellationToken);
        return folder ?? throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowFolderNotFound, "微流文件夹不存在。", 404);
    }

    private void EnsureContext(MicroflowFolderEntity folder, MicroflowRequestContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.WorkspaceId)
            && !string.Equals(folder.WorkspaceId, context.WorkspaceId, StringComparison.Ordinal))
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowWorkspaceForbidden, "微流文件夹不属于当前工作区。", 403);
        }

        if (!string.IsNullOrWhiteSpace(context.TenantId)
            && !string.Equals(folder.TenantId, context.TenantId, StringComparison.Ordinal))
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowPermissionDenied, "微流文件夹不属于当前租户。", 403);
        }
    }

    private async Task EnsureNameAvailableAsync(
        string? workspaceId,
        string? tenantId,
        string moduleId,
        string? parentFolderId,
        string name,
        string? excludeId,
        CancellationToken cancellationToken)
    {
        if (await _folderRepository.ExistsBySiblingNameAsync(workspaceId, tenantId, moduleId, parentFolderId, name, excludeId, cancellationToken))
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowFolderNameDuplicated, "同一父级下已存在同名微流文件夹。", 409);
        }
    }

    private static string RequireModuleId(string? moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
        {
            throw new MicroflowApiException(
                MicroflowApiErrorCode.MicroflowValidationFailed,
                "moduleId 不能为空。",
                422,
                fieldErrors: [new MicroflowApiFieldError { FieldPath = "moduleId", Code = "REQUIRED", Message = "moduleId 不能为空。" }]);
        }

        return moduleId.Trim();
    }

    private static string ValidateName(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || !FolderNameRegex.IsMatch(trimmed))
        {
            throw new MicroflowApiException(
                MicroflowApiErrorCode.MicroflowValidationFailed,
                "文件夹名称必须以字母开头，且只能包含字母、数字、空格、下划线和短横线。",
                422,
                fieldErrors: [new MicroflowApiFieldError { FieldPath = "name", Code = "INVALID_FORMAT", Message = "文件夹名称格式非法。" }]);
        }

        return trimmed;
    }

    private static void EnsureDepth(int depth)
    {
        if (depth > MaxDepth)
        {
            throw new MicroflowApiException(MicroflowApiErrorCode.MicroflowFolderDepthExceeded, $"微流文件夹最多支持 {MaxDepth} 级。", 422);
        }
    }

    private static string BuildPath(string? parentPath, string name)
    {
        return string.IsNullOrWhiteSpace(parentPath) ? name : $"{parentPath}/{name}";
    }
}
