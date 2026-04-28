using System.Text.Json;
using System.Runtime.ExceptionServices;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Domain.AiPlatform.Enums;
using Atlas.Domain.Identity.Entities;
using Atlas.Domain.Platform.Entities;
using Atlas.Infrastructure.Repositories;
using SqlSugar;

#pragma warning disable CS0618 // 门户旧 AI 数据库版本字段兼容展示。
namespace Atlas.Infrastructure.Services.Platform;

public sealed class WorkspacePortalService : IWorkspacePortalService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ISqlSugarClient _db;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly IAiAppService _aiAppService;
    private readonly ICozeWorkflowCommandService _workflowCommandService;
    private readonly WorkspaceRepository _workspaceRepository;
    private readonly WorkspaceRoleRepository _workspaceRoleRepository;
    private readonly WorkspaceMemberRepository _workspaceMemberRepository;
    private readonly WorkspaceResourcePermissionRepository _workspaceResourcePermissionRepository;
    private readonly IAppManifestCommandService _appManifestCommandService;

    public WorkspacePortalService(
        ISqlSugarClient db,
        IIdGeneratorAccessor idGeneratorAccessor,
        IAiAppService aiAppService,
        ICozeWorkflowCommandService workflowCommandService,
        WorkspaceRepository workspaceRepository,
        WorkspaceRoleRepository workspaceRoleRepository,
        WorkspaceMemberRepository workspaceMemberRepository,
        WorkspaceResourcePermissionRepository workspaceResourcePermissionRepository,
        IAppManifestCommandService appManifestCommandService)
    {
        _db = db;
        _idGeneratorAccessor = idGeneratorAccessor;
        _aiAppService = aiAppService;
        _workflowCommandService = workflowCommandService;
        _workspaceRepository = workspaceRepository;
        _workspaceRoleRepository = workspaceRoleRepository;
        _workspaceMemberRepository = workspaceMemberRepository;
        _workspaceResourcePermissionRepository = workspaceResourcePermissionRepository;
        _appManifestCommandService = appManifestCommandService;
    }

    public async Task<IReadOnlyList<WorkspaceListItem>> ListWorkspacesAsync(
        TenantId tenantId,
        long userId,
        bool isPlatformAdmin,
        CancellationToken cancellationToken = default)
    {
        var workspaces = await _workspaceRepository.ListByTenantAsync(tenantId, cancellationToken);
        if (workspaces.Count == 0)
        {
            return Array.Empty<WorkspaceListItem>();
        }

        var members = await _workspaceMemberRepository.ListByUserAsync(tenantId, userId, cancellationToken);
        var memberWorkspaceIds = members.Select(x => x.WorkspaceId).ToHashSet();
        var visibleWorkspaces = isPlatformAdmin
            ? workspaces
            : workspaces.Where(item => memberWorkspaceIds.Contains(item.Id)).ToList();

        if (visibleWorkspaces.Count == 0)
        {
            return Array.Empty<WorkspaceListItem>();
        }

        var roleIds = members.Select(x => x.WorkspaceRoleId).Distinct().ToArray();
        var roles = roleIds.Length == 0
            ? new List<WorkspaceRole>()
            : await _db.Queryable<WorkspaceRole>()
                .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(roleIds, x.Id))
                .ToListAsync(cancellationToken);
        var roleMap = roles.ToDictionary(x => x.Id);
        var memberMap = members.ToDictionary(x => x.WorkspaceId);
        var workspaceIds = visibleWorkspaces.Select(x => x.Id).ToArray();

        var appCountsTask = _db.Queryable<AiApp>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.WorkspaceId.HasValue && SqlFunc.ContainsArray(workspaceIds, x.WorkspaceId!.Value))
            .GroupBy(x => x.WorkspaceId)
            .Select(x => new { WorkspaceId = x.WorkspaceId!.Value, Count = SqlFunc.AggregateCount(x.Id) })
            .ToListAsync(cancellationToken);
        var agentCountsTask = _db.Queryable<Agent>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.WorkspaceId.HasValue && SqlFunc.ContainsArray(workspaceIds, x.WorkspaceId!.Value))
            .GroupBy(x => x.WorkspaceId)
            .Select(x => new { WorkspaceId = x.WorkspaceId!.Value, Count = SqlFunc.AggregateCount(x.Id) })
            .ToListAsync(cancellationToken);
        var workflowCountsTask = _db.Queryable<CozeWorkflowMeta>()
            .Where(x => x.TenantIdValue == tenantId.Value && !x.IsDeleted && x.WorkspaceId.HasValue && SqlFunc.ContainsArray(workspaceIds, x.WorkspaceId!.Value))
            .GroupBy(x => x.WorkspaceId)
            .Select(x => new { WorkspaceId = x.WorkspaceId!.Value, Count = SqlFunc.AggregateCount(x.Id) })
            .ToListAsync(cancellationToken);

        await Task.WhenAll(appCountsTask, agentCountsTask, workflowCountsTask);

        var appCountMap = appCountsTask.Result.ToDictionary(x => x.WorkspaceId, x => x.Count);
        var agentCountMap = agentCountsTask.Result.ToDictionary(x => x.WorkspaceId, x => x.Count);
        var workflowCountMap = workflowCountsTask.Result.ToDictionary(x => x.WorkspaceId, x => x.Count);

        // 1→N 模型：为每个 workspace 解析「默认主应用」（用最早创建的 AppManifest 兜底）
        var manifests = await _db.Queryable<AppManifest>()
            .Where(x => x.TenantIdValue == tenantId.Value
                        && x.WorkspaceId.HasValue
                        && SqlFunc.ContainsArray(workspaceIds, x.WorkspaceId!.Value))
            .OrderBy(x => x.CreatedAt, OrderByType.Asc)
            .ToListAsync(cancellationToken);
        var defaultManifestMap = manifests
            .GroupBy(x => x.WorkspaceId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        return visibleWorkspaces
            .Select(item =>
            {
                var member = memberMap.GetValueOrDefault(item.Id);
                var roleCode = isPlatformAdmin
                    ? WorkspaceBuiltInRoleCodes.Owner
                    : member is not null && roleMap.TryGetValue(member.WorkspaceRoleId, out var role)
                        ? role.Code
                        : WorkspaceBuiltInRoleCodes.Member;

                var appInstanceId = item.AppInstanceId?.ToString();
                var appKey = item.AppKey;
                if (string.IsNullOrEmpty(appInstanceId) && defaultManifestMap.TryGetValue(item.Id, out var defaultManifest))
                {
                    appInstanceId = defaultManifest.Id.ToString();
                    appKey = defaultManifest.AppKey;
                }

                return new WorkspaceListItem(
                    item.Id.ToString(),
                    tenantId.Value.ToString("D"),
                    item.Name,
                    item.Description,
                    item.Icon,
                    appInstanceId,
                    appKey,
                    roleCode,
                    appCountMap.GetValueOrDefault(item.Id),
                    agentCountMap.GetValueOrDefault(item.Id),
                    workflowCountMap.GetValueOrDefault(item.Id),
                    item.CreatedAt.ToString("O"),
                    item.LastVisitedAt?.ToString("O"));
            })
            .OrderByDescending(item => item.LastVisitedAt ?? item.CreatedAt)
            .ToArray();
    }

    public async Task<WorkspaceDetailDto?> GetWorkspaceAsync(
        TenantId tenantId,
        long workspaceId,
        long userId,
        bool isPlatformAdmin,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _workspaceRepository.FindByIdAsync(tenantId, workspaceId, cancellationToken);
        if (workspace is null || workspace.IsArchived)
        {
            return null;
        }

        var (_, role) = await RequireWorkspaceAccessAsync(tenantId, workspaceId, userId, isPlatformAdmin, WorkspacePermissionActions.View, cancellationToken);
        workspace.MarkVisited(userId);
        await _workspaceRepository.UpdateAsync(workspace, cancellationToken);

        // 1→N 模型：若 Workspace 仍未绑定主应用，回填首个属于该 workspace 的 AppManifest 作为默认应用，
        // 兼容前端 `workspace.appKey` 的字符串拼接习惯。
        var (appInstanceId, appKey) = await ResolveDefaultAppInstanceAsync(tenantId, workspace, cancellationToken);

        return new WorkspaceDetailDto(
            workspace.Id.ToString(),
            tenantId.Value.ToString("D"),
            workspace.Name,
            workspace.Description,
            workspace.Icon,
            appInstanceId,
            appKey,
            role?.Code ?? WorkspaceBuiltInRoleCodes.Owner,
            ResolveRoleActions(role?.Code, isPlatformAdmin),
            workspace.CreatedAt.ToString("O"),
            workspace.LastVisitedAt?.ToString("O"));
    }

    public async Task<WorkspaceDetailDto?> GetWorkspaceByAppKeyAsync(
        TenantId tenantId,
        string appKey,
        long userId,
        bool isPlatformAdmin,
        CancellationToken cancellationToken = default)
    {
        var normalizedAppKey = appKey.Trim();
        if (string.IsNullOrWhiteSpace(normalizedAppKey))
        {
            return null;
        }

        // 1→N 模型：先按历史字段（Workspace.AppKey）查；找不到再通过 AppManifest 反查（任意挂在 workspace 下的 manifest）。
        var workspace = await _workspaceRepository.FindByAppKeyAsync(tenantId, normalizedAppKey, cancellationToken);
        if (workspace is null)
        {
            var manifest = await _db.Queryable<AppManifest>()
                .Where(x => x.TenantIdValue == tenantId.Value
                            && x.AppKey == normalizedAppKey
                            && x.WorkspaceId.HasValue)
                .FirstAsync(cancellationToken);
            if (manifest is not null && manifest.WorkspaceId.HasValue)
            {
                workspace = await _workspaceRepository.FindByIdAsync(tenantId, manifest.WorkspaceId.Value, cancellationToken);
            }
        }

        return workspace is null
            ? null
            : await GetWorkspaceAsync(tenantId, workspace.Id, userId, isPlatformAdmin, cancellationToken);
    }

    /// <summary>
    /// 1→N 模型默认主应用解析：
    /// - 若 Workspace.AppInstanceId/AppKey 已绑定，直接返回；
    /// - 否则查询第一个 WorkspaceId == workspace.Id 的 AppManifest，并将其回填为 workspace 默认主应用以提升下次读路径性能。
    /// - 若工作空间下无任何 AppManifest，返回 (null, null)。
    /// </summary>
    private async Task<(string? AppInstanceId, string? AppKey)> ResolveDefaultAppInstanceAsync(
        TenantId tenantId,
        Workspace workspace,
        CancellationToken cancellationToken)
    {
        if (workspace.AppInstanceId.HasValue && !string.IsNullOrWhiteSpace(workspace.AppKey))
        {
            return (workspace.AppInstanceId.Value.ToString(), workspace.AppKey);
        }

        var manifest = await _db.Queryable<AppManifest>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.WorkspaceId == workspace.Id)
            .OrderBy(x => x.CreatedAt, OrderByType.Asc)
            .FirstAsync(cancellationToken);
        if (manifest is null)
        {
            return (null, null);
        }

        workspace.BindDefaultAppInstance(manifest.Id, manifest.AppKey, workspace.UpdatedBy);
        await _workspaceRepository.UpdateAsync(workspace, cancellationToken);
        return (manifest.Id.ToString(), manifest.AppKey);
    }

    public async Task<long> CreateWorkspaceAsync(
        TenantId tenantId,
        long userId,
        WorkspaceCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new BusinessException(ErrorCodes.ValidationError, "工作空间名称不能为空。");
        }

        // 1→N 模型：创建工作空间不再要求绑定 AppManifest；应用实例通过
        // POST .../workspaces/{id}/app-instances 单独在工作空间内创建。
        var workspaceId = _idGeneratorAccessor.NextId();
        var workspace = new Workspace(
            tenantId,
            normalizedName,
            request.Description?.Trim(),
            request.Icon?.Trim(),
            userId,
            workspaceId);
        await _workspaceRepository.AddAsync(workspace, cancellationToken);
        await EnsureBuiltInRolesAsync(tenantId, workspaceId, cancellationToken);
        await EnsureMemberAsync(tenantId, workspaceId, userId, WorkspaceBuiltInRoleCodes.Owner, userId, cancellationToken);
        return workspaceId;
    }

    public async Task UpdateWorkspaceAsync(
        TenantId tenantId,
        long workspaceId,
        long userId,
        bool isPlatformAdmin,
        WorkspaceUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _workspaceRepository.FindByIdAsync(tenantId, workspaceId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, "工作空间不存在。");
        await RequireWorkspaceAccessAsync(tenantId, workspaceId, userId, isPlatformAdmin, WorkspacePermissionActions.Edit, cancellationToken);
        workspace.Update(request.Name, request.Description, request.Icon, userId);
        await _workspaceRepository.UpdateAsync(workspace, cancellationToken);
    }

    public async Task DeleteWorkspaceAsync(
        TenantId tenantId,
        long workspaceId,
        long userId,
        bool isPlatformAdmin,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _workspaceRepository.FindByIdAsync(tenantId, workspaceId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, "工作空间不存在。");
        await RequireWorkspaceAccessAsync(tenantId, workspaceId, userId, isPlatformAdmin, WorkspacePermissionActions.Delete, cancellationToken);
        workspace.Archive(userId);
        await _workspaceRepository.UpdateAsync(workspace, cancellationToken);
    }

    public async Task<PagedResult<WorkspaceAppCardDto>> GetDevelopAppsAsync(
        TenantId tenantId,
        long workspaceId,
        long userId,
        bool isPlatformAdmin,
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        await RequireWorkspaceAccessAsync(tenantId, workspaceId, userId, isPlatformAdmin, WorkspacePermissionActions.View, cancellationToken);
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = Math.Clamp(request.PageSize <= 0 ? 24 : request.PageSize, 1, 100);
        var keyword = request.Keyword?.Trim();

        var query = _db.Queryable<AiApp>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.WorkspaceId == workspaceId);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x => x.Name.Contains(keyword) || (x.Description != null && x.Description.Contains(keyword)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
            .OrderBy(x => x.CreatedAt, OrderByType.Desc)
            .ToPageListAsync(pageIndex, pageSize, cancellationToken);

        var orgId = tenantId.Value.ToString("D");
        return new PagedResult<WorkspaceAppCardDto>(
            items.Select(item => new WorkspaceAppCardDto(
                item.Id.ToString(),
                item.Name,
                item.Description,
                item.Status.ToString(),
                item.PublishVersion > 0 ? "published" : "draft",
                item.Icon,
                (item.UpdatedAt ?? item.CreatedAt).ToString("O"),
                $"/org/{orgId}/workspaces/{workspaceId}/apps/{item.Id}",
                item.WorkflowId?.ToString()))
                .ToArray(),
            total,
            pageIndex,
            pageSize);
    }

    public async Task<WorkspaceAppCreateResult> CreateDevelopAppAsync(
        TenantId tenantId,
        long workspaceId,
        long userId,
        bool isPlatformAdmin,
        WorkspaceAppCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        await RequireWorkspaceAccessAsync(tenantId, workspaceId, userId, isPlatformAdmin, WorkspacePermissionActions.Edit, cancellationToken);

        var workflowId = await _workflowCommandService.CreateAsync(
            tenantId,
            userId,
            new CozeWorkflowCreateCommand(
                string.IsNullOrWhiteSpace(request.Name) ? "新建应用工作流" : request.Name.Trim(),
                request.Description?.Trim(),
                WorkflowMode.Standard,
                workspaceId),
            cancellationToken);

        long appId;
        try
        {
            appId = await _aiAppService.CreateAsync(
                tenantId,
                new AiAppCreateRequest(
                    request.Name,
                    request.Description,
                    request.Icon,
                    null,
                    workflowId,
                    null,
                    workspaceId),
                cancellationToken);
        }
        catch (Exception exception)
        {
            try
            {
                await _workflowCommandService.DeleteAsync(tenantId, workflowId, cancellationToken);
            }
            catch
            {
                // 保留原始异常语义；补偿删除失败不覆盖首个业务错误。
            }

            ExceptionDispatchInfo.Capture(exception).Throw();
            throw;
        }

        var orgId = tenantId.Value.ToString("D");
        return new WorkspaceAppCreateResult(
            appId.ToString(),
            workflowId.ToString(),
            $"/org/{orgId}/workspaces/{workspaceId}/apps/{appId}");
    }

    public async Task<PagedResult<WorkspaceResourceCardDto>> GetResourcesAsync(
        TenantId tenantId,
        long workspaceId,
        long userId,
        bool isPlatformAdmin,
        string? resourceType,
        PagedRequest request,
        CancellationToken cancellationToken = default)
    {
        await RequireWorkspaceAccessAsync(tenantId, workspaceId, userId, isPlatformAdmin, WorkspacePermissionActions.View, cancellationToken);

        var normalizedType = NormalizeResourceType(resourceType);
        var keyword = request.Keyword?.Trim();
        var resources = new List<WorkspaceResourceCardDto>();
        var orgId = tenantId.Value.ToString("D");

        if (string.IsNullOrEmpty(normalizedType) || normalizedType == "agent")
        {
            var agents = await _db.Queryable<Agent>()
                .Where(x => x.TenantIdValue == tenantId.Value && x.WorkspaceId == workspaceId)
                .WhereIF(!string.IsNullOrWhiteSpace(keyword), x => x.Name.Contains(keyword!) || (x.Description != null && x.Description.Contains(keyword!)))
                .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
                .Take(40)
                .ToListAsync(cancellationToken);
            resources.AddRange(agents.Select(item => new WorkspaceResourceCardDto(
                "agent",
                item.Id.ToString(),
                item.Name,
                item.Description,
                item.Status.ToString(),
                item.PublishVersion > 0 ? "published" : "draft",
                (item.UpdatedAt ?? item.CreatedAt).ToString("O"),
                $"/org/{orgId}/workspaces/{workspaceId}/agents/{item.Id}",
                item.PublishVersion > 0 ? $"v{item.PublishVersion}" : null,
                item.DefaultWorkflowId?.ToString())));
        }

        if (string.IsNullOrEmpty(normalizedType) || normalizedType == "workflow" || normalizedType == "chatflow")
        {
            var workflows = await _db.Queryable<CozeWorkflowMeta>()
                .Where(x => x.TenantIdValue == tenantId.Value && !x.IsDeleted && x.WorkspaceId == workspaceId)
                .WhereIF(!string.IsNullOrWhiteSpace(keyword), x => x.Name.Contains(keyword!) || (x.Description != null && x.Description.Contains(keyword!)))
                .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
                .Take(40)
                .ToListAsync(cancellationToken);
            resources.AddRange(workflows
                .Where(item => string.IsNullOrEmpty(normalizedType)
                    || (normalizedType == "workflow" && item.Mode == WorkflowMode.Standard)
                    || (normalizedType == "chatflow" && item.Mode == WorkflowMode.ChatFlow))
                .Select(item => new WorkspaceResourceCardDto(
                    item.Mode == WorkflowMode.ChatFlow ? "chatflow" : "workflow",
                    item.Id.ToString(),
                    item.Name,
                    item.Description,
                    item.Status.ToString(),
                    item.LatestVersionNumber > 0 ? "published" : "draft",
                    item.UpdatedAt.ToString("O"),
                    item.Mode == WorkflowMode.ChatFlow
                        ? $"/org/{orgId}/workspaces/{workspaceId}/chatflows/{item.Id}"
                        : $"/org/{orgId}/workspaces/{workspaceId}/workflows/{item.Id}",
                    item.LatestVersionNumber > 0 ? $"v{item.LatestVersionNumber}" : null,
                    null)));
        }

        if (string.IsNullOrEmpty(normalizedType) || normalizedType == "plugin")
        {
            var plugins = await _db.Queryable<AiPlugin>()
                .Where(x => x.TenantIdValue == tenantId.Value && x.WorkspaceId == workspaceId)
                .WhereIF(!string.IsNullOrWhiteSpace(keyword), x => x.Name.Contains(keyword!) || (x.Description != null && x.Description.Contains(keyword!)))
                .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
                .Take(40)
                .ToListAsync(cancellationToken);
            resources.AddRange(plugins.Select(item => new WorkspaceResourceCardDto(
                "plugin",
                item.Id.ToString(),
                item.Name,
                item.Description,
                item.Status.ToString(),
                item.PublishedVersion > 0 ? "published" : "draft",
                (item.UpdatedAt ?? item.CreatedAt).ToString("O"),
                $"/org/{orgId}/workspaces/{workspaceId}/plugins/{item.Id}",
                item.PublishedVersion > 0 ? $"v{item.PublishedVersion}" : null,
                null)));
        }

        if (string.IsNullOrEmpty(normalizedType) || normalizedType == "knowledge-base")
        {
            var knowledgeBases = await _db.Queryable<KnowledgeBase>()
                .Where(x => x.TenantIdValue == tenantId.Value && x.WorkspaceId == workspaceId)
                .WhereIF(!string.IsNullOrWhiteSpace(keyword), x => x.Name.Contains(keyword!) || (x.Description != null && x.Description.Contains(keyword!)))
                .OrderBy(x => x.CreatedAt, OrderByType.Desc)
                .Take(40)
                .ToListAsync(cancellationToken);
            resources.AddRange(knowledgeBases.Select(item => new WorkspaceResourceCardDto(
                "knowledge-base",
                item.Id.ToString(),
                item.Name,
                item.Description,
                item.Type.ToString(),
                "draft",
                item.CreatedAt.ToString("O"),
                $"/org/{orgId}/workspaces/{workspaceId}/knowledge-bases/{item.Id}",
                null,
                null)));
        }

        if (string.IsNullOrEmpty(normalizedType) || normalizedType == "database")
        {
            var databases = await _db.Queryable<AiDatabase>()
                .Where(x => x.TenantIdValue == tenantId.Value && x.WorkspaceId == workspaceId)
                .WhereIF(!string.IsNullOrWhiteSpace(keyword), x => x.Name.Contains(keyword!) || (x.Description != null && x.Description.Contains(keyword!)))
                .OrderBy(x => x.UpdatedAt, OrderByType.Desc)
                .Take(40)
                .ToListAsync(cancellationToken);
            resources.AddRange(databases.Select(item => new WorkspaceResourceCardDto(
                "database",
                item.Id.ToString(),
                item.Name,
                item.Description,
                item.OwnerType.ToString(),
                item.PublishedVersion > 0 ? "published" : "draft",
                (item.UpdatedAt ?? item.CreatedAt).ToString("O"),
                $"/org/{orgId}/workspaces/{workspaceId}/databases/{item.Id}",
                item.PublishedVersion > 0 ? $"v{item.PublishedVersion}" : null,
                null)));
        }

        var ordered = resources
            .OrderByDescending(item => item.UpdatedAt)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var pageIndex = request.PageIndex <= 0 ? 1 : request.PageIndex;
        var pageSize = Math.Clamp(request.PageSize <= 0 ? 24 : request.PageSize, 1, 100);
        var pagedItems = ordered.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToArray();
        return new PagedResult<WorkspaceResourceCardDto>(pagedItems, ordered.Count, pageIndex, pageSize);
    }

    public async Task<IReadOnlyList<WorkspaceMemberDto>> GetMembersAsync(
        TenantId tenantId,
        long workspaceId,
        long userId,
        bool isPlatformAdmin,
        CancellationToken cancellationToken = default)
    {
        await RequireWorkspaceAccessAsync(tenantId, workspaceId, userId, isPlatformAdmin, WorkspacePermissionActions.View, cancellationToken);
        var members = await _workspaceMemberRepository.ListByWorkspaceAsync(tenantId, workspaceId, cancellationToken);
        if (members.Count == 0)
        {
            return Array.Empty<WorkspaceMemberDto>();
        }

        var roleIds = members.Select(x => x.WorkspaceRoleId).Distinct().ToArray();
        var userIds = members.Select(x => x.UserId).Distinct().ToArray();
        var rolesTask = _db.Queryable<WorkspaceRole>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(roleIds, x.Id))
            .ToListAsync(cancellationToken);
        var usersTask = _db.Queryable<UserAccount>()
            .Where(x => x.TenantIdValue == tenantId.Value && SqlFunc.ContainsArray(userIds, x.Id))
            .ToListAsync(cancellationToken);
        await Task.WhenAll(rolesTask, usersTask);

        var roleMap = rolesTask.Result.ToDictionary(x => x.Id);
        var userMap = usersTask.Result.ToDictionary(x => x.Id);

        return members
            .Where(member => roleMap.ContainsKey(member.WorkspaceRoleId) && userMap.ContainsKey(member.UserId))
            .Select(member =>
            {
                var role = roleMap[member.WorkspaceRoleId];
                var user = userMap[member.UserId];
                return new WorkspaceMemberDto(
                    member.UserId.ToString(),
                    user.Username,
                    user.DisplayName,
                    role.Id.ToString(),
                    role.Code,
                    role.Name,
                    member.JoinedAt.ToString("O"));
            })
            .OrderBy(item => item.RoleCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task AddMemberAsync(
        TenantId tenantId,
        long workspaceId,
        long operatorUserId,
        bool isPlatformAdmin,
        WorkspaceMemberCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureWorkspaceManagerAsync(tenantId, workspaceId, operatorUserId, isPlatformAdmin, cancellationToken);

        if (!long.TryParse(request.UserId, out var targetUserId) || targetUserId <= 0)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "工作空间成员标识无效。");
        }

        var user = await _db.Queryable<UserAccount>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == targetUserId)
            .FirstAsync(cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, "成员用户不存在。");

        var role = await RequireRoleByCodeAsync(tenantId, workspaceId, request.RoleCode, cancellationToken);
        var existing = await _workspaceMemberRepository.FindByWorkspaceAndUserAsync(tenantId, workspaceId, targetUserId, cancellationToken);
        if (existing is not null)
        {
            existing.ChangeRole(role.Id);
            await _workspaceMemberRepository.UpdateAsync(existing, cancellationToken);
            return;
        }

        _ = user;
        var member = new WorkspaceMember(
            tenantId,
            workspaceId,
            targetUserId,
            role.Id,
            operatorUserId,
            _idGeneratorAccessor.NextId());
        await _workspaceMemberRepository.AddAsync(member, cancellationToken);
    }

    public async Task UpdateMemberRoleAsync(
        TenantId tenantId,
        long workspaceId,
        long targetUserId,
        long operatorUserId,
        bool isPlatformAdmin,
        WorkspaceMemberRoleUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureWorkspaceManagerAsync(tenantId, workspaceId, operatorUserId, isPlatformAdmin, cancellationToken);
        var member = await _workspaceMemberRepository.FindByWorkspaceAndUserAsync(tenantId, workspaceId, targetUserId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, "工作空间成员不存在。");
        var role = await RequireRoleByCodeAsync(tenantId, workspaceId, request.RoleCode, cancellationToken);
        member.ChangeRole(role.Id);
        await _workspaceMemberRepository.UpdateAsync(member, cancellationToken);
    }

    public async Task RemoveMemberAsync(
        TenantId tenantId,
        long workspaceId,
        long targetUserId,
        long operatorUserId,
        bool isPlatformAdmin,
        CancellationToken cancellationToken = default)
    {
        await EnsureWorkspaceManagerAsync(tenantId, workspaceId, operatorUserId, isPlatformAdmin, cancellationToken);
        await _workspaceMemberRepository.DeleteByWorkspaceAndUserAsync(tenantId, workspaceId, targetUserId, cancellationToken);
    }

    public async Task<IReadOnlyList<WorkspaceRolePermissionDto>> GetResourcePermissionsAsync(
        TenantId tenantId,
        long workspaceId,
        long userId,
        bool isPlatformAdmin,
        string resourceType,
        long resourceId,
        CancellationToken cancellationToken = default)
    {
        await RequireWorkspaceAccessAsync(tenantId, workspaceId, userId, isPlatformAdmin, WorkspacePermissionActions.ManagePermission, cancellationToken);

        var normalizedType = NormalizeResourceType(resourceType);
        ValidateResourceType(normalizedType);

        var roles = await _workspaceRoleRepository.ListByWorkspaceAsync(tenantId, workspaceId, cancellationToken);
        var permissions = await _workspaceResourcePermissionRepository.ListByResourceAsync(tenantId, workspaceId, normalizedType, resourceId, cancellationToken);
        var permissionMap = permissions.GroupBy(x => x.WorkspaceRoleId).ToDictionary(x => x.Key, x => x.First());

        return roles.Select(role =>
        {
            var actions = permissionMap.TryGetValue(role.Id, out var item)
                ? ParseActions(item.ActionsJson)
                : ParseActions(role.DefaultActionsJson);
            return new WorkspaceRolePermissionDto(role.Id.ToString(), role.Code, role.Name, actions);
        }).ToArray();
    }

    public async Task UpdateResourcePermissionsAsync(
        TenantId tenantId,
        long workspaceId,
        long userId,
        bool isPlatformAdmin,
        string resourceType,
        long resourceId,
        WorkspaceResourcePermissionUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        await RequireWorkspaceAccessAsync(tenantId, workspaceId, userId, isPlatformAdmin, WorkspacePermissionActions.ManagePermission, cancellationToken);
        var normalizedType = NormalizeResourceType(resourceType);
        ValidateResourceType(normalizedType);

        var roles = await _workspaceRoleRepository.ListByWorkspaceAsync(tenantId, workspaceId, cancellationToken);
        var roleMap = roles.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
        var entities = new List<WorkspaceResourcePermission>();
        foreach (var item in request.Items)
        {
            if (!roleMap.TryGetValue(item.RoleCode.Trim(), out var role))
            {
                throw new BusinessException(ErrorCodes.ValidationError, $"工作空间角色不存在：{item.RoleCode}");
            }

            var normalizedActions = NormalizeActions(item.Actions);
            entities.Add(new WorkspaceResourcePermission(
                tenantId,
                workspaceId,
                role.Id,
                normalizedType,
                resourceId,
                JsonSerializer.Serialize(normalizedActions, JsonOptions),
                userId,
                _idGeneratorAccessor.NextId()));
        }

        await _workspaceResourcePermissionRepository.ReplaceAsync(
            tenantId,
            workspaceId,
            normalizedType,
            resourceId,
            entities,
            cancellationToken);
    }

    public async Task<WorkspaceAppInstanceDto> CreateAppInstanceAsync(
        TenantId tenantId,
        long workspaceId,
        long userId,
        bool isPlatformAdmin,
        WorkspaceAppInstanceCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        await RequireWorkspaceAccessAsync(tenantId, workspaceId, userId, isPlatformAdmin, WorkspacePermissionActions.Edit, cancellationToken);

        var workspace = await _workspaceRepository.FindByIdAsync(tenantId, workspaceId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, "工作空间不存在。");
        if (workspace.IsArchived)
        {
            throw new BusinessException(ErrorCodes.ValidationError, "工作空间已归档，无法创建应用实例。");
        }

        var normalizedName = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            throw new BusinessException(ErrorCodes.ValidationError, "应用实例名称不能为空。");
        }

        // AppKey 缺省时按 workspace 自动生成；规则：ws-{workspaceId}-app-{8 位时间戳后缀}
        var normalizedAppKey = string.IsNullOrWhiteSpace(request.AppKey)
            ? $"ws-{workspaceId}-app-{DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 100000000:D8}"
            : request.AppKey.Trim();

        // 复用现有 manifest 命令服务创建底层 AppManifest
        var manifestId = await _appManifestCommandService.CreateAsync(
            tenantId,
            userId,
            new AppManifestCreateRequest(
                AppKey: normalizedAppKey,
                Name: normalizedName,
                Description: request.Description?.Trim(),
                Category: request.Category?.Trim(),
                Icon: request.Icon?.Trim(),
                DataSourceId: null),
            cancellationToken);

        // 把新建 manifest 归属到 workspace（1→N 关系）
        var manifest = await _db.Queryable<AppManifest>()
            .Where(x => x.TenantIdValue == tenantId.Value && x.Id == manifestId)
            .FirstAsync(cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, "创建后未找到 AppManifest 记录。");
        manifest.AssignWorkspace(workspaceId, userId, DateTimeOffset.UtcNow);
        await _db.Updateable(manifest).ExecuteCommandAsync(cancellationToken);

        // 若 workspace 还没有默认主应用，把当前新建的 manifest 设为默认（兼容历史前端字段）
        if (!workspace.AppInstanceId.HasValue || string.IsNullOrWhiteSpace(workspace.AppKey))
        {
            workspace.BindDefaultAppInstance(manifest.Id, manifest.AppKey, userId);
            await _workspaceRepository.UpdateAsync(workspace, cancellationToken);
        }

        return new WorkspaceAppInstanceDto(
            manifest.Id.ToString(),
            manifest.AppKey,
            manifest.Name,
            manifest.Description,
            manifest.Icon,
            manifest.Category,
            manifest.Status.ToString(),
            manifest.Version,
            manifest.CreatedAt.ToString("O"),
            manifest.UpdatedAt.ToString("O"));
    }

    private async Task EnsureBuiltInRolesAsync(TenantId tenantId, long workspaceId, CancellationToken cancellationToken)
    {
        var existing = await _workspaceRoleRepository.ListByWorkspaceAsync(tenantId, workspaceId, cancellationToken);
        var existingCodes = existing.Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var inserts = new List<WorkspaceRole>();

        if (!existingCodes.Contains(WorkspaceBuiltInRoleCodes.Owner))
        {
            inserts.Add(new WorkspaceRole(
                tenantId,
                workspaceId,
                WorkspaceBuiltInRoleCodes.Owner,
                "拥有者",
                JsonSerializer.Serialize(NormalizeActions(new[]
                {
                    WorkspacePermissionActions.View,
                    WorkspacePermissionActions.Edit,
                    WorkspacePermissionActions.Publish,
                    WorkspacePermissionActions.Delete,
                    WorkspacePermissionActions.ManagePermission
                }), JsonOptions),
                isSystem: true,
                _idGeneratorAccessor.NextId()));
        }

        if (!existingCodes.Contains(WorkspaceBuiltInRoleCodes.Admin))
        {
            inserts.Add(new WorkspaceRole(
                tenantId,
                workspaceId,
                WorkspaceBuiltInRoleCodes.Admin,
                "管理员",
                JsonSerializer.Serialize(NormalizeActions(new[]
                {
                    WorkspacePermissionActions.View,
                    WorkspacePermissionActions.Edit,
                    WorkspacePermissionActions.Publish,
                    WorkspacePermissionActions.Delete,
                    WorkspacePermissionActions.ManagePermission
                }), JsonOptions),
                isSystem: true,
                _idGeneratorAccessor.NextId()));
        }

        if (!existingCodes.Contains(WorkspaceBuiltInRoleCodes.Member))
        {
            inserts.Add(new WorkspaceRole(
                tenantId,
                workspaceId,
                WorkspaceBuiltInRoleCodes.Member,
                "成员",
                JsonSerializer.Serialize(NormalizeActions(new[]
                {
                    WorkspacePermissionActions.View
                }), JsonOptions),
                isSystem: true,
                _idGeneratorAccessor.NextId()));
        }

        if (inserts.Count > 0)
        {
            await _db.Insertable(inserts.ToArray()).ExecuteCommandAsync(cancellationToken);
        }
    }

    private async Task EnsureMemberAsync(
        TenantId tenantId,
        long workspaceId,
        long userId,
        string roleCode,
        long operatorUserId,
        CancellationToken cancellationToken)
    {
        var existing = await _workspaceMemberRepository.FindByWorkspaceAndUserAsync(tenantId, workspaceId, userId, cancellationToken);
        var role = await RequireRoleByCodeAsync(tenantId, workspaceId, roleCode, cancellationToken);

        if (existing is null)
        {
            await _workspaceMemberRepository.AddAsync(
                new WorkspaceMember(
                    tenantId,
                    workspaceId,
                    userId,
                    role.Id,
                    operatorUserId,
                    _idGeneratorAccessor.NextId()),
                cancellationToken);
            return;
        }

        existing.ChangeRole(role.Id);
        await _workspaceMemberRepository.UpdateAsync(existing, cancellationToken);
    }

    private async Task<(WorkspaceMember? Member, WorkspaceRole? Role)> RequireWorkspaceAccessAsync(
        TenantId tenantId,
        long workspaceId,
        long userId,
        bool isPlatformAdmin,
        string requiredAction,
        CancellationToken cancellationToken)
    {
        var workspace = await _workspaceRepository.FindByIdAsync(tenantId, workspaceId, cancellationToken);
        if (workspace is null || workspace.IsArchived)
        {
            throw new BusinessException(ErrorCodes.NotFound, "工作空间不存在。");
        }

        if (isPlatformAdmin)
        {
            return (null, null);
        }

        var member = await _workspaceMemberRepository.FindByWorkspaceAndUserAsync(tenantId, workspaceId, userId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.Forbidden, "当前用户无权访问该工作空间。");
        var role = await _workspaceRoleRepository.FindByIdAsync(tenantId, member.WorkspaceRoleId, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.Forbidden, "当前工作空间角色不存在。");
        var actions = ParseActions(role.DefaultActionsJson);
        if (!actions.Contains(requiredAction, StringComparer.OrdinalIgnoreCase))
        {
            throw new BusinessException(ErrorCodes.Forbidden, "当前用户缺少工作空间操作权限。");
        }

        return (member, role);
    }

    private async Task EnsureWorkspaceManagerAsync(
        TenantId tenantId,
        long workspaceId,
        long userId,
        bool isPlatformAdmin,
        CancellationToken cancellationToken)
    {
        if (isPlatformAdmin)
        {
            return;
        }

        var (_, role) = await RequireWorkspaceAccessAsync(
            tenantId,
            workspaceId,
            userId,
            false,
            WorkspacePermissionActions.ManagePermission,
            cancellationToken);
        if (role is null)
        {
            return;
        }

        if (!string.Equals(role.Code, WorkspaceBuiltInRoleCodes.Owner, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(role.Code, WorkspaceBuiltInRoleCodes.Admin, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(ErrorCodes.Forbidden, "当前用户无权管理工作空间成员。");
        }
    }

    private async Task<WorkspaceRole> RequireRoleByCodeAsync(TenantId tenantId, long workspaceId, string roleCode, CancellationToken cancellationToken)
    {
        var normalizedCode = roleCode.Trim();
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            throw new BusinessException(ErrorCodes.ValidationError, "工作空间角色不能为空。");
        }

        return await _workspaceRoleRepository.FindByCodeAsync(tenantId, workspaceId, normalizedCode, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, "工作空间角色不存在。");
    }

    private static string NormalizeResourceType(string? resourceType)
    {
        return string.IsNullOrWhiteSpace(resourceType)
            ? string.Empty
            : resourceType.Trim().ToLowerInvariant();
    }

    private static void ValidateResourceType(string resourceType)
    {
        if (resourceType is "app" or "agent" or "workflow" or "chatflow" or "plugin" or "knowledge-base" or "database")
        {
            return;
        }

        throw new BusinessException(ErrorCodes.ValidationError, $"不支持的资源类型：{resourceType}");
    }

    private static string[] ParseActions(string? actionsJson)
    {
        if (string.IsNullOrWhiteSpace(actionsJson))
        {
            return Array.Empty<string>();
        }

        try
        {
            return NormalizeActions(JsonSerializer.Deserialize<string[]>(actionsJson, JsonOptions) ?? Array.Empty<string>());
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static string[] NormalizeActions(IEnumerable<string> actions)
    {
        return actions
            .Select(item => item?.Trim().ToLowerInvariant())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToArray();
    }

    private static IReadOnlyList<string> ResolveRoleActions(string? roleCode, bool isPlatformAdmin)
    {
        if (isPlatformAdmin)
        {
            return NormalizeActions(new[]
            {
                WorkspacePermissionActions.View,
                WorkspacePermissionActions.Edit,
                WorkspacePermissionActions.Publish,
                WorkspacePermissionActions.Delete,
                WorkspacePermissionActions.ManagePermission
            });
        }

        return NormalizeActions(roleCode?.Trim() switch
        {
            WorkspaceBuiltInRoleCodes.Owner => new[]
            {
                WorkspacePermissionActions.View,
                WorkspacePermissionActions.Edit,
                WorkspacePermissionActions.Publish,
                WorkspacePermissionActions.Delete,
                WorkspacePermissionActions.ManagePermission
            },
            WorkspaceBuiltInRoleCodes.Admin => new[]
            {
                WorkspacePermissionActions.View,
                WorkspacePermissionActions.Edit,
                WorkspacePermissionActions.Publish,
                WorkspacePermissionActions.Delete,
                WorkspacePermissionActions.ManagePermission
            },
            _ => new[]
            {
                WorkspacePermissionActions.View
            }
        });
    }
}
