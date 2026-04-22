using System.Globalization;
using Atlas.Application.Platform.Abstractions;
using Atlas.Application.Platform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Presentation.Shared.Controllers.Ai;

public abstract class CozeIntelligenceCompatControllerBase : ControllerBase
{
    private readonly IWorkspacePortalService _workspacePortalService;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    protected CozeIntelligenceCompatControllerBase(
        IWorkspacePortalService workspacePortalService,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _workspacePortalService = workspacePortalService;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpPost("/api/intelligence_api/ping")]
    [Authorize]
    public ActionResult<object> Ping()
    {
        return Ok(SuccessWithoutData());
    }

    [HttpPost("/api/intelligence_api/search/get_draft_intelligence_list")]
    [Authorize]
    public async Task<ActionResult<object>> GetDraftIntelligenceList(
        [FromBody] CozeGetDraftIntelligenceListRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureWorkspaceAccessibleAsync(request.space_id, cancellationToken);

        // Atlas 当前尚未提供完整的 Coze intelligence 域模型。
        // 这里先返回稳定空列表，消除前端 404，并保持现有列表页/选择器可继续工作。
        return Ok(Success(new
        {
            intelligences = Array.Empty<object>(),
            total = 0,
            has_more = false,
            next_cursor_id = string.Empty
        }));
    }

    [HttpPost("/api/intelligence_api/search/get_draft_intelligence_info")]
    [Authorize]
    public async Task<ActionResult<object>> GetDraftIntelligenceInfo(
        [FromBody] CozeGetDraftIntelligenceInfoRequest? request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var workspace = await ResolveWorkspaceAsync(null, cancellationToken);
        var intelligenceId = string.IsNullOrWhiteSpace(request?.intelligence_id)
            ? "compat-project"
            : request!.intelligence_id!;
        var intelligenceType = request?.intelligence_type is 1 or 2 or 3
            ? request.intelligence_type!.Value
            : 2;
        var displayName = intelligenceType == 1 ? $"Compat Bot {intelligenceId}" : $"Compat Project {intelligenceId}";
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);

        return Ok(Success(new
        {
            intelligence_type = intelligenceType,
            basic_info = new
            {
                id = intelligenceId,
                name = displayName,
                description = "Atlas compatibility placeholder",
                icon_uri = string.Empty,
                icon_url = string.Empty,
                space_id = workspace?.Id ?? string.Empty,
                owner_id = currentUser.UserId.ToString(CultureInfo.InvariantCulture),
                create_time = now,
                update_time = now,
                status = 1
            },
            publish_info = new
            {
                publish_time = string.Empty,
                has_published = false,
                connectors = Array.Empty<object>()
            },
            owner_info = BuildUserInfo(currentUser)
        }));
    }

    [HttpPost("/api/intelligence_api/draft_project/create")]
    [Authorize]
    public async Task<ActionResult<object>> DraftProjectCreate(
        [FromBody] CozeDraftProjectCreateRequest? request,
        CancellationToken cancellationToken)
    {
        await EnsureWorkspaceAccessibleAsync(request?.space_id, cancellationToken);
        var projectId = $"compat-project-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)}";

        return Ok(Success(new
        {
            project_id = projectId,
            audit_data = BuildAuditData()
        }));
    }

    [HttpPost("/api/intelligence_api/draft_project/update")]
    [Authorize]
    public ActionResult<object> DraftProjectUpdate([FromBody] CozeDraftProjectUpdateRequest? _request)
    {
        return Ok(Success(new
        {
            audit_data = BuildAuditData()
        }));
    }

    [HttpPost("/api/intelligence_api/draft_project/copy")]
    [Authorize]
    public async Task<ActionResult<object>> DraftProjectCopy(
        [FromBody] CozeDraftProjectCopyRequest? request,
        CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        await EnsureWorkspaceAccessibleAsync(request?.to_space_id, cancellationToken);
        var copiedProjectId = string.IsNullOrWhiteSpace(request?.project_id)
            ? $"compat-project-copy-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)}"
            : $"{request!.project_id}-copy";

        return Ok(Success(new
        {
            basic_info = new
            {
                id = copiedProjectId,
                name = string.IsNullOrWhiteSpace(request?.name) ? "Compat Copied Project" : request!.name,
                description = request?.description ?? string.Empty,
                icon_uri = request?.icon_uri ?? string.Empty,
                icon_url = string.Empty,
                status = 1
            },
            audit_data = BuildAuditData(),
            user_info = BuildUserInfo(currentUser)
        }));
    }

    [HttpPost("/api/intelligence_api/draft_project/delete")]
    [Authorize]
    public ActionResult<object> DraftProjectDelete([FromBody] CozeDraftProjectDeleteRequest? _request)
    {
        return Ok(SuccessWithoutData());
    }

    [HttpPost("/api/intelligence_api/draft_project/inner_task_list")]
    [Authorize]
    public ActionResult<object> DraftProjectInnerTaskList([FromBody] CozeDraftProjectInnerTaskListRequest? _request)
    {
        return Ok(Success(new
        {
            task_list = Array.Empty<object>()
        }));
    }

    [HttpPost("/api/intelligence_api/entity_task/search")]
    [Authorize]
    public ActionResult<object> EntityTaskSearch([FromBody] CozeEntityTaskSearchRequest? request)
    {
        var entityTaskMap = (request?.task_list ?? Array.Empty<CozeTaskStruct>())
            .Where(item => !string.IsNullOrWhiteSpace(item.entity_id))
            .ToDictionary(
                item => item.entity_id!,
                item => (object)new
                {
                    entity_id = item.entity_id,
                    entity_status = 1
                },
                StringComparer.OrdinalIgnoreCase);

        return Ok(Success(new
        {
            entity_task_map = entityTaskMap
        }));
    }

    [HttpPost("/api/intelligence_api/entity_task/process")]
    [Authorize]
    public ActionResult<object> ProcessEntityTask([FromBody] CozeProcessEntityTaskRequest? request)
    {
        return Ok(Success(new
        {
            entity_task = new
            {
                entity_id = request?.entity_id ?? string.Empty,
                entity_status = 1
            }
        }));
    }

    private async Task EnsureWorkspaceAccessibleAsync(string? workspaceId, CancellationToken cancellationToken)
    {
        _ = await ResolveWorkspaceAsync(workspaceId, cancellationToken);
    }

    private async Task<WorkspaceListItem?> ResolveWorkspaceAsync(string? workspaceId, CancellationToken cancellationToken)
    {
        var currentUser = _currentUserAccessor.GetCurrentUserOrThrow();
        var workspaces = await _workspacePortalService.ListWorkspacesAsync(
            _tenantProvider.GetTenantId(),
            currentUser.UserId,
            currentUser.IsPlatformAdmin,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return workspaces.FirstOrDefault();
        }

        return workspaces.FirstOrDefault(item => string.Equals(item.Id, workspaceId, StringComparison.OrdinalIgnoreCase));
    }

    private static object BuildUserInfo(CurrentUserInfo currentUser)
    {
        return new
        {
            user_id = currentUser.UserId.ToString(CultureInfo.InvariantCulture),
            nickname = string.IsNullOrWhiteSpace(currentUser.DisplayName) ? currentUser.Username : currentUser.DisplayName,
            avatar_url = string.Empty,
            user_unique_name = currentUser.Username
        };
    }

    private static object BuildAuditData()
    {
        return new
        {
            check_not_pass = false,
            check_not_pass_msg = string.Empty
        };
    }

    protected static object Success(object? data)
    {
        return new
        {
            code = 0,
            msg = "success",
            data
        };
    }

    protected static object SuccessWithoutData()
    {
        return new
        {
            code = 0,
            msg = "success"
        };
    }
}

public sealed record CozeGetDraftIntelligenceListRequest(
    string space_id,
    string? name,
    bool? has_published,
    int[]? status,
    int[]? types,
    int? search_scope,
    string? folder_id,
    bool? folder_include_children,
    int? order_type,
    bool? is_fav,
    bool? recently_open,
    object? option,
    int? order_by,
    string? cursor_id,
    int? size);

public sealed record CozeGetDraftIntelligenceInfoRequest(
    string? intelligence_id,
    int? intelligence_type,
    string? version);

public sealed record CozeDraftProjectCreateRequest(
    string? space_id,
    string? name,
    string? description,
    string? icon_uri,
    object? monetization_conf,
    string? create_from,
    string? folder_id);

public sealed record CozeDraftProjectUpdateRequest(
    string? project_id,
    string? name,
    string? description,
    string? icon_uri);

public sealed record CozeDraftProjectCopyRequest(
    string? project_id,
    string? to_space_id,
    string? name,
    string? description,
    string? icon_uri);

public sealed record CozeDraftProjectDeleteRequest(string? project_id);

public sealed record CozeDraftProjectInnerTaskListRequest(string? project_id);

public sealed record CozeTaskStruct(string? entity_id, int? task_type);

public sealed record CozeEntityTaskSearchRequest(IReadOnlyList<CozeTaskStruct>? task_list);

public sealed record CozeProcessEntityTaskRequest(
    string? entity_id,
    int? action,
    IReadOnlyList<string>? task_id_list);
